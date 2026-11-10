using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace Konvert;

// Watch button-down events only while the popup is visible. Never consume or retain the input.
internal sealed class OutsideClicks : IDisposable
{
    private readonly MouseHook callback;
    private IntPtr hook;

    public OutsideClicks(Window window, Action dismiss)
    {
        var handle = new WindowInteropHelper(window).Handle;
        callback = (code, message, data) =>
        {
            if (code >= 0 && message.ToInt64() is 0x0201 or 0x0204 or 0x0207 or 0x020B)
            {
                var hit = WindowFromPoint(Marshal.PtrToStructure<ScreenPoint>(data));
                const uint RootOwner = 3;
                if (hit != handle && GetAncestor(hit, RootOwner) != handle)
                    window.Dispatcher.BeginInvoke(DispatcherPriority.Input, dismiss);
            }
            return CallNextHookEx(IntPtr.Zero, code, message, data);
        };
        hook = SetWindowsHookEx(14, callback, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(hook);
        hook = IntPtr.Zero;
        GC.KeepAlive(callback);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ScreenPoint { public int X, Y; }

    private delegate IntPtr MouseHook(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW")]
    private static extern IntPtr SetWindowsHookEx(int type, MouseHook callback, IntPtr module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll")]
    private static extern IntPtr WindowFromPoint(ScreenPoint point);

    [DllImport("user32.dll")]
    private static extern IntPtr GetAncestor(IntPtr window, uint flags);

    [DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)]
    private static extern IntPtr GetModuleHandle(string? module);
}
