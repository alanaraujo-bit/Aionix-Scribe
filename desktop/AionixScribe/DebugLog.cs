using System.IO;

namespace AionixScribe;

// Log temporário de diagnóstico durante o desenvolvimento do P0/P1. Não é observabilidade de
// produção (isso é §45, ainda não implementado) — só para depurar localmente.
public static class DebugLog
{
    private static readonly string Path = System.IO.Path.Combine(AppContext.BaseDirectory, "debug.log");
    public static void Write(string msg)
    {
        try { File.AppendAllText(Path, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}{Environment.NewLine}"); } catch { }
    }
}
