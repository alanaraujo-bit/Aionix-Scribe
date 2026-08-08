using Microsoft.Win32;

namespace AionixScribe;

/// Liga/desliga a inicialização automática via HKCU\...\Run — funciona rodando direto do
/// bin/Release, sem depender de instalador.
public static class StartupSettings
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "AionixScribe";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        var stored = key?.GetValue(ValueName) as string;
        if (string.IsNullOrEmpty(stored)) return false;

        var currentPath = GetExecutablePath();
        if (currentPath == null) return false;

        // O valor é gravado entre aspas (necessário para caminhos com espaço) — remove antes de
        // comparar. Caminho antigo/diferente do atual (app movido) conta como desabilitado, para
        // não mostrar um falso-positivo.
        return string.Equals(stored.Trim('"'), currentPath, StringComparison.OrdinalIgnoreCase);
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
            ?? Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);

        if (enabled)
        {
            var currentPath = GetExecutablePath()
                ?? throw new InvalidOperationException("Não foi possível determinar o caminho do executável.");
            key.SetValue(ValueName, $"\"{currentPath}\"");
        }
        else
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
    }

    private static string? GetExecutablePath() =>
        Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
}
