using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NAudio.Wave;

namespace AionixScribe;

public sealed record MicrophoneOption(int Index, string Name);

public partial class SettingsWindow : Window
{
    private bool _capturing;
    private bool _initializingMode;
    private bool _initializingMicrophone;
    private bool _initializingStartup;
    private bool _initializingTheme;

    public SettingsWindow()
    {
        InitializeComponent();
        RefreshCurrentHotkeyDisplay();
        RefreshModeSelection();
        RefreshMicrophoneList();
        RefreshStartupState();
        RefreshThemeSelection();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // Reaplicar "Sistema" toda vez que a janela abre cobre o caso de o usuário ter trocado o tema
    // do Windows enquanto o app não estava com Configurações aberta — App já observa
    // SystemEvents.UserPreferenceChanged em tempo real, então isso aqui é só reforço na abertura.
    private void RefreshThemeSelection()
    {
        _initializingTheme = true;
        var preference = ThemeSettings.Load();
        SystemThemeRadio.IsChecked = preference == ThemePreference.System;
        LightThemeRadio.IsChecked = preference == ThemePreference.Light;
        DarkThemeRadio.IsChecked = preference == ThemePreference.Dark;
        _initializingTheme = false;
    }

    private void OnThemeChanged(object sender, RoutedEventArgs e)
    {
        if (_initializingTheme) return;

        var preference = ReferenceEquals(sender, LightThemeRadio) ? ThemePreference.Light
            : ReferenceEquals(sender, DarkThemeRadio) ? ThemePreference.Dark
            : ThemePreference.System;

        ThemeSettings.Save(preference);
        ((App)System.Windows.Application.Current).ApplyTheme();
    }

    private void RefreshMicrophoneList()
    {
        _initializingMicrophone = true;

        var options = new List<MicrophoneOption> { new(AudioSettings.SystemDefaultDeviceIndex, "Padrão do sistema") };
        for (var i = 0; i < WaveInEvent.DeviceCount; i++)
        {
            options.Add(new MicrophoneOption(i, WaveInEvent.GetCapabilities(i).ProductName));
        }
        MicrophoneCombo.ItemsSource = options;

        var savedIndex = AudioSettings.LoadDeviceIndex();
        var selected = options.FirstOrDefault(o => o.Index == savedIndex) ?? options[0];
        MicrophoneCombo.SelectedItem = selected;

        _initializingMicrophone = false;
    }

    private void OnMicrophoneChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initializingMicrophone) return;
        if (MicrophoneCombo.SelectedItem is not MicrophoneOption option) return;

        AudioSettings.SaveDeviceIndex(option.Index);
    }

    private void RefreshStartupState()
    {
        _initializingStartup = true;
        StartWithWindowsCheck.IsChecked = StartupSettings.IsEnabled();
        _initializingStartup = false;
    }

    private void OnStartWithWindowsChanged(object sender, RoutedEventArgs e)
    {
        if (_initializingStartup) return;

        try
        {
            StartupSettings.SetEnabled(StartWithWindowsCheck.IsChecked == true);
        }
        catch (Exception ex)
        {
            ShowError($"Não foi possível atualizar a inicialização automática: {ex.Message}");
            RefreshStartupState();
        }
    }

    private void OnOpenDataFolderClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "AionixScribe");
            Directory.CreateDirectory(folder);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowError($"Não foi possível abrir a pasta de dados: {ex.Message}");
        }
    }

    private void RefreshCurrentHotkeyDisplay()
    {
        CurrentHotkeyText.Text = ((App)System.Windows.Application.Current).CurrentHotkeyLabel;
    }

    private void RefreshModeSelection()
    {
        var mode = ((App)System.Windows.Application.Current).CurrentHotkeyMode;
        // Marca o RadioButton sem disparar OnModeChanged (que reaplicaria o mesmo modo no App).
        _initializingMode = true;
        ToggleModeRadio.IsChecked = mode == HotkeyMode.Toggle;
        PushToTalkModeRadio.IsChecked = mode == HotkeyMode.PushToTalk;
        _initializingMode = false;
        UpdateModeDescription(mode);
    }

    private void UpdateModeDescription(HotkeyMode mode)
    {
        ModeDescriptionText.Text = mode == HotkeyMode.Toggle
            ? "Pressione o atalho uma vez para começar a ditar, e de novo para parar."
            : "Segure o atalho para ditar, solte para parar.";
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (_initializingMode) return;

        var mode = ReferenceEquals(sender, PushToTalkModeRadio) ? HotkeyMode.PushToTalk : HotkeyMode.Toggle;
        var app = (App)System.Windows.Application.Current;
        if (app.TryChangeMode(mode, out var error))
        {
            UpdateModeDescription(mode);
            StatusText.Visibility = Visibility.Collapsed;
        }
        else
        {
            ShowError(error ?? "Não foi possível trocar o modo de ativação.");
            RefreshModeSelection();
        }
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
        RefreshModeSelection();
        StatusText.Visibility = Visibility.Collapsed;
    }

    private void OnCloseClicked(object sender, RoutedEventArgs e) => Close();
}
