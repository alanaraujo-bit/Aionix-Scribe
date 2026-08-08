using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace AionixScribe;

public enum OverlayState { Listening, Processing, Done, Error, Cancelled }

/// Overlay always-on-top que nunca rouba foco da janela ativa — validado no spike via
/// WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW | WS_EX_LAYERED (ver DECISIONS.md D003).
public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => PositionBottomCenter();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
    }

    private void PositionBottomCenter()
    {
        Left = (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
        Top = SystemParameters.PrimaryScreenHeight - ActualHeight - 90;
    }

    public void SetState(OverlayState state, string? detail = null)
    {
        (StatusText.Text, StatusDot.Fill) = state switch
        {
            OverlayState.Listening => ("Ouvindo...", Brush("#4ADE80")),
            OverlayState.Processing => ("Processando...", Brush("#FBBF24")),
            OverlayState.Done => (detail ?? "Concluído", Brush("#4ADE80")),
            OverlayState.Error => (detail ?? "Erro ao processar", Brush("#F87171")),
            OverlayState.Cancelled => ("Cancelado", Brush("#9CA3AF")),
            _ => (StatusText.Text, StatusDot.Fill),
        };
        Dispatcher.InvokeAsync(PositionBottomCenter);
    }

    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
}
