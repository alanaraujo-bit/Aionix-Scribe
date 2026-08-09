using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AionixScribe;

public static class Motion
{
    public static void PlayEntrance(FrameworkElement root)
    {
        var slide = new TranslateTransform(0, 12);
        root.RenderTransform = slide;
        root.Opacity = 0;

        var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };
        var slideUp = new DoubleAnimation(12, 0, TimeSpan.FromMilliseconds(400))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        root.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        slide.BeginAnimation(TranslateTransform.YProperty, slideUp);
    }
}
