using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AionixScribe;

public sealed record TranscribeResponse(string Text, int TotalLatencyMs, int GeminiLatencyMs, int ClientLatencyMs);

public sealed record GeminiUsageCall(
    DateTime CreatedAtUtc,
    string ModelVersion,
    int PromptTokens,
    int CandidateTokens,
    int TotalTokens,
    double CostUsd,
    string? FinishReason,
    bool EmptyResult);

public sealed record GeminiUsageSummary(
    double? BudgetUsd,
    double? RemainingThisMonthUsd,
    double SpentTodayUsd,
    double SpentThisMonthUsd,
    double SpentAllTimeUsd,
    int CallsToday,
    int CallsThisMonth,
    int CallsAllTime,
    IReadOnlyList<GeminiUsageCall> Recent);

public sealed class BackendClient : IDisposable
{
    // TODO(P3): mover para configuração por ambiente quando existir build de release separado de dev.
    private const string BaseUrl = "https://aionix-scribe-api-production.up.railway.app";

    // Stopgap enquanto não existe conta/dispositivo real (P3, D013). ATENÇÃO: a partir do momento em
    // que existe download público do app (release no GitHub), este valor deixa de ser proteção de
    // verdade — qualquer pessoa extrai a string do .exe com `strings`. Ele continua aqui só para
    // barrar chamada direta à URL por quem nunca baixou o app. A proteção real é o D018 Passo 3
    // (Bearer/JWT) somado a um teto de gasto na própria chave da Gemini. Não trate como segredo.
    // Precisa bater com DESKTOP_SHARED_SECRET no Railway (backend/.env.example).
    private const string AppSecret = "73a2b19bdd2856312409f5b10d04c155da0894487d48a894f4a39155151876e7";

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public BackendClient()
    {
        _http.DefaultRequestHeaders.Add("X-App-Secret", AppSecret);
    }

    public async Task<TranscribeResponse> TranscribeAsync(byte[] wavAudio)
    {
        var clientStart = DateTime.UtcNow;
        using var content = new ByteArrayContent(wavAudio);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var response = await _http.PostAsync($"{BaseUrl}/api/transcribe", content);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Backend retornou {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        var text = root.GetProperty("text").GetString() ?? "";
        var latency = root.GetProperty("latency");
        var totalMs = latency.GetProperty("totalMs").GetInt32();
        var geminiMs = latency.GetProperty("geminiMs").GetInt32();
        var clientLatencyMs = (int)(DateTime.UtcNow - clientStart).TotalMilliseconds;

        // O backend já devolve usage; registrar aqui é o que permite comparar custo de entrada (áudio)
        // contra saída/thinking com números reais em vez de estimativa — base para qualquer decisão
        // futura de economia. Nenhum texto transcrito vai para o log, só a contagem de tokens.
        if (root.TryGetProperty("usage", out var usage))
        {
            DebugLog.Write($"usage: prompt={usage.GetProperty("promptTokens").GetInt32()} " +
                           $"candidate={usage.GetProperty("candidateTokens").GetInt32()} " +
                           $"total={usage.GetProperty("totalTokens").GetInt32()} " +
                           $"audioBytes={wavAudio.Length}");
        }

        return new TranscribeResponse(text, totalMs, geminiMs, clientLatencyMs);
    }

    /// Alimenta a seção "Custo de IA" (painel interno, D027) com o gasto real registrado pelo
    /// backend a cada chamada à Gemini — nada calculado no cliente, o backend é a fonte de verdade
    /// do preço.
    public async Task<GeminiUsageSummary> GetGeminiUsageAsync()
    {
        using var response = await _http.GetAsync($"{BaseUrl}/api/admin/gemini-usage");
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Backend retornou {(int)response.StatusCode}: {body}");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        double? ReadNullableDouble(string prop) =>
            root.TryGetProperty(prop, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetDouble() : null;

        var callCount = root.GetProperty("callCount");

        var recent = new List<GeminiUsageCall>();
        foreach (var item in root.GetProperty("recent").EnumerateArray())
        {
            recent.Add(new GeminiUsageCall(
                CreatedAtUtc: DateTime.Parse(item.GetProperty("createdAt").GetString()!, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
                ModelVersion: item.GetProperty("modelVersion").GetString() ?? "",
                PromptTokens: item.GetProperty("promptTokens").GetInt32(),
                CandidateTokens: item.GetProperty("candidateTokens").GetInt32(),
                TotalTokens: item.GetProperty("totalTokens").GetInt32(),
                CostUsd: item.GetProperty("costUsd").GetDouble(),
                FinishReason: item.TryGetProperty("finishReason", out var fr) && fr.ValueKind != JsonValueKind.Null ? fr.GetString() : null,
                EmptyResult: item.TryGetProperty("emptyResult", out var er) && er.GetBoolean()));
        }

        return new GeminiUsageSummary(
            BudgetUsd: ReadNullableDouble("budgetUsd"),
            RemainingThisMonthUsd: ReadNullableDouble("remainingThisMonthUsd"),
            SpentTodayUsd: root.GetProperty("spentTodayUsd").GetDouble(),
            SpentThisMonthUsd: root.GetProperty("spentThisMonthUsd").GetDouble(),
            SpentAllTimeUsd: root.GetProperty("spentAllTimeUsd").GetDouble(),
            CallsToday: callCount.GetProperty("today").GetInt32(),
            CallsThisMonth: callCount.GetProperty("thisMonth").GetInt32(),
            CallsAllTime: callCount.GetProperty("allTime").GetInt32(),
            Recent: recent);
    }

    public void Dispose() => _http.Dispose();
}
