using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TextDock.Services;

public record TargetWindow(
    IntPtr Hwnd,
    string ProcessName,
    string WindowTitle,
    string ClassName,
    int Pid)
{
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    public static TargetWindow? Capture()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
            return null;

        GetWindowThreadProcessId(hwnd, out var pid);
        if (pid == 0 || pid == Environment.ProcessId)
            return null;

        string processName;
        try
        {
            processName = Process.GetProcessById((int)pid).ProcessName + ".exe";
        }
        catch (ArgumentException)
        {
            return null;
        }

        if (string.Equals(processName, "explorer.exe", StringComparison.OrdinalIgnoreCase))
            return null;

        var title = new StringBuilder(512);
        GetWindowText(hwnd, title, title.Capacity);
        var className = new StringBuilder(256);
        GetClassName(hwnd, className, className.Capacity);

        return new TargetWindow(hwnd, processName, title.ToString(), className.ToString(), (int)pid);
    }
}
