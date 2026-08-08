using System.Windows;

namespace AionixScribe;

public partial class OnboardingWindow : Window
{
    private readonly App _app;

    public OnboardingWindow()
    {
        InitializeComponent();
        _app = (App)System.Windows.Application.Current;

        ApplyHotkeyState();
        Activated += (_, _) => ApplyHotkeyState();
        _app.DictationSucceeded += OnDictationSucceeded;
    }

    /// Reaplicado em Activated (não só no construtor) para o caso comum deste cenário: usuário
    /// sem atalho ativo clica "Abrir Configurações", resolve o conflito lá, e volta pra essa
    /// janela — que precisa deixar de mostrar o aviso de "sem atalho" sozinha.
    private void ApplyHotkeyState()
    {
        HotkeyRun.Text = _app.CurrentHotkeyLabel;
        HotkeyRunSuccess.Text = _app.CurrentHotkeyLabel;

        var hasHotkey = _app.HasActiveHotkey;
        HasHotkeyText.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        HasHotkeyDetailText.Visibility = hasHotkey ? Visibility.Visible : Visibility.Collapsed;
        NoHotkeyText.Visibility = hasHotkey ? Visibility.Collapsed : Visibility.Visible;
        OpenSettingsButton.Visibility = hasHotkey ? Visibility.Collapsed : Visibility.Visible;
        WaitingDot.Fill = (System.Windows.Media.Brush)FindResource(hasHotkey ? "SuccessBrush" : "ErrorBrush");
        WaitingText.Text = hasHotkey ? "Aguardando seu primeiro ditado..." : "Sem atalho ativo — configure um para poder ditar";
    }

    private void OnDictationSucceeded(string text)
    {
        WelcomePanel.Visibility = Visibility.Collapsed;
        SuccessPanel.Visibility = Visibility.Visible;
    }

    private void OnOpenSettingsClicked(object sender, RoutedEventArgs e) => _app.OpenSettings();

    private void OnSkipClicked(object sender, RoutedEventArgs e) => Close();

    private void OnFinishClicked(object sender, RoutedEventArgs e) => Close();

    /// Cobre os três jeitos de sair (Pular, Concluir, X da janela) com uma única marcação —
    /// fechar sem interagir também conta como "visto", senão o onboarding reapareceria a cada
    /// startup só porque o usuário nunca clicou em nada.
    private void OnWindowClosed(object? sender, EventArgs e)
    {
        _app.DictationSucceeded -= OnDictationSucceeded;
        OnboardingSettings.MarkCompleted();
    }
}
