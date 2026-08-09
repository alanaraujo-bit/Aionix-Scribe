using System.Windows;
using NAudio.Wave;

namespace AionixScribe;

public sealed class RecentEntryViewModel
{
    public required string Text { get; init; }
    public required string TimestampDisplay { get; init; }
}

public partial class MainPanelWindow : Window
{
    public MainPanelWindow()
    {
        InitializeComponent();
        Refresh();
    }

    public void Refresh()
    {
        var app = (App)System.Windows.Application.Current;
        HotkeyText.Text = app.CurrentHotkeyLabel;

        var hasMic = WaveInEvent.DeviceCount > 0;
        MicStatusText.Text = hasMic ? "Microfone pronto" : "Nenhum microfone detectado";
        MicStatusDot.Fill = (System.Windows.Media.Brush)FindResource(hasMic ? "SuccessBrush" : "ErrorBrush");

        var recent = HistoryStore.List().Take(5)
            .Select(e => new RecentEntryViewModel { Text = e.Text, TimestampDisplay = e.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm") })
            .ToList();

        RecentList.ItemsSource = recent;
        var isEmpty = recent.Count == 0;
        RecentList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        EmptyRecentText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnHistoryClicked(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).OpenHistory();
    private void OnSettingsClicked(object sender, RoutedEventArgs e) => ((App)System.Windows.Application.Current).OpenSettings();

    private void OnMinimizeClicked(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();

    private void OnWindowLoaded(object sender, RoutedEventArgs e) => Motion.PlayEntrance(RootContent);
}
