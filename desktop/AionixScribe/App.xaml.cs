using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace AionixScribe;

public partial class App : System.Windows.Application
{
    private HotkeyManager? _hotkey;
    private readonly AudioRecorder _recorder = new();
    private readonly BackendClient _backend = new();
    private OverlayWindow? _overlay;
    private Forms.NotifyIcon? _tray;
    private DispatcherTimer? _hideTimer;

    private enum AppState { Idle, Listening, Processing }
    private AppState _state = AppState.Idle;

    // Candidatos tentados em ordem até um registrar sem conflito. Configuração manual de atalho
    // fica para a UI de settings (P2) — por ora, resiliência automática é melhor que travar num
    // único combo fixo que pode já estar em uso por outro app na máquina do usuário.
    private static readonly (uint Modifiers, uint Vk, string Label)[] HotkeyCandidates =
    {
        (Native.MOD_CONTROL | Native.MOD_ALT, 0x20, "Ctrl+Alt+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, 0x20, "Ctrl+Alt+Shift+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_WIN, 0x20, "Ctrl+Win+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, 0x44, "Ctrl+Alt+Shift+D"),
    };

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        SetupTrayIcon();
        RegisterHotkeyWithFallback();
    }

    private void RegisterHotkeyWithFallback()
    {
        foreach (var (modifiers, vk, label) in HotkeyCandidates)
        {
            try
            {
                _hotkey = new HotkeyManager(modifiers, vk);
                _hotkey.Triggered += OnHotkeyTriggered;
                _tray!.Text = $"Aionix Scribe — {label}";
                _tray!.ShowBalloonTip(3000, "Aionix Scribe", $"Atalho ativo: {label}", Forms.ToolTipIcon.Info);
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "active_hotkey.txt"), label);
                return;
            }
            catch (HotkeyConflictException)
            {
                // tenta o próximo candidato
            }
        }

        _tray!.Text = "Aionix Scribe — sem atalho disponível";
        _tray!.ShowBalloonTip(8000, "Aionix Scribe",
            "Todos os atalhos padrão já estão em uso por outros aplicativos. Abra o menu da bandeja para mais informações.",
            Forms.ToolTipIcon.Warning);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _tray?.ShowBalloonTip(5000, "Aionix Scribe", $"Erro inesperado: {e.Exception.Message}", Forms.ToolTipIcon.Error);
        _state = AppState.Idle;
        e.Handled = true; // não derruba o app por um erro pontual — melhor continuar disponível
    }

    private void SetupTrayIcon()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = Drawing.SystemIcons.Application,
            Visible = true,
            Text = "Aionix Scribe",
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Sair", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
    }

    private async void OnHotkeyTriggered()
    {
        switch (_state)
        {
            case AppState.Idle:
                StartListening();
                break;
            case AppState.Listening:
                await StopAndProcessAsync();
                break;
            case AppState.Processing:
                // Ignora nova ativação enquanto um processamento já está em andamento (§44).
                break;
        }
    }

    private void StartListening()
    {
        try
        {
            _recorder.Start();
        }
        catch (Exception ex)
        {
            _tray?.ShowBalloonTip(5000, "Aionix Scribe", $"Não foi possível acessar o microfone: {ex.Message}", Forms.ToolTipIcon.Error);
            return; // permanece em Idle — não há gravação para processar depois
        }
        _state = AppState.Listening;
        ShowOverlay(OverlayState.Listening);
    }

    private async Task StopAndProcessAsync()
    {
        _state = AppState.Processing;
        UpdateOverlay(OverlayState.Processing);

        var wav = _recorder.Stop();
        if (wav == null || wav.Length == 0)
        {
            UpdateOverlay(OverlayState.Cancelled);
            HideOverlayAfter(TimeSpan.FromSeconds(1.2));
            _state = AppState.Idle;
            return;
        }

        try
        {
            var result = await _backend.TranscribeAsync(wav);
            if (string.IsNullOrWhiteSpace(result.Text))
            {
                UpdateOverlay(OverlayState.Cancelled, "Nenhuma fala detectada");
            }
            else
            {
                await ClipboardInjector.InsertTextAsync(result.Text);
                UpdateOverlay(OverlayState.Done);
            }
        }
        catch (Exception ex)
        {
            UpdateOverlay(OverlayState.Error, "Erro: verifique sua conexão");
            _tray?.ShowBalloonTip(4000, "Aionix Scribe", $"Falha ao transcrever: {ex.Message}", Forms.ToolTipIcon.Error);
        }
        finally
        {
            HideOverlayAfter(TimeSpan.FromSeconds(1.5));
            _state = AppState.Idle;
        }
    }

    private void ShowOverlay(OverlayState state)
    {
        _hideTimer?.Stop();
        _overlay ??= new OverlayWindow();
        _overlay.SetState(state);
        _overlay.Show();
    }

    private void UpdateOverlay(OverlayState state, string? detail = null) => _overlay?.SetState(state, detail);

    private void HideOverlayAfter(TimeSpan delay)
    {
        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer { Interval = delay };
        _hideTimer.Tick += (_, _) =>
        {
            _hideTimer!.Stop();
            _overlay?.Hide();
        };
        _hideTimer.Start();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _recorder.Dispose();
        _backend.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
