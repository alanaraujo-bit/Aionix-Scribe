using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AionixScribe;

public sealed record TranscribeResponse(string Text, int TotalLatencyMs, int GeminiLatencyMs, int ClientLatencyMs);

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

    public void Dispose() => _http.Dispose();
}
