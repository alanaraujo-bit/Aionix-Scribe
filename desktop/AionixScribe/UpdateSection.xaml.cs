using System.Windows;

namespace AionixScribe;

public partial class UpdateSection : System.Windows.Controls.UserControl
{
    private UpdateManifest? _manifest;

    /// Disparado quando o usuário escolhe adiar — o shell usa para voltar à seção anterior.
    public event Action? Dismissed;

    public UpdateSection()
    {
        InitializeComponent();
    }

    public void Show(UpdateManifest manifest)
    {
        _manifest = manifest;

        VersionBadgeText.Text = $"versão {manifest.Version}";
        ReleasedAtText.Text = string.IsNullOrWhiteSpace(manifest.ReleasedAt) ? "" : $"publicada em {manifest.ReleasedAt}";

        if (!string.IsNullOrWhiteSpace(manifest.Headline)) HeadlineText.Text = manifest.Headline;

        SummaryText.Text = string.IsNullOrWhiteSpace(manifest.Summary)
            ? $"Você está na versão {UpdateService.CurrentVersionDisplay}."
            : manifest.Summary;

        var highlights = manifest.Highlights ?? Array.Empty<UpdateHighlight>();
        HighlightsList.ItemsSource = highlights;
        HighlightsList.Visibility = highlights.Length == 0 ? Visibility.Collapsed : Visibility.Visible;

        // Voltar ao estado inicial: a seção é reaproveitada, então uma tentativa que falhou antes
        // não pode deixar o erro e o progresso na tela quando o painel abre de novo.
        ErrorText.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Collapsed;
        ActionsPanel.Visibility = Visibility.Visible;
        UpdateButton.IsEnabled = true;
        ProgressFill.Width = 0;
    }

    private async void OnUpdateClicked(object sender, RoutedEventArgs e)
    {
        if (_manifest == null) return;

        ErrorText.Visibility = Visibility.Collapsed;
        ActionsPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;

        var progress = new Progress<double>(fraction =>
        {
            // A trilha é o pai do preenchimento; largura em pixels em vez de porcentagem porque um
            // Border não tem "valor" como uma ProgressBar.
            if (ProgressFill.Parent is FrameworkElement track && track.ActualWidth > 0)
            {
                ProgressFill.Width = track.ActualWidth * Math.Clamp(fraction, 0, 1);
            }
            ProgressText.Text = $"Baixando a atualização... {fraction * 100:0}%";
        });

        try
        {
            await UpdateService.DownloadAndInstallAsync(_manifest, progress);
            ProgressText.Text = "Instalando...";
            // Daqui em diante quem manda é o instalador: ele fecha este app e o reabre no fim.
        }
        catch (Exception ex)
        {
            DebugLog.Write($"update: instalação falhou: {ex}");
            ProgressPanel.Visibility = Visibility.Collapsed;
            ActionsPanel.Visibility = Visibility.Visible;
            ErrorText.Text = ex is InvalidOperationException
                ? ex.Message
                : "Não consegui baixar a atualização. Verifique sua conexão e tente de novo.";
            ErrorText.Visibility = Visibility.Visible;
        }
    }

    private void OnLaterClicked(object sender, RoutedEventArgs e)
    {
        if (_manifest != null) UpdateSettings.Snooze(_manifest.Version, TimeSpan.FromHours(24));
        Dismissed?.Invoke();
    }
}
