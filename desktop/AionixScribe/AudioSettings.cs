using System.IO;
using System.Text.Json;

namespace AionixScribe;

/// Persiste a escolha de microfone do usuário. Ausência de arquivo = -1 (padrão do sistema),
/// mesmo padrão de HotkeySettings.cs. Resolve a lacuna de multi-microfone identificada desde
/// D010/D012.
public static class AudioSettings
{
    public const int SystemDefaultDeviceIndex = -1;

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "audio-settings.json");

    private record StoredAudioSettings(int DeviceIndex);

    public static int LoadDeviceIndex()
    {
        try
        {
            if (!File.Exists(FilePath)) return SystemDefaultDeviceIndex;
            var json = File.ReadAllText(FilePath);
            var stored = JsonSerializer.Deserialize<StoredAudioSettings>(json);
            return stored?.DeviceIndex ?? SystemDefaultDeviceIndex;
        }
        catch
        {
            return SystemDefaultDeviceIndex; // config corrompida ou ausente — comportamento seguro é usar o padrão
        }
    }

    public static void SaveDeviceIndex(int deviceIndex)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(new StoredAudioSettings(deviceIndex)));
    }
}
