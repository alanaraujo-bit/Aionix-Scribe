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

        // Diálogo próprio, não MessageBox: o do Windows não aceita personalização nenhuma e abria
        // uma caixa cinza do sistema no meio de uma interface escura (ver D023).
        var confirmed = ConfirmDialog.Ask(
            Window.GetWindow(this),
            "Limpar histórico",
            "Todos os seus ditados guardados nesta máquina serão apagados. Essa ação não pode ser desfeita.",
            confirmLabel: "Apagar tudo");

        if (confirmed)
        {
            HistoryStore.Clear();
            Refresh();
        }
    }
}
