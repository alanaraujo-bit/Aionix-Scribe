using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AionixScribe;

public sealed record TranscribeResponse(string Text, int TotalLatencyMs, int GeminiLatencyMs, int ClientLatencyMs);

public sealed class BackendClient : IDisposable
{
    // TODO(P3): mover para configuração por ambiente quando existir build de release separado de dev.
    private const string BaseUrl = "https://aionix-scribe-api-production.up.railway.app";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

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
