using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace AionixScribe;

public sealed class RecentEntryViewModel
{
    public required string Text { get; init; }
    public required string TimestampDisplay { get; init; }
}

public partial class DictationSection : System.Windows.Controls.UserControl
{
    public DictationSection()
    {
        InitializeComponent();
        Refresh();
    }

    /// Chamado na construção e toda vez que a seção volta a ficar visível — a instância é
    /// reaproveitada pelo shell, então sem isso o estado do microfone e a lista de recentes
    /// congelariam no que valia quando a janela abriu pela primeira vez.
    public void Refresh()
    {
        var app = (App)System.Windows.Application.Current;
        HotkeyText.Text = app.CurrentHotkeyLabel;

        var hasMic = WaveInEvent.DeviceCount > 0;
        MicStatusText.Text = hasMic ? "Microfone pronto" : "Nenhum microfone detectado";
        MicStatusDot.Fill = (System.Windows.Media.Brush)FindResource(hasMic ? "SuccessBrush" : "ErrorBrush");

        var all = HistoryStore.List();
        TotalCountText.Text = all.Count.ToString();

        var recent = all.Take(5)
            .Select(e => new RecentEntryViewModel { Text = e.Text, TimestampDisplay = e.TimestampUtc.ToLocalTime().ToString("dd/MM HH:mm") })
            .ToList();

        RecentList.ItemsSource = recent;
        var isEmpty = recent.Count == 0;
        RecentList.Visibility = isEmpty ? Visibility.Collapsed : Visibility.Visible;
        EmptyRecentText.Visibility = isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }
}
