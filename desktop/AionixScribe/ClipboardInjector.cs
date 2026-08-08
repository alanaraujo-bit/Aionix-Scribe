using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.IDataObject;

namespace AionixScribe;

/// Insere texto no campo em foco via colagem de clipboard (Ctrl+V sintético), não digitação
/// caractere-a-caractere — validado no spike (desktop/spike/HotkeySpike) que SendInput com
/// KEYEVENTF_UNICODE corrompe texto acentuado em rajada. Salva e restaura TODOS os formatos
/// presentes no clipboard (não só texto), para nunca destruir uma imagem/arquivo que o usuário
/// tinha copiado (§22 da diretiva).
public static class ClipboardInjector
{
    public static async Task InsertTextAsync(string text)
    {
        WpfDataObject? previous = null;
        try
        {
            previous = WpfClipboard.GetDataObject();
        }
        catch
        {
            // Clipboard ocasionalmente indisponível (outro processo com lock momentâneo) — segue sem backup.
        }

        WpfClipboard.SetText(text);
        await Task.Delay(80);
        Native.SendCtrlV();
        await Task.Delay(150);

        if (previous != null)
        {
            try
            {
                WpfClipboard.SetDataObject(previous, true);
            }
            catch
            {
                // Restauração best-effort; não deve derrubar o fluxo de ditado por causa disso.
            }
        }
    }
}
