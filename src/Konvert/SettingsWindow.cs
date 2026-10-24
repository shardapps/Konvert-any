using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Konvert;

public sealed class SettingsWindow : Window
{
    private readonly Slider quality = new() { Minimum = 10, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true };
    private readonly Slider dpi = new() { Minimum = 72, Maximum = 600, TickFrequency = 24, IsSnapToTickEnabled = false };
    private readonly TextBox folder = new();
    private readonly TextBox office = new();
    private readonly TextBox ffmpeg = new();
    private readonly CheckBox motion = new() { Content = "Reduce motion" };
    private readonly TextBlock feedback = new() { TextWrapping = TextWrapping.Wrap };
    public ConversionSettings Settings { get; private set; }

    public SettingsWindow(ConversionSettings settings, EngineCapabilities capabilities)
    {
        Settings = settings;
        Style = (Style)FindResource(typeof(Window));
        Title = "Konvert · Settings"; Width = 590; Height = 720; MinWidth = 500; MinHeight = 500;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        quality.Value = settings.JpegQuality; dpi.Value = settings.Dpi;
        folder.Text = settings.OutputDirectory ?? ""; office.Text = settings.LibreOfficePath ?? ""; ffmpeg.Text = settings.FfmpegPath ?? "";
        motion.IsChecked = settings.ReducedMotion;
        var root = new DockPanel { Margin = new Thickness(26) };
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 18, 0, 0) };
        var cancel = new Button { Content = "Cancel", IsCancel = true, Margin = new Thickness(0, 0, 8, 0) };
        var save = new Button { Content = "Save settings", IsDefault = true };
        save.Click += (_, _) => Save(); footer.Children.Add(cancel); footer.Children.Add(save);
        DockPanel.SetDock(footer, Dock.Bottom); root.Children.Add(footer);
        var body = new StackPanel();
        body.Children.Add(new TextBlock { Text = "Make it yours.", FontSize = 27, FontWeight = FontWeights.SemiBold });
        body.Children.Add(Description("Preferences stay on this device. Changes apply to your next conversion."));
        Heading(body, "OUTPUT FOLDER");
        body.Children.Add(Description("Leave blank to save beside the original. Existing files are never overwritten."));
        var browseFolder = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
        browseFolder.Click += (_, _) => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName; };
        body.Children.Add(Row(folder, browseFolder));
        Heading(body, "IMAGE QUALITY");
        var qualityLabel = new TextBlock();
        void UpdateQuality() => qualityLabel.Text = $"JPEG / image PDF quality: {quality.Value:0}%";
        quality.ValueChanged += (_, _) => UpdateQuality(); UpdateQuality();
        body.Children.Add(qualityLabel); body.Children.Add(quality);
        var dpiLabel = new TextBlock { Margin = new Thickness(0, 12, 0, 0) };
        void UpdateDpi() => dpiLabel.Text = $"Output DPI: {dpi.Value:0} · pixels stay the same";
        dpi.ValueChanged += (_, _) => UpdateDpi(); UpdateDpi();
        body.Children.Add(dpiLabel); body.Children.Add(dpi);
        body.Children.Add(Description("DPI changes physical print/PDF size, not image detail. JPEG flattens transparency onto white. Animated and multi-page images export their first frame."));
        Heading(body, "OPTIONAL ENGINES");
        body.Children.Add(Description($"LibreOffice: {(capabilities.LibreOffice is null ? "not detected" : "available")} · document, spreadsheet and presentation conversion"));
        body.Children.Add(EngineRow(office, "Choose soffice.exe", "soffice.exe"));
        body.Children.Add(Description($"FFmpeg: {(capabilities.Ffmpeg is null ? "not detected" : "available")} · audio and video conversion"));
        body.Children.Add(EngineRow(ffmpeg, "Choose ffmpeg.exe", "ffmpeg.exe"));
        body.Children.Add(Description("Blank paths use installed engines on PATH or in Program Files. Choose executables you trust. Engines are optional and installed separately."));
        Heading(body, "WINDOWS INTEGRATION");
        body.Children.Add(Description("Portable builds: add Konvert to Explorer’s ‘Show more options’ menu and your user PATH. Keep the app folder in a permanent location first."));
        var integrate = new Button { Content = "Enable Explorer + PATH", Margin = new Thickness(0, 0, 8, 0) };
        var remove = new Button { Content = "Remove integration" };
        integrate.Click += (_, _) => ChangeIntegration(true); remove.Click += (_, _) => ChangeIntegration(false);
        var integration = new StackPanel { Orientation = Orientation.Horizontal };
        integration.Children.Add(integrate); integration.Children.Add(remove); body.Children.Add(integration);
        body.Children.Add(motion); body.Children.Add(feedback);
        body.Children.Add(Description("Konvert 0.1 alpha · Shardapps · Apache-2.0\nNo startup task, tray process or background service. Close to exit."));
        root.Children.Add(new ScrollViewer { Content = body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto });
        Content = root;
    }

    private void Save()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(folder.Text) && !Directory.Exists(folder.Text)) throw new IOException("Choose an existing output folder.");
            ValidateEngine(office.Text, "soffice.exe"); ValidateEngine(ffmpeg.Text, "ffmpeg.exe");
            Settings = new ConversionSettings
            {
                JpegQuality = (int)quality.Value,
                Dpi = (int)dpi.Value,
                OutputDirectory = folder.Text,
                LibreOfficePath = string.IsNullOrWhiteSpace(office.Text) ? null : office.Text,
                FfmpegPath = string.IsNullOrWhiteSpace(ffmpeg.Text) ? null : ffmpeg.Text,
                ReducedMotion = motion.IsChecked == true
            }.Validate();
            DialogResult = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException) { feedback.Text = ex.Message; }
    }

    private static void ValidateEngine(string path, string expected)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || !Path.GetFileName(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException($"Choose an existing, absolute path to {expected}.");
    }
    private void ChangeIntegration(bool enable)
    {
        try { if (enable) ShellIntegration.Enable(); else ShellIntegration.Remove(); feedback.Text = enable ? "Integration enabled. Open a new terminal to use Konvert on PATH." : "Explorer and PATH integration removed."; }
        catch (Exception ex) { feedback.Text = ex.Message; }
    }
    private UIElement EngineRow(TextBox box, string title, string executable)
    {
        var browse = new Button { Content = "Browse…", Margin = new Thickness(8, 0, 0, 0) };
        browse.Click += (_, _) => { var dialog = new OpenFileDialog { Title = title, Filter = $"{executable}|{executable}", CheckFileExists = true }; if (dialog.ShowDialog(this) == true) box.Text = dialog.FileName; };
        return Row(box, browse);
    }
    private static DockPanel Row(UIElement field, UIElement button)
    {
        var panel = new DockPanel { Margin = new Thickness(0, 5, 0, 4) };
        DockPanel.SetDock(button, Dock.Right); panel.Children.Add(button); panel.Children.Add(field); return panel;
    }
    private static TextBlock Description(string text) => new() { Text = text, Foreground = new SolidColorBrush(Color.FromRgb(155, 170, 185)), FontSize = 12, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 10) };
    private static void Heading(Panel panel, string text) => panel.Children.Add(new TextBlock { Text = text, FontWeight = FontWeights.SemiBold, FontSize = 11, Foreground = new SolidColorBrush(Color.FromRgb(131, 232, 245)), Margin = new Thickness(0, 18, 0, 2) });
}
