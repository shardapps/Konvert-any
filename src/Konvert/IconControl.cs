using System.Windows;
using System.Windows.Media;

namespace Konvert;

// Original 24-unit line icons. No icon-font, bitmap atlas or third-party icon dependency.
public sealed class IconControl : FrameworkElement
{
    public static readonly DependencyProperty KindProperty = DependencyProperty.Register(nameof(Kind), typeof(string), typeof(IconControl), new FrameworkPropertyMetadata("file", FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty InkProperty = DependencyProperty.Register(nameof(Ink), typeof(Brush), typeof(IconControl), new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public Brush Ink { get => (Brush)GetValue(InkProperty); set => SetValue(InkProperty, value); }
    private static readonly Dictionary<string, Geometry> Icons = new()
    {
        ["file"] = Parse("M7,3 L14,3 19,8 19,21 5,21 5,3 Z M14,3 L14,8 19,8 M8,12 L16,12 M8,16 L14,16"),
        ["convert"] = Parse("M4,8 L18,8 14,4 M18,8 L14,12 M20,16 L6,16 10,12 M6,16 L10,20"),
        ["settings"] = Parse("M4,6 L10,6 M15,6 L20,6 M4,12 L15,12 M20,12 L20,12 M4,18 L6,18 M11,18 L20,18 M10,3 L15,3 15,9 10,9 Z M15,9 L20,9 20,15 15,15 Z M6,15 L11,15 11,21 6,21 Z"),
        ["folder"] = Parse("M3,7 L3,20 21,20 21,7 12,7 10,4 3,4 Z"),
        ["image"] = Parse("M4,4 L20,4 20,20 4,20 Z M4,17 L9,12 13,16 16,12 20,17 M8,8 A1,1 0 1 0 8,10 A1,1 0 1 0 8,8"),
        ["audio"] = Parse("M9,17 L9,5 19,3 19,15 M9,8 L19,6 M9,17 A3,2 0 1 1 3,17 A3,2 0 1 1 9,17 M19,15 A3,2 0 1 1 13,15 A3,2 0 1 1 19,15"),
        ["video"] = Parse("M3,6 L16,6 16,18 3,18 Z M16,10 L21,7 21,17 16,14"),
        ["table"] = Parse("M3,4 L21,4 21,20 3,20 Z M3,10 L21,10 M3,15 L21,15 M10,4 L10,20"),
        ["palette"] = Parse("M12,3 A9,9 0 1 0 12,21 L14,21 Q17,21 15,18 Q13,15 18,15 Q22,15 21,11 A9,9 0 0 0 12,3 M7,8 L7.1,8 M12,6 L12.1,6 M17,9 L17.1,9 M6,13 L6.1,13"),
        ["keyboard"] = Parse("M3,6 L21,6 21,18 3,18 Z M6,10 L7,10 M10,10 L11,10 M14,10 L15,10 M18,10 L18.1,10 M6,14 L7,14 M10,14 L18,14"),
        ["download"] = Parse("M12,3 L12,15 M7,10 L12,15 17,10 M4,16 L4,21 20,21 20,16"),
        ["check"] = Parse("M5,12 L10,17 20,6"),
        ["arrow"] = Parse("M4,12 L20,12 M14,6 L20,12 14,18"),
        ["close"] = Parse("M6,6 L18,18 M18,6 L6,18"),
        ["back"] = Parse("M16,5 L9,12 16,19"),
        ["more"] = Parse("M5,12 L5.2,12 M12,12 L12.2,12 M19,12 L19.2,12"),
        ["link"] = Parse("M9,15 L15,9 M8,10 L5,13 A4,4 0 0 0 11,19 L14,16 M10,8 L13,5 A4,4 0 0 1 19,11 L16,14"),
        ["info"] = Parse("M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,11 L12,17 M12,7 L12,7.1")
    };
    private static Geometry Parse(string value) { var geometry = Geometry.Parse(value); geometry.Freeze(); return geometry; }
    protected override void OnRender(DrawingContext context)
    {
        base.OnRender(context);
        var scale = Math.Min(ActualWidth, ActualHeight) / 24;
        context.PushTransform(new ScaleTransform(scale, scale));
        var pen = new Pen(Ink, 1.65) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
        context.DrawGeometry(null, pen, Icons.GetValueOrDefault(Kind, Icons["file"]));
        context.Pop();
    }
    public static string ForExtension(string extension) => extension.ToLowerInvariant() switch
    {
        "png" or "jpg" or "jpeg" or "bmp" or "tiff" or "gif" or "webp" => "image",
        "mp3" or "wav" or "flac" or "ogg" or "m4a" or "opus" => "audio",
        "mp4" or "mkv" or "webm" or "avi" or "mov" => "video",
        "xlsx" or "ods" or "csv" or "tsv" or "json" => "table",
        _ => "file"
    };
}
