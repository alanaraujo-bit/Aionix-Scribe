using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AionixScribe;

/// <summary>
/// Scroll suave e por pixel para qualquer ScrollViewer (direto ou dentro de um ListBox
/// virtualizado, como o Histórico). Sem isto o scroll do mouse tem duas formas de ficar
/// engessado: (1) num ListBox virtualizado, o WPF por padrão rola "por item" — como os itens do
/// histórico têm alturas diferentes (texto ditado mais curto ou mais longo), cada notch do mouse
/// anda uma distância diferente, e às vezes salta o item inteiro; (2) mesmo num ScrollViewer
/// simples, o scroll padrão do WPF não é animado, então cada notch é um salto seco e fixo.
/// Aqui cada notch do mouse move o ScrollViewer suavemente até o novo offset, com easing.
/// </summary>
public static class SmoothScroll
{
    // Pixels por unidade de delta do mouse (o delta padrão de um notch é 120 => ~110px).
    private const double PixelsPerWheelDelta = 0.92;
    private static readonly TimeSpan Duration = TimeSpan.FromMilliseconds(280);

    public static readonly DependencyProperty IsEnabledProperty = DependencyProperty.RegisterAttached(
        "IsEnabled", typeof(bool), typeof(SmoothScroll), new PropertyMetadata(false, OnIsEnabledChanged));

    public static void SetIsEnabled(DependencyObject element, bool value) => element.SetValue(IsEnabledProperty, value);
    public static bool GetIsEnabled(DependencyObject element) => (bool)element.GetValue(IsEnabledProperty);

    // Proxy animável: ScrollViewer.VerticalOffset não é uma DependencyProperty (só um getter),
    // então animamos esta propriedade auxiliar e, a cada frame, aplicamos o valor via
    // ScrollToVerticalOffset.
    private static readonly DependencyProperty AnimatedOffsetProperty = DependencyProperty.RegisterAttached(
        "AnimatedOffset", typeof(double), typeof(SmoothScroll), new PropertyMetadata(0.0, OnAnimatedOffsetChanged));

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element) return;

        if ((bool)e.NewValue)
            element.PreviewMouseWheel += OnPreviewMouseWheel;
        else
            element.PreviewMouseWheel -= OnPreviewMouseWheel;
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var host = (DependencyObject)sender;
        var scrollViewer = host as ScrollViewer ?? FindDescendantScrollViewer(host);
        if (scrollViewer is null || scrollViewer.ScrollableHeight <= 0) return;

        e.Handled = true;

        var target = Clamp(scrollViewer.VerticalOffset - e.Delta * PixelsPerWheelDelta, 0, scrollViewer.ScrollableHeight);
        var animation = new DoubleAnimation(scrollViewer.VerticalOffset, target, Duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };
        scrollViewer.BeginAnimation(AnimatedOffsetProperty, animation);
    }

    private static void OnAnimatedOffsetChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ScrollViewer scrollViewer)
            scrollViewer.ScrollToVerticalOffset((double)e.NewValue);
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject parent)
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is ScrollViewer sv) return sv;

            var nested = FindDescendantScrollViewer(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);
}
