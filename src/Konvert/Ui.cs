using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Konvert;

internal static class Ui
{
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
        content.Children.Add(Icon(icon, 18, primary ? "Chrome" : "Ink"));
        var label = Text(text, 13, primary ? "Chrome" : "Ink", FontWeights.SemiBold); label.Margin = new Thickness(9, 0, 0, 0); content.Children.Add(label);
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
}
