using Microsoft.Win32;
using System.IO;
using System.Text.Json;

namespace AionixScribe;

public enum ThemePreference { System, Light, Dark }

/// Persiste a preferência de tema do usuário. Ausência de arquivo = Sistema, mesmo padrão de
/// HotkeySettings/AudioSettings/OnboardingSettings — sem flag salva = comportamento default
/// seguro (acompanhar o Windows).
public static class ThemeSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "theme-settings.json");

    private const string PersonalizeKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    // Preference como string? para que um theme-settings.json futuro/antigo com valor desconhecido
    // desserialize sem estourar — cai no default System abaixo (mesmo truque de HotkeySettings.Mode).
    private record StoredTheme(string? Preference);

    public static ThemePreference Load()
    {
        try
        {
            if (!File.Exists(FilePath)) return ThemePreference.System;
            var json = File.ReadAllText(FilePath);
            var stored = JsonSerializer.Deserialize<StoredTheme>(json);
            return stored?.Preference switch
            {
                nameof(ThemePreference.Light) => ThemePreference.Light,
                nameof(ThemePreference.Dark) => ThemePreference.Dark,
                _ => ThemePreference.System,
            };
        }
        catch
        {
            return ThemePreference.System; // config corrompida ou ausente — acompanhar o Windows é o default seguro
        }
    }

    public static void Save(ThemePreference preference)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoredTheme(preference.ToString())));
    }

    /// AppsUseLightTheme (DWORD): 0 = escuro, 1 = claro. Chave ou valor ausente (Windows antigo,
    /// Server, ou leitura falhando por qualquer motivo) = assume escuro — mesmo default que o app
    /// já usava antes de ThemePreference existir.
    public static bool IsWindowsLightThemeActive()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath, writable: false);
            return key?.GetValue("AppsUseLightTheme") is int value && value != 0;
        }
        catch
        {
            return false;
        }
    }
}
