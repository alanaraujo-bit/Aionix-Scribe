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

public partial class HistoryWindow : Window
{
    public HistoryWindow()
    {
        InitializeComponent();
        Reload();
    }

    private void Reload()
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
        Reload();
    }

    private void OnClearAllClicked(object sender, RoutedEventArgs e)
    {
        if (HistoryStore.List().Count == 0) return;

        var result = System.Windows.MessageBox.Show(
            this,
            "Apagar todo o histórico de ditados? Essa ação não pode ser desfeita.",
            "Limpar histórico",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result == System.Windows.MessageBoxResult.Yes)
        {
            HistoryStore.Clear();
            Reload();
        }
    }
}
