using System.IO;
using System.Text.Json;

namespace AionixScribe;

/// Persiste o "me lembre depois" da atualização. Mesmo padrão dos demais settings: arquivo ausente
/// ou corrompido = comportamento padrão seguro (nada adiado).
///
/// Semântica deliberada: adiar silencia o AVISO (balão da bandeja e abertura automática do painel)
/// até a data escolhida — mas NUNCA esconde o selo de atualização, que fica visível enquanto houver
/// versão nova. Foi exatamente isso que o proprietário pediu: "pode marcar pra depois e relembrar,
/// mas sempre ia ficar uma taginha". Por isso não existe "pular esta versão".
public static class UpdateSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "update-settings.json");

    private record StoredSnooze(string? Version, DateTime? UntilUtc);

    /// True quando o aviso desta versão deve ficar quieto por enquanto. Guardamos a VERSÃO junto com
    /// o prazo: se sair uma versão mais nova que a adiada, ela avisa na hora em vez de herdar o
    /// silêncio da anterior.
    public static bool IsSnoozed(string version)
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            var stored = JsonSerializer.Deserialize<StoredSnooze>(File.ReadAllText(FilePath));
            if (stored?.Version == null || stored.UntilUtc == null) return false;
            if (!string.Equals(stored.Version, version, StringComparison.OrdinalIgnoreCase)) return false;
            return DateTime.UtcNow < stored.UntilUtc.Value;
        }
        catch
        {
            return false; // na dúvida, avisar — perder um aviso é pior que repetir um
        }
    }

    public static void Snooze(string version, TimeSpan duration)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(
                new StoredSnooze(version, DateTime.UtcNow.Add(duration))));
        }
        catch (Exception ex)
        {
            DebugLog.Write($"UpdateSettings.Snooze falhou: {ex.Message}");
        }
    }

    public static void Clear()
    {
        try
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
        catch
        {
            // Falhar aqui não muda nada de importante — no pior caso um aviso fica quieto até o prazo.
        }
    }
}
