using System.Windows.Interop;

namespace AionixScribe;

public sealed class HotkeyConflictException : Exception
{
    public HotkeyConflictException(string combo) : base($"O atalho {combo} já está em uso por outro aplicativo.") { }
}

/// Toggle mode: primeira pressionada inicia, segunda encerra. RegisterHotKey não fornece evento
/// de "soltar tecla", então push-to-talk (segurar/soltar) exigiria um low-level keyboard hook
/// (WH_KEYBOARD_LL) em vez disso — ver DECISIONS.md D0xx. Fica para uma iteração seguinte.
public sealed class HotkeyManager : IDisposable
{
    private const int HotkeyId = 1;
    private readonly HwndSource _source;
    public event Action? Triggered;

    public HotkeyManager(uint modifiers, uint vk)
    {
        var parameters = new HwndSourceParameters("AionixScribeHotkeyWindow") { Width = 0, Height = 0, WindowStyle = 0 };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        var registered = Native.RegisterHotKey(_source.Handle, HotkeyId, modifiers | Native.MOD_NOREPEAT, vk);
        if (!registered)
        {
            _source.RemoveHook(WndProc);
            _source.Dispose();
            throw new HotkeyConflictException(DescribeCombo(modifiers, vk));
        }
    }

    private static string DescribeCombo(uint modifiers, uint vk)
    {
        var parts = new List<string>();
        if ((modifiers & Native.MOD_CONTROL) != 0) parts.Add("Ctrl");
        if ((modifiers & Native.MOD_ALT) != 0) parts.Add("Alt");
        if ((modifiers & Native.MOD_SHIFT) != 0) parts.Add("Shift");
        if ((modifiers & Native.MOD_WIN) != 0) parts.Add("Win");
        parts.Add($"VK_{vk:X2}");
        return string.Join("+", parts);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            Triggered?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Native.UnregisterHotKey(_source.Handle, HotkeyId);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}
