using System.Runtime.InteropServices;
using System.Threading;
using TextDock.Models;
using Clipboard = System.Windows.Clipboard;

namespace TextDock.Services;

public class PasteService
{
    private const int WM_CHAR = 0x0102;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const ushort VK_RETURN = 0x0D;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public uint flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public RECT rcCaret;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
        private readonly ulong _padding;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    /// <summary>アプリ別設定（プロセス名・大文字小文字無視）→ デフォルト方式の順で決定する（仕様書16章）。</summary>
    public static string ResolveMethod(TargetWindow? target, AppSettings settings)
    {
        if (target != null)
        {
            var match = settings.AppPasteSettings.FirstOrDefault(
                kv => kv.Key.Equals(target.ProcessName, StringComparison.OrdinalIgnoreCase));
            if (match.Key != null)
                return match.Value;
        }
        return settings.DefaultPasteMethod;
    }

    public void Paste(TargetWindow target, string text, AppSettings settings)
    {
        var method = ResolveMethod(target, settings);
        switch (method)
        {
            case "WM_CHAR":
                PasteByWmChar(target, text, settings.WmcharDelay);
                break;
            case "SendInput":
                PasteBySendInput(target, text, settings.SendinputDelay);
                break;
            default:
                PasteByClipboard(target, text, settings);
                break;
        }
    }

    private void PasteByClipboard(TargetWindow target, string text, AppSettings settings)
    {
        ClipboardBackup? backup = null;
        if (settings.ClipboardProtection)
            backup = ClipboardBackup.Capture();

        Clipboard.SetDataObject(text, copy: true);
        SetForegroundWindow(target.Hwnd);
        Thread.Sleep(settings.ClipboardDelay);
        SendCtrlV();

        if (backup != null)
        {
            // 対象アプリが新しい内容を読み取る前に復元しないよう待機する
            Thread.Sleep(300);
            backup.Restore();
        }
    }

    private void PasteByWmChar(TargetWindow target, string text, int delayMs)
    {
        var hwnd = GetFocusedChildWindow(target.Hwnd);
        foreach (var ch in text.Replace("\r\n", "\n"))
        {
            // 端末系アプリは改行をCRで受け取るためLFはCRに変換する
            var send = ch == '\n' ? '\r' : ch;
            PostMessage(hwnd, WM_CHAR, new IntPtr(send), IntPtr.Zero);
            Thread.Sleep(delayMs);
        }
    }

    private void PasteBySendInput(TargetWindow target, string text, int delayMs)
    {
        SetForegroundWindow(target.Hwnd);
        Thread.Sleep(50);
        foreach (var ch in text.Replace("\r\n", "\n"))
        {
            if (ch == '\n')
                SendKey(VK_RETURN);
            else
                SendUnicodeChar(ch);
            Thread.Sleep(delayMs);
        }
    }

    private static IntPtr GetFocusedChildWindow(IntPtr hwnd)
    {
        var threadId = GetWindowThreadProcessId(hwnd, out _);
        var info = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(threadId, ref info) && info.hwndFocus != IntPtr.Zero)
            return info.hwndFocus;
        return hwnd;
    }

    private static void SendCtrlV()
    {
        var inputs = new[]
        {
            KeyInput(VK_CONTROL, false),
            KeyInput(VK_V, false),
            KeyInput(VK_V, true),
            KeyInput(VK_CONTROL, true),
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendKey(ushort vk)
    {
        var inputs = new[] { KeyInput(vk, false), KeyInput(vk, true) };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static void SendUnicodeChar(char ch)
    {
        var inputs = new[]
        {
            new INPUT { type = 1, ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE } },
            new INPUT { type = 1, ki = new KEYBDINPUT { wScan = ch, dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP } },
        };
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT KeyInput(ushort vk, bool keyUp) =>
        new() { type = 1, ki = new KEYBDINPUT { wVk = vk, dwFlags = keyUp ? KEYEVENTF_KEYUP : 0 } };
}
