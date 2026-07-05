using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace TextDock.Services;

public class HotkeyManager : IDisposable
{
    public const uint MOD_ALT = 0x0001;
    public const uint MOD_CONTROL = 0x0002;
    public const uint MOD_SHIFT = 0x0004;
    public const uint MOD_WIN = 0x0008;

    private const int WM_HOTKEY = 0x0312;
    private const int HotkeyId = 0xA123;

    [DllImport("user32.dll")]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll")]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _source;
    private bool _registered;

    public event Action? HotkeyPressed;

    public static bool TryParse(string text, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var parts = text.Split('+', StringSplitOptions.TrimEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            var isLast = i == parts.Length - 1;
            switch (part)
            {
                case "Ctrl": modifiers |= MOD_CONTROL; break;
                case "Shift": modifiers |= MOD_SHIFT; break;
                case "Alt": modifiers |= MOD_ALT; break;
                case "Win": modifiers |= MOD_WIN; break;
                default:
                    if (!isLast || !Enum.TryParse<Key>(part, ignoreCase: true, out var key))
                        return false;
                    vk = (uint)KeyInterop.VirtualKeyFromKey(key);
                    return vk != 0;
            }
        }
        return false;
    }

    public bool Register(IntPtr hwnd, uint modifiers, uint vk)
    {
        Unregister();
        _source = HwndSource.FromHwnd(hwnd);
        if (_source == null)
            return false;
        _source.AddHook(WndProc);
        _registered = RegisterHotKey(hwnd, HotkeyId, modifiers, vk);
        if (!_registered)
        {
            _source.RemoveHook(WndProc);
            _source = null;
        }
        return _registered;
    }

    public void Unregister()
    {
        if (_source != null)
        {
            if (_registered)
                UnregisterHotKey(_source.Handle, HotkeyId);
            _source.RemoveHook(WndProc);
            _source = null;
            _registered = false;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HotkeyId)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Unregister();
}
