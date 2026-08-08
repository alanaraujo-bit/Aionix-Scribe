using System.IO;
using System.Text.Json;

namespace AionixScribe;

/// Marca se o usuário já passou pelo onboarding (guiado ou pulado). Ausência de arquivo =
/// nunca visto, mesmo padrão de HotkeySettings/AudioSettings — sem flag salva = comportamento
/// default seguro (mostrar o onboarding).
public static class OnboardingSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "onboarding.json");

    private record StoredOnboarding(bool Completed);

    public static bool IsCompleted()
    {
        try
        {
            if (!File.Exists(FilePath)) return false;
            var json = File.ReadAllText(FilePath);
            var stored = JsonSerializer.Deserialize<StoredOnboarding>(json);
            return stored?.Completed ?? false;
        }
        catch
        {
            return false; // config corrompida ou ausente — melhor mostrar o onboarding de novo do que nunca mostrar
        }
    }

    public static void MarkCompleted()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoredOnboarding(true)));
    }
}
