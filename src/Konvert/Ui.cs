using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Konvert;

internal static class Ui
{
    internal static IReadOnlyList<int> ImageSizeLimits { get; } = [0, 512, 1080, 1920, 2560, 3840];

    internal static string ImageSizeLabel(int index) =>
        ImageSizeLimits[index] == 0 ? "Original" : $"Up to {ImageSizeLimits[index]} px";

    internal static int ImageSizeIndex(int edge)
    {
        for (var index = 0; index < ImageSizeLimits.Count; index++)
            if (ImageSizeLimits[index] >= edge) return index;
        return ImageSizeLimits.Count - 1;
    }

    public static TextBlock Text(string value, double size = 14, string color = "Ink", FontWeight? weight = null)
    {
        var text = new TextBlock { Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal, TextWrapping = TextWrapping.Wrap };
        text.SetResourceReference(TextBlock.ForegroundProperty, color); return text;
    }
    public static IconControl Icon(string kind, double size = 21, string color = "Ink")
    {
        var icon = new IconControl { Kind = kind, Width = size, Height = size, VerticalAlignment = VerticalAlignment.Center };
        icon.SetResourceReference(IconControl.InkProperty, color); return icon;
    }
    public static Button Button(string text, string icon, RoutedEventHandler action, bool primary = false)
    {
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(Icon(icon, 18, primary ? "OnAccent" : "Ink"));
        var label = Text(text, 13, primary ? "OnAccent" : "Ink", FontWeights.SemiBold); label.Margin = new Thickness(9, 0, 0, 0); content.Children.Add(label);
        var button = new Button { Content = content, Padding = new Thickness(16, 12, 16, 12) };
        if (primary) button.SetResourceReference(Control.BackgroundProperty, "Accent");
        AutomationProperties.SetName(button, text); button.Click += action; return button;
    }
    public static Button IconButton(string icon, string name, RoutedEventHandler action)
    {
        var button = new Button { Content = Icon(icon, 18), Width = 38, Height = 38, Padding = new Thickness(8), ToolTip = name };
        AutomationProperties.SetName(button, name); button.Click += action; return button;
    }
    public static Border Card(UIElement child, double radius = 22, Thickness? padding = null)
    {
        var card = new Border { Child = child, CornerRadius = new CornerRadius(radius), Padding = padding ?? new Thickness(22), BorderThickness = new Thickness(1) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface"); card.SetResourceReference(Border.BorderBrushProperty, "Outline"); return card;
    }

    public static string FormatName(string extension) => extension switch
    {
        "pdf" => "PDF document",
        "txt" => "Plain text",
        "docx" => "Word document",
        "odt" => "OpenDocument text",
        "xlsx" => "Excel workbook",
        "ods" => "OpenDocument spreadsheet",
        "pptx" => "PowerPoint presentation",
        "odp" => "OpenDocument presentation",
        "csv" => "Comma-separated values",
        "tsv" => "Tab-separated values",
        "jpg" => "JPEG image",
        "ico" => "Windows icon",
        "webp" => "WebP image",
        "ts" => "MPEG transport stream",
        "caf" => "Core Audio file",
        "json" => "JSON data",
        _ => $"{extension.ToUpperInvariant()} {FormatCategory(extension).ToLowerInvariant()}"
    };

    public static string FormatCategory(string extension) => extension switch
    {
        "png" or "jpg" or "bmp" or "tiff" or "gif" or "ico" or "webp" or "avif" or "heif" or "heic" or "wdp" => "Image",
        "txt" or "html" or "rtf" => "Text",
        "csv" or "tsv" or "xlsx" or "ods" => "Table",
        "pptx" or "odp" => "Slides",
        "mp3" or "wav" or "flac" or "ogg" or "m4a" or "opus" or "aac" or "aiff" or "caf" => "Audio",
        "mp4" or "webm" or "mkv" or "mov" or "avi" or "ts" => "Video",
        "json" => "Data",
        _ => "Document"
    };

    public static string FormatIcon(string extension) => FormatCategory(extension) switch
    {
        "Image" => "image",
        "Audio" => "audio",
        "Video" => "video",
        "Table" or "Data" => "table",
        "Slides" => "slides",
        _ => "file"
    };
}
