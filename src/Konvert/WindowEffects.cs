using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Konvert;

public sealed record PopupAnchor(double X, double Y, double? ItemTop = null);
public sealed record PopupBounds(double Left, double Top);

public static class WindowEffects
{
    public static PopupBounds Clamp(double x, double y, double width, double height, double left, double top, double right, double bottom)
        => new(Math.Clamp(x - width / 2, left + 10, Math.Max(left + 10, right - width - 10)),
            Math.Clamp(y - height / 2, top + 10, Math.Max(top + 10, bottom - height - 10)));

    public static PopupAnchor PlacePopup(Window window, PopupAnchor? anchor, double ringSize)
    {
        if (anchor is null) { GetCursorPos(out var point); anchor = new(point.X, point.Y); }
        var handle = new WindowInteropHelper(window).Handle;
        var monitor = MonitorFromPoint(new PointNative((int)anchor.X, (int)anchor.Y), 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return anchor;
        var dpi = GetDpiForMonitor(monitor, 0, out var monitorDpi, out _) == 0 ? monitorDpi / 96.0 : 1;
        var workArea = new Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top);
        var position = FitBesideSelection(anchor, new Size(window.Width * dpi, window.Height * dpi), ringSize * dpi, workArea, dpi);
        SetWindowPos(handle, IntPtr.Zero, (int)position.Left, (int)position.Top,
            (int)position.Width, (int)position.Height, 0x0014); // SWP_NOACTIVATE | SWP_NOZORDER
        return anchor;
    }

    internal static Rect FitBesideSelection(PopupAnchor anchor, Size desired, double ringHeight, Rect workArea, double scale = 1)
    {
        var margin = 10 * scale;
        var gap = 8 * scale;
        var top = workArea.Top + margin;
        var bottom = Math.Max(top + 1, workArea.Bottom - margin);
        var left = workArea.Left + margin;
        var right = Math.Max(left + 1, workArea.Right - margin);
        var width = Math.Min(desired.Width, right - left);
        var x = Math.Clamp(anchor.X - width / 2, left, right - width);
        var below = Math.Clamp(anchor.Y + gap, top, bottom);
        var above = Math.Clamp((anchor.ItemTop ?? anchor.Y) - gap, top, bottom);
        var roomBelow = bottom - below;
        var roomAbove = above - top;
        var minimumHeight = Math.Min(desired.Height, ringHeight + 64 * scale);
        if (roomBelow >= minimumHeight || (roomBelow >= ringHeight && roomBelow >= roomAbove))
            return new(x, below, width, Math.Min(desired.Height, roomBelow));
        if (roomAbove >= ringHeight)
        {
            var height = Math.Min(desired.Height, roomAbove);
            return new(x, above - height, width, height);
        }
        var fitted = Math.Min(desired.Height, bottom - top);
        return new(x, Math.Clamp(anchor.Y - fitted / 2, top, bottom - fitted), width, fitted);
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
    [DllImport("shcore.dll")] private static extern int GetDpiForMonitor(IntPtr monitor, int type, out uint dpiX, out uint dpiY);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmExtendFrameIntoClientArea(IntPtr window, ref Margins margins);
}
