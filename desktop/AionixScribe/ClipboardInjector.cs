using WpfClipboard = System.Windows.Clipboard;
using WpfDataObject = System.Windows.DataObject;

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
        // Clipboard.GetDataObject() devolve um wrapper "vivo" sobre a fonte original (muitas apps
        // usam delayed-rendering: só entregam os bytes quando GetData é chamado). Se apenas guardarmos
        // essa referência e sobrescrevermos o clipboard com SetText, a fonte original pode ser
        // invalidada e a restauração falha silenciosamente. Por isso copiamos cada formato para um
        // DataObject novo AGORA, enquanto a fonte original ainda está viva.
        WpfDataObject? snapshot = null;
        try
        {
            var previous = WpfClipboard.GetDataObject();
            if (previous != null)
            {
                var formats = previous.GetFormats();
                if (formats.Length > 0)
                {
                    snapshot = new WpfDataObject();
                    foreach (var format in formats)
                    {
                        try
                        {
                            var data = previous.GetData(format);
                            if (data != null) snapshot.SetData(format, data);
                        }
                        catch
                        {
                            // Formato individual pode falhar ao copiar (ex.: fonte de delayed-rendering
                            // já indisponível) — ignora esse formato, mantém os demais.
                        }
                    }
                }
            }
        }
        catch
        {
            // Clipboard ocasionalmente indisponível (outro processo com lock momentâneo) — segue sem backup.
        }

        WpfClipboard.SetText(text);
        await Task.Delay(80);
        Native.SendCtrlV();
        await Task.Delay(150);

        if (snapshot != null && snapshot.GetFormats().Length > 0)
        {
            try
            {
                WpfClipboard.SetDataObject(snapshot, true);
            }
            catch
            {
                // Restauração best-effort; não deve derrubar o fluxo de ditado por causa disso.
            }
        }
    }
}
