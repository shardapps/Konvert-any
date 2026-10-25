using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Konvert;

internal static class ShellIntegration
{
    private const string ShellKey = @"Software\Classes\*\shell\Shardapps.Konvert";
    private const string TrackingKey = @"Software\Shardapps\Konvert";

    public static void Enable()
    {
        EnsureUnpackaged();
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate Konvert.exe.");
        if (!Path.GetFileName(executable).Equals("Konvert.exe", StringComparison.OrdinalIgnoreCase)) throw new IOException("Run the published Konvert.exe to enable integration.");
        using var tracking = Registry.CurrentUser.CreateSubKey(TrackingKey);
        // Preserve ownership across re-enabling and relocation so removal never deletes a pre-existing PATH entry.
        RemoveOwnedPath(tracking);
        using var key = Registry.CurrentUser.CreateSubKey(ShellKey);
        key.SetValue("", "Konvert"); key.SetValue("Icon", executable); key.SetValue("MultiSelectModel", "Single");
        using var command = key.CreateSubKey("command"); command.SetValue("", $"\"{executable}\" --popup \"%1\"");
        var directory = Path.GetDirectoryName(executable)!;
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        if (!userPath.Split(';').Any(item => SamePath(item, directory)))
        {
            Environment.SetEnvironmentVariable("PATH", string.IsNullOrEmpty(userPath) ? directory : directory + ";" + userPath.TrimStart(';'), EnvironmentVariableTarget.User);
            tracking.SetValue("OwnedPath", directory);
        }
        Broadcast();
    }

    public static void Remove()
    {
        EnsureUnpackaged();
        var executable = Environment.ProcessPath ?? throw new IOException("Cannot locate Konvert.exe.");
        using (var command = Registry.CurrentUser.OpenSubKey(ShellKey + @"\command"))
        {
            if (command?.GetValue("") is string value && value.StartsWith($"\"{executable}\"", StringComparison.OrdinalIgnoreCase))
            {
                command.Close();
                Registry.CurrentUser.DeleteSubKeyTree(ShellKey, false);
            }
        }
        using var tracking = Registry.CurrentUser.OpenSubKey(TrackingKey, true);
        if (tracking?.GetValue("OwnedPath") is string directory && SamePath(directory, Path.GetDirectoryName(executable)!)) RemoveOwnedPath(tracking);
        Broadcast();
    }
    private static void RemoveOwnedPath(RegistryKey tracking)
    {
        if (tracking.GetValue("OwnedPath") is not string directory) return;
        var userPath = Environment.GetEnvironmentVariable("PATH", EnvironmentVariableTarget.User) ?? "";
        Environment.SetEnvironmentVariable("PATH", string.Join(';', userPath.Split(';').Where(item => !SamePath(item, directory))), EnvironmentVariableTarget.User);
        tracking.DeleteValue("OwnedPath", false);
    }
    private static bool SamePath(string a, string b) => a.Trim().TrimEnd('\\').Equals(b.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);
    private static void EnsureUnpackaged()
    {
        uint length = 0;
        if (GetCurrentPackageFullName(ref length, IntPtr.Zero) != 15700)
            throw new InvalidOperationException("Packaged installs use manifest-managed integration. Manual registration is only for portable builds.");
    }
    private static void Broadcast() => SendMessageTimeout(new IntPtr(0xffff), 0x001A, IntPtr.Zero, "Environment", 2, 1000, out _);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern int GetCurrentPackageFullName(ref uint length, IntPtr packageFullName);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern IntPtr SendMessageTimeout(IntPtr window, uint message, IntPtr wParam, string lParam, uint flags, uint timeout, out IntPtr result);
}
