using System.Windows;

namespace AionixScribe;

/// Seção exibida na área de conteúdo do shell. A bandeja abre a janela já numa seção específica
/// (ex.: "Configurações..." no menu), em vez de abrir uma janela separada por assunto.
public enum PanelSection
{
    Dictation,
    History,
    Usage,
    Settings,
    Update,
}

/// Shell de janela única: barra lateral de navegação + área de conteúdo. Substituiu o modelo
/// anterior de três janelas independentes (painel, histórico, configurações) — ver DECISIONS.md D020.
/// As seções são instanciadas sob demanda e REAPROVEITADAS: por isso cada uma expõe Refresh(),
/// chamado ao navegar, senão a seção mostraria dados congelados da primeira vez que foi aberta.
public partial class MainPanelWindow : Window
{
    private DictationSection? _dictation;
    private HistorySection? _history;
    private UsageSection? _usage;
    private SettingsSection? _settings;
    private UpdateSection? _update;

    public MainPanelWindow()
    {
        InitializeComponent();
        Navigate(PanelSection.Dictation);
    }

    public void Navigate(PanelSection section)
    {
        // Atualização não é item da barra lateral (só existe quando há versão nova) — vai direto
        // para o conteúdo, sem passar pelo grupo de RadioButtons.
        if (section == PanelSection.Update)
        {
            ShowSection(section);
            return;
        }

        // Marcar o RadioButton dispara OnNavChanged, que faz a troca de conteúdo de verdade —
        // assim navegação por código e por clique passam pelo mesmo caminho.
        var target = section switch
        {
            PanelSection.History => NavHistory,
            PanelSection.Usage => NavUsage,
            PanelSection.Settings => NavSettings,
            _ => NavDictation,
        };

        if (target.IsChecked == true) ShowSection(section); // já marcado: Checked não dispara de novo
        else target.IsChecked = true;
    }

    /// Mostra/esconde o selo de atualização. Chamado pelo App quando a verificação encontra versão
    /// nova (ou quando a janela abre e já havia uma pendente).
    public void SetUpdateAvailable(bool available)
    {
        UpdateBadge.Visibility = available ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnUpdateBadgeClicked(object sender, RoutedEventArgs e) => Navigate(PanelSection.Update);

    private void OnNavChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        if (ReferenceEquals(sender, NavHistory)) ShowSection(PanelSection.History);
        else if (ReferenceEquals(sender, NavUsage)) ShowSection(PanelSection.Usage);
        else if (ReferenceEquals(sender, NavSettings)) ShowSection(PanelSection.Settings);
        else ShowSection(PanelSection.Dictation);
    }

    private void ShowSection(PanelSection section)
    {
        switch (section)
        {
            case PanelSection.History:
                _history ??= new HistorySection();
                _history.Refresh();
                SectionHost.Content = _history;
                break;

            case PanelSection.Usage:
                _usage ??= new UsageSection();
                _usage.Refresh();
                SectionHost.Content = _usage;
                break;

            case PanelSection.Settings:
                _settings ??= new SettingsSection();
                _settings.Refresh();
                SectionHost.Content = _settings;
                break;

            case PanelSection.Update:
                var manifest = ((App)System.Windows.Application.Current).PendingUpdate;
                if (manifest == null) { ShowSection(PanelSection.Dictation); return; }
                if (_update == null)
                {
                    _update = new UpdateSection();
                    // Adiar volta para o Ditado; o selo continua lá, então o caminho de volta
                    // para este painel nunca some.
                    _update.Dismissed += () => Navigate(PanelSection.Dictation);
                }
                _update.Show(manifest);
                SectionHost.Content = _update;
                break;

            default:
                _dictation ??= new DictationSection();
                _dictation.Refresh();
                SectionHost.Content = _dictation;
                break;
        }

        RefreshSidebar();
    }

    /// Reaplica o estado que a janela mostra fora das seções (atalho no rodapé da lateral) e
    /// atualiza a seção visível. Chamado pelo App quando a janela já está aberta.
    public void Refresh()
    {
        RefreshSidebar();
        (SectionHost.Content as DictationSection)?.Refresh();
        (SectionHost.Content as HistorySection)?.Refresh();
        (SectionHost.Content as UsageSection)?.Refresh();
        (SectionHost.Content as SettingsSection)?.Refresh();
    }

    private void RefreshSidebar()
    {
        SidebarHotkeyRun.Text = ((App)System.Windows.Application.Current).CurrentHotkeyLabel;
    }

    private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeClicked(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnWindowLoaded(object sender, RoutedEventArgs e)
    {
        RefreshSidebar();
        Motion.PlayEntrance(RootContent);
    }
}
