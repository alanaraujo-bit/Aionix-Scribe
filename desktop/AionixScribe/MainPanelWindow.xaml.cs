using System.Windows;

namespace AionixScribe;

/// Seção exibida na área de conteúdo do shell. A bandeja abre a janela já numa seção específica
/// (ex.: "Configurações..." no menu), em vez de abrir uma janela separada por assunto.
public enum PanelSection
{
    Dictation,
    History,
    Settings,
}

/// Shell de janela única: barra lateral de navegação + área de conteúdo. Substituiu o modelo
/// anterior de três janelas independentes (painel, histórico, configurações) — ver DECISIONS.md D020.
/// As seções são instanciadas sob demanda e REAPROVEITADAS: por isso cada uma expõe Refresh(),
/// chamado ao navegar, senão a seção mostraria dados congelados da primeira vez que foi aberta.
public partial class MainPanelWindow : Window
{
    private DictationSection? _dictation;
    private HistorySection? _history;
    private SettingsSection? _settings;

    public MainPanelWindow()
    {
        InitializeComponent();
        Navigate(PanelSection.Dictation);
    }

    public void Navigate(PanelSection section)
    {
        // Marcar o RadioButton dispara OnNavChanged, que faz a troca de conteúdo de verdade —
        // assim navegação por código e por clique passam pelo mesmo caminho.
        var target = section switch
        {
            PanelSection.History => NavHistory,
            PanelSection.Settings => NavSettings,
            _ => NavDictation,
        };

        if (target.IsChecked == true) ShowSection(section); // já marcado: Checked não dispara de novo
        else target.IsChecked = true;
    }

    private void OnNavChanged(object sender, RoutedEventArgs e)
    {
        if (!IsInitialized) return;

        if (ReferenceEquals(sender, NavHistory)) ShowSection(PanelSection.History);
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

            case PanelSection.Settings:
                _settings ??= new SettingsSection();
                _settings.Refresh();
                SectionHost.Content = _settings;
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
