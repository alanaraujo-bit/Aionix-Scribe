using System.Runtime.InteropServices;

namespace AionixScribe;

public sealed class PushToTalkHookException : Exception
{
    public PushToTalkHookException() : base("Não foi possível instalar o hook de teclado para push-to-talk.") { }
}

/// WH_KEYBOARD_LL entrega os VKs específicos de lado para os modificadores (VK_LCONTROL/VK_RCONTROL
/// etc.), nunca o genérico VK_CONTROL/VK_MENU/VK_SHIFT — por isso o rastreamento abaixo trata os
/// dois lados de cada modificador. Assume-se (como no combo de toggle) que os modificadores são
/// pressionados antes da tecla principal; soltar/pressionar fora dessa ordem não ativa o combo,
/// espelhando o comportamento que RegisterHotKey já tem no modo toggle.
public sealed class PushToTalkHook : IDisposable
{
    private const int VK_LSHIFT = 0xA0, VK_RSHIFT = 0xA1;
    private const int VK_LCONTROL = 0xA2, VK_RCONTROL = 0xA3;
    private const int VK_LMENU = 0xA4, VK_RMENU = 0xA5;
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C;

    private readonly uint _modifiers;
    private readonly int _vk;
    private readonly HashSet<int> _keysDown = new();
    // Precisa ficar num campo: se for uma lambda/local temporária o GC pode coletar o delegate
    // enquanto o hook nativo ainda referencia o ponteiro, derrubando o callback silenciosamente.
    private readonly Native.LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle;
    private bool _active;
    private bool _disposed;

    public event Action? Pressed;
    public event Action? Released;

    public PushToTalkHook(uint modifiers, uint vk)
    {
        _modifiers = modifiers;
        _vk = (int)vk;
        _proc = HookCallback;
        _hookHandle = Native.SetWindowsHookEx(Native.WH_KEYBOARD_LL, _proc, Native.GetModuleHandle(null), 0);
        if (_hookHandle == IntPtr.Zero)
            throw new PushToTalkHookException();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0)
            return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        var data = Marshal.PtrToStructure<Native.KBDLLHOOKSTRUCT>(lParam);
        var vk = (int)data.vkCode;
        var msg = wParam.ToInt32();

        if (msg is Native.WM_KEYDOWN or Native.WM_SYSKEYDOWN)
        {
            _keysDown.Add(vk);
            if (!_active && vk == _vk && AllModifiersDown())
            {
                _active = true;
                Pressed?.Invoke();
                return new IntPtr(1);
            }
            // Suprime tanto o key-repeat do próprio combo quanto repeats de modificadores segurados.
            if (_active && (vk == _vk || IsRequiredModifier(vk)))
                return new IntPtr(1);
        }
        else if (msg is Native.WM_KEYUP or Native.WM_SYSKEYUP)
        {
            _keysDown.Remove(vk);
            if (_active && (vk == _vk || IsRequiredModifier(vk)))
            {
                _active = false;
                Released?.Invoke();
                return new IntPtr(1);
            }
        }

        return Native.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private bool AllModifiersDown()
    {
        if ((_modifiers & Native.MOD_CONTROL) != 0 && !IsDown(VK_LCONTROL, VK_RCONTROL)) return false;
        if ((_modifiers & Native.MOD_ALT) != 0 && !IsDown(VK_LMENU, VK_RMENU)) return false;
        if ((_modifiers & Native.MOD_SHIFT) != 0 && !IsDown(VK_LSHIFT, VK_RSHIFT)) return false;
        if ((_modifiers & Native.MOD_WIN) != 0 && !IsDown(VK_LWIN, VK_RWIN)) return false;
        return true;
    }

    private bool IsDown(int a, int b) => _keysDown.Contains(a) || _keysDown.Contains(b);

    private bool IsRequiredModifier(int vk)
    {
        if ((_modifiers & Native.MOD_CONTROL) != 0 && vk is VK_LCONTROL or VK_RCONTROL) return true;
        if ((_modifiers & Native.MOD_ALT) != 0 && vk is VK_LMENU or VK_RMENU) return true;
        if ((_modifiers & Native.MOD_SHIFT) != 0 && vk is VK_LSHIFT or VK_RSHIFT) return true;
        if ((_modifiers & Native.MOD_WIN) != 0 && vk is VK_LWIN or VK_RWIN) return true;
        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Native.UnhookWindowsHookEx(_hookHandle);
    }
}
