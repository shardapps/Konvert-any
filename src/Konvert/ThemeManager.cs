using System.Windows;
using System.Windows.Media;

namespace Konvert;

public sealed record AppTheme(string Id, string Name, string Description, string Chrome, string Surface,
    string Raised, string Ink, string Muted, string Accent, string Outline, bool Glass = false);

public static class ThemeManager
{
    public static IReadOnlyList<AppTheme> Themes { get; } =
    [
        new("glass", "Glass", "Graphite glass · ice blue", "#F214202C", "#FC1C2B39", "#FF293D4E", "#F4F9FD", "#C1D5E5", "#A9E8FA", "#577084", true),
        new("pearl", "Pearl", "Soft white · cobalt", "#F4F7FB", "#FFFFFF", "#E7EDF7", "#1B2537", "#4C5D73", "#245DD0", "#B6C5D9"),
        new("graphite", "Graphite", "Quiet grey · icy cyan", "#171C22", "#222A33", "#2D3742", "#F1F6FC", "#B7C7D8", "#91DFEF", "#536473"),
        new("midnight", "Midnight", "Deep ink · periwinkle", "#11172A", "#1B2540", "#293555", "#F4F6FF", "#AFBCDA", "#B7C4FF", "#3C4C70"),
        new("ocean", "Ocean", "Atlantic blue · seafoam", "#102932", "#193B46", "#254E59", "#EDF9FA", "#AACCD0", "#8DE4D6", "#39616A"),
        new("lavender", "Lavender", "Pale lilac · iris", "#F4F0FC", "#FFFCFF", "#E8E0F7", "#332647", "#6D5B82", "#7451B4", "#D9CBEA"),
        new("forest", "Forest", "Evergreen · sage", "#182A24", "#233B31", "#304D40", "#F0F8F1", "#B5CCBC", "#B7E3B1", "#456451")
    ];

    public static AppTheme Current { get; private set; } = Themes[0];
    public static void Apply(string? id, ResourceDictionary resources, bool? highContrast = null)
    {
        Current = Themes.FirstOrDefault(theme => theme.Id == id) ?? Themes[0];
        var colors = new[] { Current.Chrome, Current.Surface, Current.Raised, Current.Ink, Current.Muted, Current.Accent, Current.Outline };
        string[] names = ["Chrome", "Surface", "Raised", "Ink", "Muted", "Accent", "Outline"];
        for (var i = 0; i < names.Length; i++) resources[names[i]] = Brush(colors[i]);
        var useHighContrast = highContrast ?? SystemParameters.HighContrast;
        if (useHighContrast)
        {
            resources["Chrome"] = resources["Surface"] = resources["Raised"] = SystemColors.WindowBrush;
            resources["Ink"] = resources["Muted"] = SystemColors.WindowTextBrush;
            resources["Accent"] = resources["Outline"] = SystemColors.HighlightBrush;
        }
        var accent = ((SolidColorBrush)resources["Accent"]).Color;
        var surface = ((SolidColorBrush)resources["Surface"]).Color;
        resources["CoreFill"] = resources["Surface"];
        resources["TooltipFill"] = Frozen(Color.FromRgb(surface.R, surface.G, surface.B));
        resources["StateTint"] = Frozen(Color.FromArgb(useHighContrast ? (byte)0 : (byte)40, accent.R, accent.G, accent.B));
        var luminance = 0.2126 * accent.ScR + 0.7152 * accent.ScG + 0.0722 * accent.ScB;
        resources["OnAccent"] = useHighContrast ? SystemColors.HighlightTextBrush : luminance > 0.179 ? Brushes.Black : Brushes.White;
        // A single static sheen defines the floating shell.
        var sheen = new LinearGradientBrush(
            Color.FromArgb(Current.Glass && !useHighContrast ? (byte)29 : (byte)0, 255, 255, 255),
            Colors.Transparent, new Point(0, 0), new Point(0.25, 0.82));
        sheen.Freeze();
        resources["GlassHighlight"] = sheen;
    }

    public static SolidColorBrush Brush(string color) => Frozen((Color)ColorConverter.ConvertFromString(color));

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
