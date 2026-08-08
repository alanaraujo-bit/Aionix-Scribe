using System.IO;
using System.Text.Json;

namespace AionixScribe;

public enum HotkeyMode { Toggle, PushToTalk }

public sealed record HotkeyChoice(uint Modifiers, uint Vk, string Label, HotkeyMode Mode = HotkeyMode.Toggle);

/// Persiste a escolha de atalho do usuário. Ausência de arquivo = "automático" (cadeia de
/// fallback em App.xaml.cs). Formato simples de propósito — não é um sistema de configurações
/// geral (isso é P2); só resolve a lacuna identificada em DECISIONS.md D010.
public static class HotkeySettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AionixScribe", "settings.json");

    // Mode como string? (não HotkeyMode) para que um settings.json antigo sem o campo desserialize
    // com Mode == null em vez de estourar — daí caímos no default Toggle abaixo.
    private record StoredHotkey(uint Modifiers, uint Vk, string Label, string? Mode = null);

    public static HotkeyChoice? LoadCustom()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var json = File.ReadAllText(FilePath);
            var stored = JsonSerializer.Deserialize<StoredHotkey>(json);
            if (stored == null) return null;
            var mode = stored.Mode == nameof(HotkeyMode.PushToTalk) ? HotkeyMode.PushToTalk : HotkeyMode.Toggle;
            return new HotkeyChoice(stored.Modifiers, stored.Vk, stored.Label, mode);
        }
        catch
        {
            return null; // config corrompida ou ausente — comportamento seguro é usar o automático
        }
    }

    public static void SaveCustom(HotkeyChoice choice)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        var stored = new StoredHotkey(choice.Modifiers, choice.Vk, choice.Label, choice.Mode.ToString());
        File.WriteAllText(FilePath, JsonSerializer.Serialize(stored));
    }

    public static void ClearCustom()
    {
        try { File.Delete(FilePath); } catch { /* best-effort */ }
    }
}
