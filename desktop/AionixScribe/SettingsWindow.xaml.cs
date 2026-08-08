using System.Windows;
using System.Windows.Input;

namespace AionixScribe;

public partial class SettingsWindow : Window
{
    private bool _capturing;

    public SettingsWindow()
    {
        InitializeComponent();
        RefreshCurrentHotkeyDisplay();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private void RefreshCurrentHotkeyDisplay()
    {
        CurrentHotkeyText.Text = ((App)System.Windows.Application.Current).CurrentHotkeyLabel;
    }

    private void OnChangeClicked(object sender, RoutedEventArgs e)
    {
        _capturing = true;
        ChangeButton.Content = "Pressione o novo atalho...";
        StatusText.Visibility = Visibility.Collapsed;
        Focus();
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_capturing) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;

        // Ignora a própria tecla modificadora sendo pressionada isoladamente — esperamos o
        // modificador + a tecla "principal" juntos.
        if (IsModifierKey(key)) return;

        e.Handled = true;
        _capturing = false;
        ChangeButton.Content = "Alterar atalho";

        var modifiers = Keyboard.Modifiers;
        if (modifiers == ModifierKeys.None)
        {
            ShowError("O atalho precisa incluir pelo menos um modificador (Ctrl, Alt, Shift ou Win).");
            return;
        }

        uint winModifiers = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) winModifiers |= Native.MOD_CONTROL;
        if (modifiers.HasFlag(ModifierKeys.Alt)) winModifiers |= Native.MOD_ALT;
        if (modifiers.HasFlag(ModifierKeys.Shift)) winModifiers |= Native.MOD_SHIFT;
        if (modifiers.HasFlag(ModifierKeys.Windows)) winModifiers |= Native.MOD_WIN;

        var vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        var label = DescribeCombo(modifiers, key);

        var app = (App)System.Windows.Application.Current;
        if (app.TryChangeHotkey(winModifiers, vk, label, out var error))
        {
            RefreshCurrentHotkeyDisplay();
            StatusText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowError(error ?? "Não foi possível registrar esse atalho.");
        }
    }

    private static bool IsModifierKey(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt
        or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;

    private static string DescribeCombo(ModifierKeys modifiers, Key key)
    {
        var parts = new List<string>();
        if (modifiers.HasFlag(ModifierKeys.Control)) parts.Add("Ctrl");
        if (modifiers.HasFlag(ModifierKeys.Alt)) parts.Add("Alt");
        if (modifiers.HasFlag(ModifierKeys.Shift)) parts.Add("Shift");
        if (modifiers.HasFlag(ModifierKeys.Windows)) parts.Add("Win");
        parts.Add(key.ToString());
        return string.Join("+", parts);
    }

    private void ShowError(string message)
    {
        StatusText.Text = message;
        StatusText.Visibility = Visibility.Visible;
    }

    private void OnResetClicked(object sender, RoutedEventArgs e)
    {
        HotkeySettings.ClearCustom();
        var app = (App)System.Windows.Application.Current;
        app.ResetHotkeyToAuto();
        RefreshCurrentHotkeyDisplay();
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
