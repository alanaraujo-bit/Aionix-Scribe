using Microsoft.Win32;
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
    private Forms.ContextMenuStrip? _trayMenu;
    private MainPanelWindow? _mainPanel;
    private bool? _appliedLightTheme;

    private enum AppState { Idle, Listening, Processing }
    private AppState _state = AppState.Idle;

    public string CurrentHotkeyLabel { get; private set; } = "nenhum atalho ativo";

    /// False apenas quando todos os candidatos de atalho conflitam e nenhum ficou registrado
    /// (RegisterHotkeyWithFallback esgotado). OnboardingWindow usa isso em vez de comparar contra
    /// o texto de CurrentHotkeyLabel.
    public bool HasActiveHotkey { get; private set; }

    /// Disparado quando uma transcrição real resulta em texto inserido com sucesso (não para
    /// "nenhuma fala detectada"). Consumido pela OnboardingWindow para saber quando o primeiro
    /// ditado do usuário deu certo.
    public event Action<string>? DictationSucceeded;

    // Candidatos tentados em ordem até um registrar sem conflito, quando não há preferência
    // salva pelo usuário (ver HotkeySettings / SettingsSection, DECISIONS.md D010).
    private static readonly (uint Modifiers, uint Vk, string Label)[] HotkeyCandidates =
    {
        (Native.MOD_CONTROL | Native.MOD_ALT, 0x20, "Ctrl+Alt+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, 0x20, "Ctrl+Alt+Shift+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_WIN, 0x20, "Ctrl+Win+Espaço"),
        (Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, 0x44, "Ctrl+Alt+Shift+D"),
    };

    /// Manifesto da versão nova, quando existe uma. Lido pelo painel de atualização.
    public UpdateManifest? PendingUpdate { get; private set; }

    private static Mutex? _singleInstanceMutex;
    private DispatcherTimer? _updateTimer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Instância única. Vira obrigatório com a atualização automática: o instalador reabre o app
        // ao terminar (RestartApplications) e a chave HKCU\...\Run também pode disparar — duas
        // instâncias competiriam pelo MESMO atalho global, e a segunda falharia em registrá-lo,
        // deixando o usuário com um "sem atalho disponível" sem explicação.
        _singleInstanceMutex = new Mutex(initiallyOwned: true, "Local\\AionixScribe.SingleInstance", out var isFirst);
        if (!isFirst)
        {
            DebugLog.Write("Outra instância já está rodando — encerrando esta.");
            Shutdown();
            return;
        }

        ApplyTheme(); // primeiro, antes de qualquer janela (onboarding pode abrir logo em seguida)
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        SystemEvents.UserPreferenceChanged += OnSystemPreferenceChanged;

        SetupTrayIcon();

        var custom = HotkeySettings.LoadCustom();
        if (custom != null && TryRegister(custom.Modifiers, custom.Vk, custom.Label, custom.Mode))
        {
            NotifyActiveHotkey();
            MaybeShowOnboarding();
            return;
        }

        RegisterHotkeyWithFallback();
        MaybeShowOnboarding();
        StartUpdateChecks();
    }

    /// Se a versão mudou entre uma execução e a seguinte, foi atualização — avisa. Roda antes da
    /// primeira verificação para o usuário ver a confirmação logo ao voltar, não 20 segundos depois.
    private void AnnounceUpdateIfVersionChanged()
    {
        var current = UpdateService.CurrentVersionDisplay;
        var previous = UpdateSettings.ExchangeLastRunVersion(current);
        if (previous == null || previous == current) return;

        DebugLog.Write($"update: versão mudou de {previous} para {current}");
        UpdateSettings.Clear(); // adiamento da versão antiga não faz mais sentido
        PendingUpdate = null;
        ToastWindow.Show($"Atualizado para a versão {current}. Tudo pronto — seu atalho e seu histórico continuam como estavam.",
            ToastKind.Info);
    }

    /// Primeira verificação com atraso para não competir com o startup (registro de atalho, bandeja,
    /// onboarding), e depois de 6 em 6 horas — o app costuma ficar dias aberto na bandeja, então
    /// verificar só na inicialização deixaria quem nunca reinicia sem nunca saber de versão nova.
    private void StartUpdateChecks()
    {
        AnnounceUpdateIfVersionChanged();
        _updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
        _updateTimer.Tick += async (_, _) => await CheckForUpdateAsync();
        _updateTimer.Start();

        var firstCheck = new DispatcherTimer { Interval = TimeSpan.FromSeconds(20) };
        firstCheck.Tick += async (s, _) =>
        {
            ((DispatcherTimer)s!).Stop();
            await CheckForUpdateAsync();
        };
        firstCheck.Start();
    }

    /// Registra o manifesto e leva o usuário ao painel de novidades. Usado pelo botão "Buscar
    /// atualizações" (Configurações e bandeja), que é um pedido explícito — por isso navega direto
    /// em vez de só acender o selo.
    public void ShowPendingUpdate(UpdateManifest manifest)
    {
        PendingUpdate = manifest;
        _mainPanel?.SetUpdateAvailable(true);
        OpenMainPanel(PanelSection.Update);
    }

    private async Task CheckForUpdateAsync()
    {
        var manifest = await UpdateService.CheckAsync();
        if (manifest == null) return;

        var isNew = PendingUpdate?.Version != manifest.Version;
        PendingUpdate = manifest;

        // O selo aparece SEMPRE que há versão nova, mesmo adiada.
        _mainPanel?.SetUpdateAvailable(true);

        // Já o aviso (balão) respeita o "lembrar depois" — e só toca uma vez por versão detectada,
        // não a cada verificação de 6 horas.
        if (!isNew || UpdateSettings.IsSnoozed(manifest.Version)) return;

        ToastWindow.Show(string.IsNullOrWhiteSpace(manifest.Headline)
                ? $"A versão {manifest.Version} está disponível. Abra o app para ver o que mudou."
                : $"Versão {manifest.Version}: {manifest.Headline}",
            ToastKind.Info);
    }

    /// Não usa ShowDialog — o onboarding é uma janela comum, o usuário pode ignorá-la e continuar
    /// usando o app enquanto ela espera a primeira transcrição bem-sucedida.
    private void MaybeShowOnboarding()
    {
        if (OnboardingSettings.IsCompleted()) return;
        new OnboardingWindow().Show();
    }

    /// Resolve o tema efetivo (preferência do usuário + Windows quando "Sistema") e troca o
    /// dicionário mesclado em Application.Resources. Chamado no startup, sempre que a preferência
    /// muda em Configurações, e (se a preferência for Sistema) quando o Windows muda de tema.
    /// Janelas usam DynamicResource para os brushes de Theme.*.xaml, então a troca aparece nelas
    /// imediatamente, sem recriar nada.
    public void ApplyTheme()
    {
        var preference = ThemeSettings.Load();
        var useLight = preference switch
        {
            ThemePreference.Light => true,
            ThemePreference.Dark => false,
            _ => ThemeSettings.IsWindowsLightThemeActive(),
        };

        if (_appliedLightTheme == useLight) return; // evita reflow/flicker ao reaplicar o mesmo tema

        var themeDictionary = new ResourceDictionary
        {
            Source = new Uri(useLight ? "Theme.Light.xaml" : "Theme.Dark.xaml", UriKind.Relative)
        };
        var stylesDictionary = new ResourceDictionary { Source = new Uri("Styles.xaml", UriKind.Relative) };
        Resources.MergedDictionaries.Clear();
        Resources.MergedDictionaries.Add(themeDictionary);
        Resources.MergedDictionaries.Add(stylesDictionary);
        _appliedLightTheme = useLight;

        // O menu da bandeja é WinForms e não reage a DynamicResource — precisa ser repintado à mão
        // a cada troca, senão quem sai do escuro para o claro fica com o menu escuro para sempre.
        if (_trayMenu != null) TrayMenuTheme.Apply(_trayMenu);
    }

    /// SystemEvents dispara em thread própria, não na do Dispatcher — nunca tocar em
    /// Application.Resources direto aqui. Não filtramos por e.Category porque a categoria usada
    /// para notificar troca de tema claro/escuro varia entre versões do Windows; ApplyTheme já
    /// ignora chamadas que não mudam o tema resolvido, então um evento não relacionado é barato.
    private void OnSystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        Dispatcher.InvokeAsync(() =>
        {
            if (ThemeSettings.Load() == ThemePreference.System) ApplyTheme();
        });
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

        HasActiveHotkey = false;
        CurrentHotkeyLabel = "sem atalho disponível";
        _tray!.Text = "Aionix Scribe — sem atalho disponível";
        ToastWindow.Show("Todos os atalhos padrão já estão em uso por outros aplicativos. Abra Configurações para escolher outro.",
            ToastKind.Warning);
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
        HasActiveHotkey = true;
        return true;
    }

    private void NotifyActiveHotkey()
    {
        _tray!.Text = $"Aionix Scribe — {CurrentHotkeyLabel}";
        ToastWindow.Show($"Atalho ativo: {CurrentHotkeyLabel}", ToastKind.Info);
    }

    /// Chamado pela seção de Configurações quando o usuário captura um novo atalho. Em caso de conflito,
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
        HasActiveHotkey = true;
        HotkeySettings.SaveCustom(new HotkeyChoice(modifiers, vk, label, _hotkeyMode));
        _tray!.Text = $"Aionix Scribe — {CurrentHotkeyLabel}";
        error = null;
        return true;
    }

    /// Chamado pela seção de Configurações ao trocar entre Toggle e PushToTalk, mantendo o combo atual.
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
        ToastWindow.Show($"Erro inesperado: {e.Exception.Message}", ToastKind.Error);
        _state = AppState.Idle;
        e.Handled = true; // não derruba o app por um erro pontual — melhor continuar disponível
    }

    private void SetupTrayIcon()
    {
        _tray = new Forms.NotifyIcon
        {
            Icon = LoadTrayIcon(),
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
        menu.Items.Add("Buscar atualizações...", null, async (_, _) =>
        {
            // Verificação manual: ignora o "lembrar depois" e sempre dá uma resposta — quem clica
            // aqui está perguntando de propósito e merece saber que está atualizado.
            var manifest = await UpdateService.CheckAsync();
            if (manifest == null)
            {
                ToastWindow.Show($"Você já está na versão mais recente ({UpdateService.CurrentVersionDisplay}).", ToastKind.Info);
                return;
            }
            UpdateSettings.Clear();
            ShowPendingUpdate(manifest);
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Sair", null, (_, _) => Shutdown());
        _trayMenu = menu;
        TrayMenuTheme.Apply(menu);
        _tray.ContextMenuStrip = menu;
        _tray.DoubleClick += (_, _) => OpenMainPanel();

        RefreshPendingMenu();
    }

    /// Ícone real embutido como Resource (build action) em vez de caminho de arquivo, pra não
    /// depender de nenhum diretório existir ao lado do .exe em produção. Cai de volta pro ícone
    /// genérico do sistema só se o pack resource falhar por algum motivo inesperado.
    private static Drawing.Icon LoadTrayIcon()
    {
        try
        {
            using var stream = System.Windows.Application.GetResourceStream(new Uri("Assets/AionixScribe.ico", UriKind.Relative))?.Stream;
            if (stream != null) return new Drawing.Icon(stream);
        }
        catch
        {
            // ignora e cai no fallback abaixo
        }
        return Drawing.SystemIcons.Application;
    }

    public void OpenMainPanel(PanelSection section = PanelSection.Dictation)
    {
        if (_mainPanel == null || !_mainPanel.IsLoaded)
        {
            _mainPanel = new MainPanelWindow();
            _mainPanel.Closed += (_, _) => _mainPanel = null;
            // A verificação roda com a janela fechada — ao abrir, o selo precisa refletir o que já
            // foi descoberto, senão só apareceria depois da próxima checagem de 6 horas.
            _mainPanel.SetUpdateAvailable(PendingUpdate != null);
            _mainPanel.Navigate(section);
        }
        else
        {
            _mainPanel.Navigate(section); // já navega e dá Refresh na seção alvo
            if (_mainPanel.WindowState == WindowState.Minimized) _mainPanel.WindowState = WindowState.Normal;
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
            ToastWindow.Show(ex.Message, ToastKind.Warning);
            return;
        }
        catch (Exception ex)
        {
            DebugLog.Write($"StartListening: falha ao acessar microfone: {ex}");
            ToastWindow.Show($"Não foi possível acessar o microfone: {ex.Message}", ToastKind.Error);
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

        // Portão local antes de gastar rede, tokens e cota do usuário: um toque acidental no atalho
        // ou um push-to-talk mal encostado não deve virar uma chamada à IA. Pela regra do D006,
        // "nenhuma fala detectada" consome cota — então cortar aqui é o que evita o custo de verdade.
        if (!AudioRecorder.HasLikelySpeech(wav, out var skipReason))
        {
            DebugLog.Write($"StopAndProcessAsync: descartado sem enviar — {skipReason}");
            UpdateOverlay(OverlayState.Cancelled, "Nenhuma fala detectada");
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
            ToastWindow.Show("Não consegui transcrever depois de duas tentativas. Sua gravação foi preservada — use \"Reprocessar pendências\" no menu da bandeja quando a conexão voltar.",
                ToastKind.Warning);
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
                DictationSucceeded?.Invoke(result.Text);
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
            ToastWindow.Show("Ainda não consegui reprocessar essa gravação. Ela continua preservada.", ToastKind.Warning);
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

    // Histórico e Configurações deixaram de ser janelas próprias: são seções do shell (D020).
    // Os itens da bandeja continuam existindo e apenas abrem a janela já na seção certa.
    public void OpenHistory() => OpenMainPanel(PanelSection.History);

    public void OpenSettings() => OpenMainPanel(PanelSection.Settings);

    protected override void OnExit(ExitEventArgs e)
    {
        SystemEvents.UserPreferenceChanged -= OnSystemPreferenceChanged; // evento estático — sem isso, vaza e pode estourar no shutdown
        _hotkey?.Dispose();
        _pushToTalkHook?.Dispose();
        _recorder.Dispose();
        _backend.Dispose();
        _tray?.Dispose();
        base.OnExit(e);
    }
}
