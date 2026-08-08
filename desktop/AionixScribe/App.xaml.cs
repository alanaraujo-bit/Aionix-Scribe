using System.Windows;
using System.Windows.Threading;
using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

namespace AionixScribe;

public partial class App : System.Windows.Application
{
    private HotkeyManager? _hotkey;
    private PushToTalkHook? _pushToTalkHook;
    private HotkeyMode _hotkeyMode = HotkeyMode.Toggle;
    private uint _currentModifiers;
    private uint _currentVk;
    private readonly AudioRecorder _recorder = new();
    private readonly BackendClient _backend = new();
    private OverlayWindow? _overlay;
    private Forms.NotifyIcon? _tray;
    private DispatcherTimer? _hideTimer;
    private Forms.ToolStripMenuItem? _pendingMenuItem;
    private SettingsWindow? _settingsWindow;
    private HistoryWindow? _historyWindow;
    private MainPanelWindow? _mainPanel;

    private enum AppState { Idle, Listening, Processing }
    private AppState _state = AppState.Idle;

    public string CurrentHotkeyLabel { get; private set; } = "nenhum atalho ativo";

    // Candidatos tentados em ordem até um registrar sem conflito, quando não há preferência
    // salva pelo usuário (ver HotkeySettings / SettingsWindow, DECISIONS.md D010).
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

        var custom = HotkeySettings.LoadCustom();
        if (custom != null && TryRegister(custom.Modifiers, custom.Vk, custom.Label, custom.Mode))
        {
            NotifyActiveHotkey();
            return;
        }

