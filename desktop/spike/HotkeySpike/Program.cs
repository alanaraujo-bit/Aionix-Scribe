using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Interop;

namespace HotkeySpike;

internal static class Native
{
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    public const int SW_RESTORE = 9;
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    public const uint WM_CLOSE = 0x0010;
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    public const uint INPUT_MOUSE = 0;
    public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;
    public const uint MOUSEEVENTF_LEFTUP = 0x0004;
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();

    public static bool ForceForeground(IntPtr hWnd)
    {
        var fg = GetForegroundWindow();
        var foreThread = GetWindowThreadProcessId(fg, out _);
        var appThread = GetCurrentThreadId();
        bool attached = foreThread != appThread && AttachThreadInput(foreThread, appThread, true);
        ShowWindow(hWnd, SW_RESTORE);
        var ok = SetForegroundWindow(hWnd);
        if (attached) AttachThreadInput(foreThread, appThread, false);
        return ok;
    }
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    [DllImport("user32.dll", SetLastError = true)] public static extern int GetWindowLong(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll", SetLastError = true)] public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
    public const int GWL_EXSTYLE = -20;
    public const int WS_EX_TRANSPARENT = 0x20;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOOLWINDOW = 0x80;
    public const int WS_EX_LAYERED = 0x80000;

    [StructLayout(LayoutKind.Explicit)]
    public struct INPUT
    {
        [FieldOffset(0)] public uint type;
        [FieldOffset(8)] public InputUnion U;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
        [FieldOffset(0)] public HARDWAREINPUT hi;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct HARDWAREINPUT
    {
        public uint uMsg;
        public ushort wParamL;
        public ushort wParamH;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const ushort VK_CONTROL = 0x11;
    public const ushort VK_V = 0x56;
    public const ushort VK_A = 0x41;
    public const ushort VK_C = 0x43;
    public const ushort VK_DELETE = 0x2E;
    public const ushort VK_MENU = 0x12;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")] public static extern bool OpenClipboard(IntPtr hWndNewOwner);
    [DllImport("user32.dll")] public static extern bool CloseClipboard();
    [DllImport("user32.dll")] public static extern bool EmptyClipboard();
    [DllImport("user32.dll")] public static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);
    [DllImport("user32.dll")] public static extern IntPtr GetClipboardData(uint uFormat);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalLock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern bool GlobalUnlock(IntPtr hMem);
    [DllImport("kernel32.dll")] public static extern IntPtr GlobalSize(IntPtr hMem);
    public const uint CF_UNICODETEXT = 13;
    public const uint GMEM_MOVEABLE = 0x0002;

    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;
}

internal static class Log
{
    private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "spike.log");
    public static void Write(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss.fff}] {msg}";
        File.AppendAllText(LogPath, line + Environment.NewLine);
        Console.WriteLine(line);
    }
}

internal static class ClipboardHelper
{
    public static string? GetText()
    {
        if (!Native.OpenClipboard(IntPtr.Zero)) return null;
        try
        {
            var h = Native.GetClipboardData(Native.CF_UNICODETEXT);
            if (h == IntPtr.Zero) return null;
            var ptr = Native.GlobalLock(h);
            if (ptr == IntPtr.Zero) return null;
            try { return Marshal.PtrToStringUni(ptr); }
            finally { Native.GlobalUnlock(h); }
        }
        finally { Native.CloseClipboard(); }
    }

    public static void SetText(string text)
    {
        if (!Native.OpenClipboard(IntPtr.Zero)) throw new InvalidOperationException("OpenClipboard failed");
        try
        {
            Native.EmptyClipboard();
            var bytes = (text.Length + 1) * 2;
            var hGlobal = Native.GlobalAlloc(Native.GMEM_MOVEABLE, (UIntPtr)bytes);
            var target = Native.GlobalLock(hGlobal);
            Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
            Marshal.WriteInt16(target, text.Length * 2, 0);
            Native.GlobalUnlock(hGlobal);
            Native.SetClipboardData(Native.CF_UNICODETEXT, hGlobal);
        }
        finally { Native.CloseClipboard(); }
    }
}

