using System.IO;

namespace AionixScribe;

/// Preserva áudio gravado quando a transcrição falha (rede, API, timeout), em vez de descartar
/// o trabalho do usuário — §23 da diretiva: nunca tratar a fala do usuário como descartável.
public static class PendingRecordings
{
    private static readonly string Folder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "pending");

    public static string Save(byte[] wav)
    {
        Directory.CreateDirectory(Folder);
        var path = Path.Combine(Folder, $"{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.wav");
        File.WriteAllBytes(path, wav);
        return path;
    }

    public static IReadOnlyList<string> List()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<string>();
        return Directory.GetFiles(Folder, "*.wav");
    }

    public static byte[] Read(string path) => File.ReadAllBytes(path);

    public static void Delete(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }
}
