using System.Windows;
using System.Windows.Controls;
using WpfClipboard = System.Windows.Clipboard;

namespace AionixScribe;

public sealed class HistoryItemViewModel
{
    public required string Id { get; init; }
    public required string Text { get; init; }
    public required string TimestampDisplay { get; init; }
}

public partial class HistorySection : System.Windows.Controls.UserControl
{
    public HistorySection()
    {
        InitializeComponent();
        Refresh();
    }

    /// Público porque o shell reaproveita a mesma instância a cada navegação — recarregar só no
    /// construtor deixaria a lista congelada no estado de quando a janela abriu, e ditados novos
    /// (que acontecem com a janela fechada, pelo atalho global) nunca apareceriam.
    public void Refresh()
    {
        var entries = HistoryStore.List();
        EntriesList.ItemsSource = entries.Select(e => new HistoryItemViewModel
        {
            Id = e.Id,
            Text = e.Text,
            TimestampDisplay = e.TimestampUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
        }).ToList();

        var isEmpty = entries.Count == 0;
        EntriesList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        EmptyStateText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        var id = (string)((System.Windows.Controls.Button)sender).Tag;
        var entry = HistoryStore.List().FirstOrDefault(x => x.Id == id);
        if (entry != null) WpfClipboard.SetText(entry.Text);
    }

    private void OnDeleteClicked(object sender, RoutedEventArgs e)
    {
        var id = (string)((System.Windows.Controls.Button)sender).Tag;
        HistoryStore.Delete(id);
        Refresh();
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (HistoryStore.List().Count == 0) return;

        // MessageBox precisa da Window dona, não do UserControl — sem isso a caixa pode abrir atrás
        // da janela principal e travar a interação sem o usuário entender o porquê.
        var result = System.Windows.MessageBox.Show(
            Window.GetWindow(this),
            "Apagar todo o histórico de ditados? Essa ação não pode ser desfeita.",
            "Limpar histórico",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            HistoryStore.Clear();
            Refresh();
        }
    }
}
