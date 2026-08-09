using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace AionixScribe;

/// Um item do "o que mudou" mostrado no painel de atualização.
public sealed record UpdateHighlight(
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("body")] string Body);

/// Conteúdo de update.json, publicado como ativo de nome fixo em cada release.
public sealed record UpdateManifest(
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("releasedAt")] string? ReleasedAt,
    [property: JsonPropertyName("headline")] string? Headline,
    [property: JsonPropertyName("summary")] string? Summary,
    [property: JsonPropertyName("highlights")] UpdateHighlight[]? Highlights,
    [property: JsonPropertyName("setupUrl")] string? SetupUrl,
    [property: JsonPropertyName("setupSha256")] string? SetupSha256);

/// Verifica, baixa e aplica atualizações.
///
/// Origem: um update.json publicado com nome FIXO em cada release do GitHub, então
/// /releases/latest/download/update.json sempre aponta para a versão mais recente — sem depender da
/// API do GitHub (que tem limite de requisições por IP) nem de interpretar o texto em markdown do
/// release, que renderizar em WPF seria trabalhoso e frágil. O "o que mudou" chega estruturado.
public static class UpdateService
{
    private const string ManifestUrl = "https://github.com/alanaraujo-bit/Aionix-Scribe/releases/latest/download/update.json";

    /// Hosts de onde aceitamos baixar o instalador. Um manifesto pode declarar qualquer setupUrl;
    /// sem esta lista, um erro futuro na hospedagem do manifesto viraria execução remota de código
    /// em toda máquina instalada. Barato agora, impossível de retrofitar depois que clientes saem.
    private static readonly string[] AllowedDownloadHosts =
    {
        "github.com",
        "objects.githubusercontent.com",
        "release-assets.githubusercontent.com",
    };

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(10) };

    /// Versão em execução, vinda de <Version> no .csproj via atributo do assembly.
    public static Version CurrentVersion
    {
        get
        {
            var raw = Assembly.GetEntryAssembly()?.GetName().Version;
            return raw ?? new Version(0, 0, 0, 0);
        }
    }

    public static string CurrentVersionDisplay => $"{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    /// Busca o manifesto e devolve-o SOMENTE se descrever uma versão realmente mais nova.
    /// Retorna null em qualquer outro caso — inclusive erro de rede, JSON inválido ou versão
    /// impossível de interpretar. "Não sei" nunca vira "tem atualização".
    public static async Task<UpdateManifest?> CheckAsync()
    {
        try
        {
            var json = await Http.GetStringAsync(ManifestUrl);
            var manifest = JsonSerializer.Deserialize<UpdateManifest>(json);
            if (manifest == null || string.IsNullOrWhiteSpace(manifest.Version))
            {
                DebugLog.Write("update: manifesto vazio ou sem versão");
                return null;
            }

            if (!Version.TryParse(manifest.Version, out var remote))
            {
                DebugLog.Write($"update: versão do manifesto ilegível ('{manifest.Version}')");
                return null;
            }

            var local = CurrentVersion;
            // Normaliza para 3 componentes: o assembly carimba 0.2.0.0 e o manifesto traz "0.2.0";
            // comparar direto faria 0.2.0 < 0.2.0.0 e nunca acabaria de atualizar (ou nunca começaria).
            var remoteN = new Version(remote.Major, remote.Minor, Math.Max(remote.Build, 0));
            var localN = new Version(local.Major, local.Minor, Math.Max(local.Build, 0));

            DebugLog.Write($"update: local={localN} remoto={remoteN}");

            // Menor OU IGUAL não é atualização. Sem isso, um manifesto em cache velho ou um release
            // publicado errado reinstalaria a mesma versão em laço.
            if (remoteN <= localN) return null;

            return manifest;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"update: falha ao verificar: {ex.Message}");
            return null;
        }
    }

    /// Baixa o instalador, confere o SHA-256 e executa a instalação silenciosa.
    /// Lança em qualquer falha — quem chama mostra a mensagem ao usuário.
    public static async Task DownloadAndInstallAsync(UpdateManifest manifest, IProgress<double>? progress = null)
    {
        if (string.IsNullOrWhiteSpace(manifest.SetupUrl))
        {
            throw new InvalidOperationException("O manifesto de atualização não informa o instalador.");
        }

        // Hash AUSENTE falha fechado, igual a hash errado. Se pudesse ser omitido, bastaria um
        // manifesto sem o campo para desligar a verificação inteira.
        if (string.IsNullOrWhiteSpace(manifest.SetupSha256))
        {
            throw new InvalidOperationException("O manifesto de atualização não traz o hash de verificação do instalador.");
        }

        var uri = new Uri(manifest.SetupUrl);
        if (uri.Scheme != Uri.UriSchemeHttps || !AllowedDownloadHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Endereço de download não confiável: {uri.Host}");
        }

        var targetDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AionixScribe", "updates");
        Directory.CreateDirectory(targetDir);
        var setupPath = Path.Combine(targetDir, $"AionixScribe-Setup-{manifest.Version}.exe");

        using (var response = await Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead))
        {
            response.EnsureSuccessStatusCode();
            var total = response.Content.Headers.ContentLength ?? -1L;

            using var source = await response.Content.ReadAsStreamAsync();
            using var file = new FileStream(setupPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long read = 0;
            int n;
            while ((n = await source.ReadAsync(buffer)) > 0)
            {
                await file.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                if (total > 0) progress?.Report((double)read / total);
            }
        }

        var actual = Convert.ToHexString(await SHA256.HashDataAsync(File.OpenRead(setupPath))).ToLowerInvariant();
        var expected = manifest.SetupSha256.Trim().ToLowerInvariant();
        if (actual != expected)
        {
            TryDelete(setupPath);
            DebugLog.Write($"update: hash divergente (esperado {expected}, obtido {actual})");
            throw new InvalidOperationException("O instalador baixado não confere com o esperado e foi descartado. Tente de novo mais tarde.");
        }

        DebugLog.Write($"update: instalador verificado, iniciando instalação silenciosa de {manifest.Version}");

        // /SILENT sem /NORESTART: o instalador fecha o app em execução (CloseApplications) e o
        // reabre no fim (RestartApplications). Quem relança é o instalador, não nós — dois
        // relançamentos criariam duas instâncias disputando o mesmo atalho global.
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(setupPath)
        {
            Arguments = "/SILENT /SUPPRESSMSGBOXES /NORESTART",
            UseShellExecute = true,
        });
    }

    private static void TryDelete(string path)
    {
        try { File.Delete(path); } catch { /* arquivo já sumiu ou está travado — irrelevante aqui */ }
    }
}
