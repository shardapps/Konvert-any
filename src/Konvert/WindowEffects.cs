using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Konvert;

public sealed record PopupAnchor(double X, double Y);
public sealed record PopupBounds(double Left, double Top);

public static class WindowEffects
{
    public static PopupBounds Clamp(double x, double y, double width, double height, double left, double top, double right, double bottom)
        => new(Math.Clamp(x - width / 2, left + 10, Math.Max(left + 10, right - width - 10)),
            Math.Clamp(y - height / 2, top + 10, Math.Max(top + 10, bottom - height - 10)));

    public static void PlacePopup(Window window, PopupAnchor? anchor)
    {
        if (anchor is null) { GetCursorPos(out var point); anchor = new(point.X, point.Y); }
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromPoint(new PointNative((int)anchor.X, (int)anchor.Y), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        var dpi = Math.Max(96, GetDpiForWindow(handle)) / 96.0;
        var side = Math.Min(560, Math.Min(info.Work.Right - info.Work.Left - 24, info.Work.Bottom - info.Work.Top - 24) / dpi);
        window.Width = window.Height = side;
        var position = Clamp(anchor.X, anchor.Y, side * dpi, side * dpi, info.Work.Left, info.Work.Top, info.Work.Right, info.Work.Bottom);
        SetWindowPos(handle, new IntPtr(-1), (int)position.Left, (int)position.Top, (int)(side * dpi), (int)(side * dpi), 0x0010);
    }

    public static void ApplyBackdrop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero || window.AllowsTransparency) return;
        var dark = ThemeManager.Current.Id is not "pearl" and not "lavender" ? 1 : 0;
        DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
        var corners = 2;
        DwmSetWindowAttribute(handle, 33, ref corners, sizeof(int));
        var backdrop = ThemeManager.Current.Glass && !SystemParameters.HighContrast ? 3 : 1;
        DwmSetWindowAttribute(handle, 38, ref backdrop, sizeof(int));
        if (backdrop == 3 && HwndSource.FromHwnd(handle) is { } source)
        {
            source.CompositionTarget.BackgroundColor = Colors.Transparent;
            var margins = new Margins { Left = -1 };
            DwmExtendFrameIntoClientArea(handle, ref margins);
        }
    }
    [StructLayout(LayoutKind.Sequential)] private struct PointNative(int x, int y) { public int X = x, Y = y; }
    [StructLayout(LayoutKind.Sequential)] private struct RectNative { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public RectNative Monitor, Work; public uint Flags; }
    [StructLayout(LayoutKind.Sequential)] private struct Margins { public int Left, Right, Top, Bottom; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointNative point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(PointNative point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr window);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
}
