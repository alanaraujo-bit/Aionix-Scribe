using System.IO;

namespace AionixScribe;

// Log temporário de diagnóstico durante o desenvolvimento do P0/P1. Não é observabilidade de
// produção (isso é §45, ainda não implementado) — só para depurar localmente. Fica em
// %LOCALAPPDATA%\AionixScribe\ (não ao lado do exe) para que a seção Privacidade das
// Configurações, que documenta essa pasta como "os dados locais", cubra também este arquivo.
public static class DebugLog
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "debug.log");

    public static void Write(string msg)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}");
        }
        catch { }
    }
}