        RegisterHotkeyWithFallback();
    }

    private void RegisterHotkeyWithFallback()
    {
        foreach (var (modifiers, vk, label) in HotkeyCandidates)
        {
            if (TryRegister(modifiers, vk, label, HotkeyMode.Toggle))
            {
                NotifyActiveHotkey();
                return;
            }
        }

        CurrentHotkeyLabel = "sem atalho disponível";
        _tray!.Text = "Aionix Scribe — sem atalho disponível";
        _tray!.ShowBalloonTip(8000, "Aionix Scribe",
            "Todos os atalhos padrão já estão em uso por outros aplicativos. Abra Configurações para escolher outro.",
            Forms.ToolTipIcon.Warning);
    }

    /// Cria o mecanismo do modo pedido ANTES de descartar o atual — se a criação lançar (conflito
    /// de RegisterHotKey, ou falha ao instalar o hook), o mecanismo anterior continua funcionando.
    private void RegisterCombo(uint modifiers, uint vk, HotkeyMode mode)
    {
        if (mode == HotkeyMode.Toggle)
        {
            var candidate = new HotkeyManager(modifiers, vk);
            _hotkey?.Dispose();
            _pushToTalkHook?.Dispose();
            _pushToTalkHook = null;
            _hotkey = candidate;
            _hotkey.Triggered += OnHotkeyTriggered;
        }
        else
        {
            var candidate = new PushToTalkHook(modifiers, vk);
            _hotkey?.Dispose();
            _pushToTalkHook?.Dispose();
            _hotkey = null;
            _pushToTalkHook = candidate;
            _pushToTalkHook.Pressed += OnPushToTalkPressed;
            _pushToTalkHook.Released += OnPushToTalkReleased;
        }
    }

    /// Tenta registrar um combo, substituindo o atual se houver um. Não salva preferência —
    /// isso é responsabilidade de quem chama (settings salva, startup/fallback não salva).
    private bool TryRegister(uint modifiers, uint vk, string label, HotkeyMode mode)
    {
        try
        {
            RegisterCombo(modifiers, vk, mode);
        }
        catch (HotkeyConflictException)
        {
            return false;
        }
        catch (PushToTalkHookException)
        {
            return false;
        }

        _hotkeyMode = mode;
        _currentModifiers = modifiers;
        _currentVk = vk;
        CurrentHotkeyLabel = label;
        return true;
    }

    private void NotifyActiveHotkey()
    {
        _tray!.Text = $"Aionix Scribe — {CurrentHotkeyLabel}";
        _tray!.ShowBalloonTip(3000, "Aionix Scribe", $"Atalho ativo: {CurrentHotkeyLabel}", Forms.ToolTipIcon.Info);
    }

    /// Chamado pela SettingsWindow quando o usuário captura um novo atalho. Em caso de conflito,
    /// mantém o atalho anterior funcionando (não deixa o app sem nenhum atalho registrado).
    public bool TryChangeHotkey(uint modifiers, uint vk, string label, out string? error)
    {
        try
        {
            RegisterCombo(modifiers, vk, _hotkeyMode);
        }
        catch (HotkeyConflictException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (PushToTalkHookException ex)
        {
            error = ex.Message;
            return false;
        }

        _currentModifiers = modifiers;
        _currentVk = vk;
        CurrentHotkeyLabel = label;
        HotkeySettings.SaveCustom(new HotkeyChoice(modifiers, vk, label, _hotkeyMode));
        _tray!.Text = $"Aionix Scribe — {CurrentHotkeyLabel}";
        error = null;
        return true;
    }

    /// Chamado pela SettingsWindow ao trocar entre Toggle e PushToTalk, mantendo o combo atual.
    public bool TryChangeMode(HotkeyMode mode, out string? error)
    {
        if (mode == _hotkeyMode)
        {
            error = null;
            return true;
        }

        try
        {
            RegisterCombo(_currentModifiers, _currentVk, mode);
        }
        catch (HotkeyConflictException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (PushToTalkHookException ex)
        {
            error = ex.Message;
            return false;
        }

        _hotkeyMode = mode;
        HotkeySettings.SaveCustom(new HotkeyChoice(_currentModifiers, _currentVk, CurrentHotkeyLabel, mode));
        error = null;
        return true;
    }

    public void ResetHotkeyToAuto()
    {
        _hotkey?.Dispose();
        _hotkey = null;
        _pushToTalkHook?.Dispose();
        _pushToTalkHook = null;
        _hotkeyMode = HotkeyMode.Toggle;
        RegisterHotkeyWithFallback();
    }

    public HotkeyMode CurrentHotkeyMode => _hotkeyMode;

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
        menu.Items.Add("Abrir Aionix Scribe", null, (_, _) => OpenMainPanel());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Histórico...", null, (_, _) => OpenHistory());
        menu.Items.Add("Configurações...", null, (_, _) => OpenSettings());
        _pendingMenuItem = new Forms.ToolStripMenuItem("Reprocessar pendências", null, OnReprocessPendingClicked) { Enabled = false };
        menu.Items.Add(_pendingMenuItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Shutdown());
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenMainPanel();

        RefreshPendingMenu();
    }

    public void OpenMainPanel()
    {
        if (_mainPanel == null || !_mainPanel.IsLoaded)
        {
            _mainPanel = new MainPanelWindow();
            _mainPanel.Closed += (_, _) => _mainPanel = null;
        }
        else
        {
            _mainPanel.Refresh();
        }
        _mainPanel.Show();
        _mainPanel.Activate();
    }

    private async void OnHotkeyTriggered()
    {
        DebugLog.Write($"OnHotkeyTriggered, state={_state}");
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

    private void OnPushToTalkPressed()
    {
        DebugLog.Write($"OnPushToTalkPressed, state={_state}");
        if (_state == AppState.Idle) StartListening();
    }

    private async void OnPushToTalkReleased()
    {
        DebugLog.Write($"OnPushToTalkReleased, state={_state}");
        if (_state == AppState.Listening) await StopAndProcessAsync();
    }

    private void StartListening()
    {
        try
        {
            // Lido a cada gravação (não só no startup) para que uma troca de microfone em
            // Configurações valha já no próximo ditado, sem precisar reiniciar o app.
            _recorder.DeviceIndex = AudioSettings.LoadDeviceIndex();
            _recorder.Start();
        }
        catch (NoMicrophoneException ex)
        {
            DebugLog.Write($"StartListening: {ex.Message}");
            _tray?.ShowBalloonTip(6000, "Aionix Scribe", ex.Message, Forms.ToolTipIcon.Warning);
            return;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"StartListening: falha ao acessar microfone: {ex}");
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
        DebugLog.Write($"StopAndProcessAsync: wav bytes = {wav?.Length ?? -1}");
        if (wav == null || wav.Length == 0)
        {
            UpdateOverlay(OverlayState.Cancelled);
            HideOverlayAfter(TimeSpan.FromSeconds(1.2));
            _state = AppState.Idle;
            return;
        }

        var ok = await TryTranscribeAndInsertAsync(wav);
        if (!ok)
        {
            // Uma falha isolada (rede/timeout) merece uma segunda tentativa automática antes de
            // desistir — §23: falha técnica não deve custar a fala do usuário na primeira tentativa.
            await Task.Delay(1000);
            ok = await TryTranscribeAndInsertAsync(wav);
        }

        if (!ok)
        {
            PendingRecordings.Save(wav);
            UpdateOverlay(OverlayState.Error, "Erro — gravação preservada");
            _tray?.ShowBalloonTip(6000, "Aionix Scribe",
                "Não consegui transcrever depois de duas tentativas. Sua gravação foi preservada — use \"Reprocessar pendências\" no menu da bandeja quando a conexão voltar.",
                Forms.ToolTipIcon.Warning);
            RefreshPendingMenu();
        }

        HideOverlayAfter(TimeSpan.FromSeconds(1.5));
        _state = AppState.Idle;
    }

    /// Retorna true se transcreveu e inseriu com sucesso (mesmo que o resultado seja "sem fala").
    /// Retorna false apenas em falha técnica real (rede, backend, timeout) — esse é o único caso
    /// que justifica preservar o áudio para retry.
    private async Task<bool> TryTranscribeAndInsertAsync(byte[] wav)
    {
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
                HistoryStore.Add(result.Text);
            }
            return true;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"TryTranscribeAndInsertAsync failed: {ex}");
            return false;
        }
    }

    private void RefreshPendingMenu()
    {
        if (_pendingMenuItem == null) return;
        var count = PendingRecordings.List().Count;
        DebugLog.Write($"RefreshPendingMenu: {count} pendências");
        _pendingMenuItem.Text = count > 0 ? $"Reprocessar pendências ({count})" : "Reprocessar pendências";
        _pendingMenuItem.Enabled = count > 0;
    }

    private async void OnReprocessPendingClicked(object? sender, EventArgs e)
    {
        var pending = PendingRecordings.List();
        if (pending.Count == 0) return;

        var path = pending[0];
        _state = AppState.Processing;
        ShowOverlay(OverlayState.Processing);

        var wav = PendingRecordings.Read(path);
        var ok = await TryTranscribeAndInsertAsync(wav);
        if (ok)
        {
            PendingRecordings.Delete(path);
        }
        else
        {
            _tray?.ShowBalloonTip(4000, "Aionix Scribe", "Ainda não consegui reprocessar essa gravação. Ela continua preservada.", Forms.ToolTipIcon.Warning);
        }

        HideOverlayAfter(TimeSpan.FromSeconds(1.5));
        _state = AppState.Idle;
        RefreshPendingMenu();
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

    public void OpenHistory()
    {
        if (_historyWindow == null || !_historyWindow.IsLoaded)
        {
            _historyWindow = new HistoryWindow();
            _historyWindow.Closed += (_, _) => _historyWindow = null;
        }
        _historyWindow.Show();
        _historyWindow.Activate();
    }

    public void OpenSettings()
    {
        if (_settingsWindow == null || !_settingsWindow.IsLoaded)
        {
            _settingsWindow = new SettingsWindow();
            _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        }
        _settingsWindow.Show();
        _settingsWindow.Activate();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _hotkey?.Dispose();
        _pushToTalkHook?.Dispose();
        _recorder.Dispose();
        _backend.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