internal static class Injector
{
    public static void SendUnicodeText(string text)
    {
        var inputs = new Native.INPUT[text.Length * 2];
        int idx = 0;
        foreach (var ch in text)
        {
            inputs[idx++] = KeyDownUnicode(ch);
            inputs[idx++] = KeyUpUnicode(ch);
        }
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        Log.Write($"SendInput(unicode text len={text.Length}) -> events sent by API={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
    }

    private static Native.INPUT KeyDownUnicode(char ch) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = Native.KEYEVENTF_UNICODE, time = 0, dwExtraInfo = IntPtr.Zero } }
    };

    private static Native.INPUT KeyUpUnicode(char ch) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = 0, wScan = ch, dwFlags = Native.KEYEVENTF_UNICODE | Native.KEYEVENTF_KEYUP, time = 0, dwExtraInfo = IntPtr.Zero } }
    };

    public static void SendKey(ushort vk)
    {
        var inputs = new[] { KeyEvent(vk, false), KeyEvent(vk, true) };
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        Log.Write($"SendInput(key {vk:X2}) -> events sent={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
    }

    private static Native.INPUT KeyEvent(ushort vk, bool up) => new()
    {
        type = Native.INPUT_KEYBOARD,
        U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = up ? Native.KEYEVENTF_KEYUP : 0, time = 0, dwExtraInfo = IntPtr.Zero } }
    };

    public static void ClickAt(int x, int y)
    {
        Native.SetCursorPos(x, y);
        var inputs = new[]
        {
            new Native.INPUT { type = Native.INPUT_MOUSE, U = new Native.InputUnion { mi = new Native.MOUSEINPUT { dx = 0, dy = 0, mouseData = 0, dwFlags = Native.MOUSEEVENTF_LEFTDOWN, time = 0, dwExtraInfo = IntPtr.Zero } } },
            new Native.INPUT { type = Native.INPUT_MOUSE, U = new Native.InputUnion { mi = new Native.MOUSEINPUT { dx = 0, dy = 0, mouseData = 0, dwFlags = Native.MOUSEEVENTF_LEFTUP, time = 0, dwExtraInfo = IntPtr.Zero } } },
        };
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        Log.Write($"ClickAt({x},{y}) -> events sent={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
    }

    public static void SendAltCombo(ushort vk)
    {
        var inputs = new[]
        {
            KeyEvent(Native.VK_MENU, false),
            KeyEvent(vk, false),
            KeyEvent(vk, true),
            KeyEvent(Native.VK_MENU, true),
        };
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        Log.Write($"SendInput(alt+{(char)vk}) -> events sent by API={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
    }

    public static void SendCtrlCombo(ushort vk)
    {
        var inputs = new[]
        {
            KeyEvent(Native.VK_CONTROL, false),
            KeyEvent(vk, false),
            KeyEvent(vk, true),
            KeyEvent(Native.VK_CONTROL, true),
        };
        var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
        Log.Write($"SendInput(ctrl+{(char)vk}) -> events sent by API={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
    }
}

public class OverlayWindow : Window
{
    public OverlayWindow()
    {
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(160, 20, 20, 30));
        Topmost = true;
        ShowInTaskbar = false;
        ShowActivated = false;
        Width = 260;
        Height = 70;
        Left = SystemParameters.PrimaryScreenWidth - 300;
        Top = SystemParameters.PrimaryScreenHeight - 140;
        Content = new System.Windows.Controls.TextBlock
        {
            Text = "Aionix Scribe — spike overlay",
            Foreground = System.Windows.Media.Brushes.White,
            FontSize = 14,
            Margin = new Thickness(12),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int ex = Native.GetWindowLong(hwnd, Native.GWL_EXSTYLE);
        ex |= Native.WS_EX_NOACTIVATE | Native.WS_EX_TOOLWINDOW | Native.WS_EX_LAYERED;
        Native.SetWindowLong(hwnd, Native.GWL_EXSTYLE, ex);
        Log.Write($"Overlay hwnd={hwnd} extended style set (WS_EX_NOACTIVATE|TOOLWINDOW|LAYERED)");
    }
}

public class HotkeyListener : IDisposable
{
    private readonly HwndSource _source;
    private readonly int _id;
    public int FireCount { get; private set; }

    public HotkeyListener(int id, uint modifiers, uint vk)
    {
        _id = id;
        var parameters = new HwndSourceParameters("AionixSpikeHotkeyWindow") { Width = 0, Height = 0, WindowStyle = 0 };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
        var registered = Native.RegisterHotKey(_source.Handle, _id, modifiers, vk);
        Log.Write($"RegisterHotKey(id={id}, mods={modifiers}, vk={vk:X2}) -> registered={registered}, lastError={Marshal.GetLastWin32Error()}");
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_HOTKEY && wParam.ToInt32() == _id)
        {
            FireCount++;
            Log.Write($"WM_HOTKEY fired (count={FireCount})");
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        Native.UnregisterHotKey(_source.Handle, _id);
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }
}

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0)
        {
            Log.Write("Usage: overlay-test | inject <text> | inject-clipboard <text> | verify-clipboard | foreground");
            return 1;
        }

        switch (args[0])
        {
            case "fire-hotkey":
                {
                    const ushort VK_F13 = 0x7C;
                    var inputs = new[]
                    {
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_CONTROL } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_MENU } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = 0x10 } } }, // VK_SHIFT
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = VK_F13 } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = VK_F13, dwFlags = Native.KEYEVENTF_KEYUP } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = 0x10, dwFlags = Native.KEYEVENTF_KEYUP } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_MENU, dwFlags = Native.KEYEVENTF_KEYUP } } },
                        new Native.INPUT { type = Native.INPUT_KEYBOARD, U = new Native.InputUnion { ki = new Native.KEYBDINPUT { wVk = Native.VK_CONTROL, dwFlags = Native.KEYEVENTF_KEYUP } } },
                    };
                    var sent = Native.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Native.INPUT>());
                    Log.Write($"fire-hotkey: sent Ctrl+Alt+Shift+F13, events={sent}/{inputs.Length}, lastError={Marshal.GetLastWin32Error()}");
                    return 0;
                }
            case "hotkey-listen":
                {
                    // args: hotkey-listen <seconds> ; uses Ctrl+Alt+Shift+F13 (uncommon combo, low collision risk)
                    var seconds = args.Length > 1 ? int.Parse(args[1]) : 5;
                    var app = new Application();
                    const uint VK_F13 = 0x7C;
                    var listener = new HotkeyListener(1, Native.MOD_CONTROL | Native.MOD_ALT | Native.MOD_SHIFT, VK_F13);
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
                    timer.Tick += (_, _) =>
                    {
                        Log.Write($"hotkey-listen window closing, totalFireCount={listener.FireCount}");
                        listener.Dispose();
                        app.Shutdown();
                    };
                    timer.Start();
                    app.Run();
                    return 0;
                }
            case "sizeof":
                {
                    Log.Write($"sizeof(INPUT)={Marshal.SizeOf<Native.INPUT>()} sizeof(KEYBDINPUT)={Marshal.SizeOf<Native.KEYBDINPUT>()} sizeof(InputUnion)={Marshal.SizeOf<Native.InputUnion>()} IntPtr.Size={IntPtr.Size}");
                    return 0;
                }
            case "list-windows":
                {
                    var found = new List<(IntPtr Hwnd, string Title, uint Pid)>();
                    Native.EnumWindows((hWnd, _) =>
                    {
                        if (Native.IsWindowVisible(hWnd))
                        {
                            var sb = new StringBuilder(256);
                            Native.GetWindowText(hWnd, sb, sb.Capacity);
                            if (sb.Length > 0)
                            {
                                Native.GetWindowThreadProcessId(hWnd, out var pid);
                                found.Add((hWnd, sb.ToString(), pid));
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                    foreach (var w in found) Log.Write($"window hwnd={w.Hwnd} pid={w.Pid} title='{w.Title}'");
                    return 0;
                }
            case "activate":
                {
                    var needle = args.Length > 1 ? args[1] : "";
                    IntPtr match = IntPtr.Zero;
                    string matchTitle = "";
                    Native.EnumWindows((hWnd, _) =>
                    {
                        if (Native.IsWindowVisible(hWnd))
                        {
                            var sb = new StringBuilder(256);
                            Native.GetWindowText(hWnd, sb, sb.Capacity);
                            if (sb.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase))
                            {
                                match = hWnd;
                                matchTitle = sb.ToString();
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (match == IntPtr.Zero) { Log.Write($"activate: no window matching '{needle}'"); return 1; }
                    var ok = Native.ForceForeground(match);
                    Thread.Sleep(150);
                    var confirm = Native.GetForegroundWindow();
                    Log.Write($"activate: matched hwnd={match} title='{matchTitle}' ForceForeground={ok} confirmedForeground={confirm == match}");
                    return 0;
                }
            case "click-activate":
                {
                    var needle = args.Length > 1 ? args[1] : "";
                    IntPtr match = IntPtr.Zero;
                    string matchTitle = "";
                    Native.EnumWindows((hWnd, _) =>
                    {
                        if (Native.IsWindowVisible(hWnd))
                        {
                            var sb = new StringBuilder(256);
                            Native.GetWindowText(hWnd, sb, sb.Capacity);
                            if (sb.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase))
                            {
                                match = hWnd;
                                matchTitle = sb.ToString();
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (match == IntPtr.Zero) { Log.Write($"click-activate: no window matching '{needle}'"); return 1; }
                    Native.GetWindowRect(match, out var rect);
                    int x = rect.Left + Math.Min(200, (rect.Right - rect.Left) / 2);
                    int y = rect.Top + Math.Min(150, (rect.Bottom - rect.Top) / 2);
                    Injector.ClickAt(x, y);
                    Thread.Sleep(200);
                    var confirm = Native.GetForegroundWindow();
                    Log.Write($"click-activate: matched hwnd={match} title='{matchTitle}' rect=({rect.Left},{rect.Top},{rect.Right},{rect.Bottom}) clickedAt=({x},{y}) confirmedForeground={confirm == match}");
                    return 0;
                }
            case "foreground":
                {
                    var hwnd = Native.GetForegroundWindow();
                    var sb = new StringBuilder(256);
                    Native.GetWindowText(hwnd, sb, sb.Capacity);
                    Log.Write($"foreground hwnd={hwnd} title='{sb}'");
                    return 0;
                }
            case "overlay-test":
                {
                    var before = Native.GetForegroundWindow();
                    Log.Write($"before-show foreground hwnd={before}");
                    var app = new Application();
                    var win = new OverlayWindow();
                    win.Show();
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
                    int ticks = 0;
                    timer.Tick += (_, _) =>
                    {
                        var fg = Native.GetForegroundWindow();
                        Log.Write($"tick={ticks} foreground hwnd={fg} sameAsBefore={fg == before} isOverlay={fg == new WindowInteropHelper(win).Handle}");
                        ticks++;
                        if (ticks >= 8) { win.Close(); app.Shutdown(); }
                    };
                    timer.Start();
                    app.Run();
                    return 0;
                }
            case "close-window":
                {
                    var needle = args.Length > 1 ? args[1] : "";
                    IntPtr match = IntPtr.Zero;
                    string matchTitle = "";
                    Native.EnumWindows((hWnd, _) =>
                    {
                        if (Native.IsWindowVisible(hWnd))
                        {
                            var sb = new StringBuilder(256);
                            Native.GetWindowText(hWnd, sb, sb.Capacity);
                            if (sb.ToString().Contains(needle, StringComparison.OrdinalIgnoreCase))
                            {
                                match = hWnd;
                                matchTitle = sb.ToString();
                                return false;
                            }
                        }
                        return true;
                    }, IntPtr.Zero);
                    if (match == IntPtr.Zero) { Log.Write($"close-window: no window matching '{needle}'"); return 1; }
                    Native.PostMessage(match, Native.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
                    Log.Write($"close-window: sent WM_CLOSE to hwnd={match} title='{matchTitle}'");
                    return 0;
                }
            case "clear":
                {
                    Injector.SendCtrlCombo(Native.VK_A);
                    Thread.Sleep(100);
                    Injector.SendKey(Native.VK_DELETE);
                    Log.Write("clear: sent ctrl+a then delete");
                    return 0;
                }
            case "altkey":
                {
                    var ch = args.Length > 1 ? (ushort)char.ToUpperInvariant(args[1][0]) : (ushort)0;
                    Injector.SendAltCombo(ch);
                    return 0;
                }
            case "inject":
                {
                    var text = args.Length > 1 ? args[1] : "";
                    var fg = Native.GetForegroundWindow();
                    var sb = new StringBuilder(256);
                    Native.GetWindowText(fg, sb, sb.Capacity);
                    Log.Write($"inject target foreground hwnd={fg} title='{sb}'");
                    Injector.SendUnicodeText(text);
                    return 0;
                }
            case "inject-clipboard":
                {
                    var text = args.Length > 1 ? args[1] : "";
                    var previous = ClipboardHelper.GetText();
                    Log.Write($"previous clipboard captured, length={previous?.Length ?? -1}");
                    ClipboardHelper.SetText(text);
                    Thread.Sleep(100);
                    Injector.SendCtrlCombo(Native.VK_V);
                    Thread.Sleep(200);
                    if (previous != null)
                    {
                        ClipboardHelper.SetText(previous);
                        Log.Write("clipboard restored to previous content");
                    }
                    return 0;
                }
            case "verify-clipboard":
                {
                    Thread.Sleep(150);
                    Injector.SendCtrlCombo(Native.VK_A);
                    Thread.Sleep(100);
                    Injector.SendCtrlCombo(Native.VK_C);
                    Thread.Sleep(200);
                    var content = ClipboardHelper.GetText();
                    Log.Write($"verify-clipboard captured selection: '{content}'");
                    return 0;
                }
            default:
                Log.Write($"Unknown mode {args[0]}");
                return 1;
        }
    }
}
