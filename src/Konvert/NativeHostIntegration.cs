using System.Diagnostics;
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
        get { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"); return key?.GetValue("Shardapps.Konvert") is string; }
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
        if (enabled) key.SetValue("Shardapps.Konvert", $"\"{HostPath}\"");
        else key.DeleteValue("Shardapps.Konvert", false);
    }
}
