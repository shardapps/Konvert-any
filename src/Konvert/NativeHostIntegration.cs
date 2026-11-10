using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Konvert;

public static class NativeHostIntegration
{
    public static string HostPath => Path.Combine(AppContext.BaseDirectory, "KonvertHost.exe");
    public static bool IsRunning
    {
        get { try { using var mutex = Mutex.OpenExisting("Local\\Shardapps.Konvert.HotkeyHost"); return true; } catch (WaitHandleCannotBeOpenedException) { return false; } }
    }
    public static bool StartsWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            return key?.GetValue("Shardapps.Konvert") is string command && OwnsStartup(command);
        }
    }
    public static void Start()
    {
        if (!File.Exists(HostPath)) throw new FileNotFoundException("The shortcut listener is missing. Repair the Konvert installation.");
        if (!IsRunning) Process.Start(new ProcessStartInfo(HostPath) { UseShellExecute = false, CreateNoWindow = true });
    }
    public static void Stop()
    {
        if (File.Exists(HostPath)) Process.Start(new ProcessStartInfo(HostPath, "--exit") { UseShellExecute = false, CreateNoWindow = true });
    }
    public static void SetStartup(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
        if (enabled)
        {
            if (!File.Exists(HostPath)) throw new FileNotFoundException("The shortcut listener is missing. Repair the Konvert installation.");
            key.SetValue("Shardapps.Konvert", $"\"{HostPath}\"");
        }
        else if (key.GetValue("Shardapps.Konvert") is string command && OwnsStartup(command))
        {
            key.DeleteValue("Shardapps.Konvert", false);
        }
    }

    private static bool OwnsStartup(string command) =>
        command.Trim().Trim('"').Equals(HostPath, StringComparison.OrdinalIgnoreCase);

    internal static void PopupWasDismissed() => NotifyHost(0x8003, Environment.ProcessId);

    internal static void ReportBackgroundResult(bool saved) => NotifyHost(0x8004, saved ? 1 : 2);

    private static void NotifyHost(uint message, int value)
    {
        var listener = FindWindowEx(new IntPtr(-3), IntPtr.Zero, "Shardapps.Konvert.HotkeyHost", null);
        if (listener != IntPtr.Zero) PostMessage(listener, message, new IntPtr(value), IntPtr.Zero);
    }

    [DllImport("user32.dll", EntryPoint = "FindWindowExW", CharSet = CharSet.Unicode)]
    private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string? title);

    [DllImport("user32.dll", EntryPoint = "PostMessageW")]
    private static extern bool PostMessage(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);
}
