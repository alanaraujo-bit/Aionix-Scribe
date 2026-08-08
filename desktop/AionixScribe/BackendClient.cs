using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AionixScribe;

public sealed record TranscribeResponse(string Text, int TotalLatencyMs, int GeminiLatencyMs, int ClientLatencyMs);

public sealed class BackendClient : IDisposable
{
    // TODO(P3): mover para configuração por ambiente quando existir build de release separado de dev.
    private const string BaseUrl = "https://aionix-scribe-api-production.up.railway.app";

    // Stopgap enquanto não existe conta/dispositivo real (P3): evita que a URL pública do endpoint
    // seja usada por terceiros para queimar a cota da Gemini. Não é autenticação de usuário — o valor
    // vive embutido no binário, mesma proteção que qualquer segredo de app cliente distribuído sem
    // ofuscação. Precisa bater com DESKTOP_SHARED_SECRET no Railway (backend/.env.example).
    private const string AppSecret = "0ae6425535c84a9b61bc006b35a5ce80485713bcfb41cca858cbbac4f1e2a476";

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

        return new TranscribeResponse(text, totalMs, geminiMs, clientLatencyMs);
    }

    public void Dispose() => _http.Dispose();
}
