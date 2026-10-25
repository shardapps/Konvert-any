using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace Konvert;

public sealed class SettingsWindow : Window
{
    private readonly ConversionSettings original;
    private readonly EngineCapabilities capabilities;
    private readonly ContentControl page = new();
    private readonly TextBlock feedback = Ui.Text("", 12, "Muted");
    private readonly Dictionary<string, Button> navigation = [];
    private string selectedTheme;
    private readonly Slider quality = new() { Minimum = 10, Maximum = 100, TickFrequency = 5, IsSnapToTickEnabled = true };
    private readonly Slider dpi = new() { Minimum = 72, Maximum = 600 };
    private readonly TextBox folder = new(), office = new(), ffmpeg = new();
    private readonly CheckBox motion = new() { Content = "Reduce motion" };
    private readonly CheckBox startup = new() { Content = "Keep Ctrl+K ready when I sign in" };
    private readonly TextBlock qualityLabel = Ui.Text("", 14, weight: FontWeights.SemiBold);
    private readonly TextBlock dpiLabel = Ui.Text("", 14, weight: FontWeights.SemiBold);
    private bool committed;
    public ConversionSettings Settings { get; private set; }

    public SettingsWindow(ConversionSettings settings, EngineCapabilities capabilities, string section = "appearance")
    {
        original = Settings = settings; this.capabilities = capabilities; selectedTheme = settings.Theme;
        Style = (Style)FindResource(typeof(Window));
        Title = "Konvert · Settings"; Width = 830; Height = 720; MinWidth = 760; MinHeight = 560;
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        quality.Value = settings.JpegQuality; dpi.Value = settings.Dpi; folder.Text = settings.OutputDirectory ?? "";
        qualityLabel.Text = $"JPEG / image PDF quality · {quality.Value:0}%";
        dpiLabel.Text = $"Print size · {dpi.Value:0} DPI";
        quality.ValueChanged += (_, _) => qualityLabel.Text = $"JPEG / image PDF quality · {quality.Value:0}%";
        dpi.ValueChanged += (_, _) => dpiLabel.Text = $"Print size · {dpi.Value:0} DPI";
        office.Text = settings.LibreOfficePath ?? ""; ffmpeg.Text = settings.FfmpegPath ?? ""; motion.IsChecked = settings.ReducedMotion;
        startup.IsChecked = NativeHostIntegration.StartsWithWindows;
        AutomationProperties.SetName(folder, "Output folder. Blank saves beside the original.");
        AutomationProperties.SetName(office, "LibreOffice executable path");
        AutomationProperties.SetName(ffmpeg, "FFmpeg executable path");
        AutomationProperties.SetName(quality, "JPEG and image PDF quality, percent");
        AutomationProperties.SetName(dpi, "Image output DPI. Pixel dimensions stay unchanged.");
        var root = new Grid { Margin = new Thickness(1) };
        root.ColumnDefinitions.Add(new() { Width = new GridLength(190) }); root.ColumnDefinitions.Add(new());
        root.RowDefinitions.Add(new() { Height = new GridLength(60) }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = new GridLength(78) });
        var title = new DockPanel { Margin = new Thickness(24, 12, 14, 0) };
        title.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or DockPanel) DragMove(); };
        var close = Ui.IconButton("close", "Close settings", (_, _) => Close()); DockPanel.SetDock(close, Dock.Right); title.Children.Add(close);
        var heading = Ui.Text("Konvert  /  Settings", 15, weight: FontWeights.SemiBold); heading.VerticalAlignment = VerticalAlignment.Center; title.Children.Add(heading);
        Grid.SetColumnSpan(title, 2); root.Children.Add(title);
        var sidebar = new StackPanel { Margin = new Thickness(18, 24, 12, 0) };
        AddNav(sidebar, "appearance", "Appearance", "palette"); AddNav(sidebar, "conversion", "Conversion", "convert");
        AddNav(sidebar, "windows", "Windows", "keyboard"); AddNav(sidebar, "engines", "Converters", "download"); AddNav(sidebar, "about", "About", "info");
        Grid.SetRow(sidebar, 1); root.Children.Add(sidebar);
        var scroll = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(10, 24, 30, 0) };
        Grid.SetColumn(scroll, 1); Grid.SetRow(scroll, 1); root.Children.Add(scroll);
        var footer = new DockPanel { Margin = new Thickness(24, 14, 24, 18) };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        var cancel = Ui.Button("Cancel", "close", (_, _) => Close()); cancel.Margin = new Thickness(0, 0, 8, 0); cancel.IsCancel = true;
        buttons.Children.Add(cancel); var save = Ui.Button("Save changes", "check", (_, _) => Save(), true); save.IsDefault = true; buttons.Children.Add(save);
        DockPanel.SetDock(buttons, Dock.Right); footer.Children.Add(buttons);
        feedback.VerticalAlignment = VerticalAlignment.Center; feedback.Margin = new Thickness(0, 0, 18, 0); footer.Children.Add(feedback);
        Grid.SetRow(footer, 2); Grid.SetColumnSpan(footer, 2); root.Children.Add(footer); Content = root;
        SourceInitialized += (_, _) => WindowEffects.ApplyBackdrop(this);
        Closed += (_, _) => { if (!committed) ThemeManager.Apply(original.Theme, Application.Current.Resources); };
        Navigate(section);
    }

    private void AddNav(Panel panel, string id, string title, string icon)
    {
        var button = Ui.Button(title, icon, (_, _) => Navigate(id)); button.HorizontalContentAlignment = HorizontalAlignment.Left;
        button.Margin = new Thickness(0, 0, 0, 8); panel.Children.Add(button); navigation[id] = button;
    }
    private void Navigate(string id)
    {
        foreach (var item in navigation)
            item.Value.SetResourceReference(Control.BorderBrushProperty, item.Key == id ? "Accent" : "Outline");
        page.Content = id switch { "conversion" => ConversionPage(), "windows" => WindowsPage(), "engines" => EnginesPage(), "about" => AboutPage(), _ => AppearancePage() };
    }
    private StackPanel Section(string title, string description)
    {
        var content = new StackPanel();
        content.Children.Add(Ui.Text(title, 27, weight: FontWeights.SemiBold));
        var copy = Ui.Text(description, 13, "Muted"); copy.Margin = new Thickness(0, 9, 0, 23); content.Children.Add(copy); return content;
    }
    private UIElement AppearancePage()
    {
        var content = Section("A little more you.", "Choose a surface. Your shortcut and conversion flow stay the same.");
        var tiles = new WrapPanel();
        foreach (var theme in ThemeManager.Themes)
        {
            var preview = new StackPanel { Width = 128 };
            var swatch = new Grid { Width = 124, Height = 66 };
            var surface = new Border { Background = ThemeManager.Brush(theme.Chrome), CornerRadius = new CornerRadius(13), BorderBrush = ThemeManager.Brush(theme.Outline), BorderThickness = new Thickness(1) }; swatch.Children.Add(surface);
            var icon = new IconControl { Kind = "convert", Ink = ThemeManager.Brush(theme.Accent), Width = 30, Height = 30 }; swatch.Children.Add(icon);
            preview.Children.Add(swatch); var label = Ui.Text(theme.Name, 13, weight: FontWeights.SemiBold); label.Margin = new Thickness(0, 10, 0, 3); preview.Children.Add(label);
            preview.Children.Add(Ui.Text(theme.Glass ? "Default · translucent" : theme.Description, 10, "Muted"));
            var button = new Button { Content = preview, Padding = new Thickness(12), Margin = new Thickness(0, 0, 10, 10), BorderThickness = new Thickness(theme.Id == selectedTheme ? 2 : 1) };
            AutomationProperties.SetName(button, theme.Name + " theme");
            if (theme.Id == selectedTheme) button.SetResourceReference(Control.BorderBrushProperty, "Accent");
            button.Click += (_, _) =>
            {
                selectedTheme = theme.Id; ThemeManager.Apply(selectedTheme, Application.Current.Resources);
                WindowEffects.ApplyBackdrop(this); Navigate("appearance");
            };
            tiles.Children.Add(button);
        }
        content.Children.Add(tiles);
        Detach(motion); content.Children.Add(motion);
        content.Children.Add(Ui.Text("Glass uses translucent surfaces and restrained highlights. Windows backdrop effects are used where available. High contrast uses system colors.", 11, "Muted"));
        return content;
    }
    private UIElement ConversionPage()
    {
        var content = Section("Keep the original.", "Every conversion creates a separate file. Existing filenames get a numbered suffix.");
        var card = new StackPanel(); card.Children.Add(Ui.Text("Save converted files", 15, weight: FontWeights.SemiBold));
        var hint = Ui.Text("Blank saves beside the original.", 12, "Muted"); hint.Margin = new Thickness(0, 7, 0, 12); card.Children.Add(hint);
        Detach(folder); var browse = Ui.IconButton("folder", "Choose output folder", (_, _) => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName; });
        card.Children.Add(FieldRow(folder, browse)); content.Children.Add(Ui.Card(card));
        var image = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        Detach(qualityLabel);
        image.Children.Add(qualityLabel); Detach(quality); image.Children.Add(quality);
        Detach(dpiLabel);
        image.Children.Add(dpiLabel); Detach(dpi); image.Children.Add(dpi);
        image.Children.Add(Ui.Text("DPI changes print size, not pixel detail. JPEG quality is a lossy setting. Transparent images stay transparent in PNG; JPEG uses white.", 12, "Muted"));
        content.Children.Add(image); return content;
    }
    private UIElement WindowsPage()
    {
        var content = Section("One shortcut. Right here.", "Select one file in Explorer or on the desktop, then press Ctrl+K. Other apps keep their own Ctrl+K shortcut.");
        var actions = new StackPanel();
        actions.Children.Add(Ui.Text(NativeHostIntegration.IsRunning ? "Shortcut listener is running" : "Shortcut listener is stopped", 15, weight: FontWeights.SemiBold));
        var start = Ui.Button("Start shortcut listener", "keyboard", (_, _) => { try { NativeHostIntegration.Start(); feedback.Text = "Ctrl+K is ready in Explorer."; } catch (Exception ex) { feedback.Text = ex.Message; } }); start.Margin = new Thickness(0, 14, 0, 8); actions.Children.Add(start);
        actions.Children.Add(Ui.Button("Stop shortcut listener", "close", (_, _) => { NativeHostIntegration.Stop(); feedback.Text = "Listener stopped. Start it again to use Ctrl+K."; }));
        Detach(startup); actions.Children.Add(startup); content.Children.Add(Ui.Card(actions));
        var shell = new StackPanel { Margin = new Thickness(0, 22, 0, 0) };
        shell.Children.Add(Ui.Text("Explorer + terminal", 17, weight: FontWeights.SemiBold));
        var explanation = Ui.Text("The installer sets up the Konvert context command and PATH. Portable copies can register below after moving to a permanent folder.", 12, "Muted"); explanation.Margin = new Thickness(0, 8, 0, 12); shell.Children.Add(explanation);
        shell.Children.Add(Ui.Button("Enable Explorer + PATH", "link", (_, _) => Integration(true)));
        var remove = Ui.Button("Remove Explorer + PATH", "close", (_, _) => Integration(false)); remove.Margin = new Thickness(0, 8, 0, 0); shell.Children.Add(remove);
        content.Children.Add(shell); return content;
    }
    private UIElement EnginesPage()
    {
        var content = Section("The right converter.", "Native image, text and table conversions work immediately. Add local engines for office documents and media.");
        content.Children.Add(EngineCard("LibreOffice", capabilities.LibreOffice is not null, "Documents, spreadsheets and presentations. Includes DOCX → PDF without Microsoft Word.", office, "soffice.exe", "https://www.libreoffice.org/download/download-libreoffice/"));
        var second = EngineCard("FFmpeg", capabilities.Ffmpeg is not null, "Audio, video and audio extraction. Only encoders provided by your FFmpeg build are offered.", ffmpeg, "ffmpeg.exe", "https://ffmpeg.org/download.html");
        second.Margin = new Thickness(0, 16, 0, 0); content.Children.Add(second);
        var note = Ui.Text("Engine downloads are separate to keep Konvert small. Install an engine, then reopen the popup or choose its executable here. Conversion stays local; unsupported or lossy routes are never presented as lossless.", 11, "Muted"); note.Margin = new Thickness(0, 18, 0, 0); content.Children.Add(note); return content;
    }
    private Border EngineCard(string name, bool ready, string description, TextBox box, string executable, string url)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(name + (ready ? "  ·  Available" : "  ·  Not installed"), 16, weight: FontWeights.SemiBold));
        var copy = Ui.Text(description, 12, "Muted"); copy.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(copy);
        Detach(box); panel.Children.Add(FieldRow(box, Ui.IconButton("folder", "Choose " + executable, (_, _) =>
        {
            var dialog = new OpenFileDialog { Filter = executable + "|" + executable, CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) box.Text = dialog.FileName;
        })));
        var button = Ui.Button("Get " + name, "download", (_, _) => OpenUrl(url)); button.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(button);
        return Ui.Card(panel);
    }
    private UIElement AboutPage()
    {
        var content = Section("Useful. Quiet. Yours.", "Konvert 0.2 alpha · by Shardapps");
        content.Children.Add(Ui.Text("A native Windows file converter with a file-centered popup. No account, uploads, ads or analytics.", 15));
        var detail = Ui.Text("The conversion window exists only when you need it. A separate native listener handles the shortcut through Windows events, without a keyboard recorder or polling loop. Quit the listener from its tray menu to stop all Konvert activity.", 13, "Muted"); detail.Margin = new Thickness(0, 18, 0, 20); content.Children.Add(detail);
        content.Children.Add(Ui.Button("Source code & feedback", "link", (_, _) => OpenUrl("https://github.com/shardapps/Konvert-any")));
        return content;
    }
    private void Save()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(folder.Text) && !Directory.Exists(folder.Text)) throw new IOException("Choose an existing output folder.");
            ValidateEngine(office.Text, "soffice.exe"); ValidateEngine(ffmpeg.Text, "ffmpeg.exe");
            Settings = original with
            {
                Theme = selectedTheme,
                JpegQuality = (int)quality.Value,
                Dpi = (int)dpi.Value,
                OutputDirectory = folder.Text,
                LibreOfficePath = string.IsNullOrWhiteSpace(office.Text) ? null : office.Text,
                FfmpegPath = string.IsNullOrWhiteSpace(ffmpeg.Text) ? null : ffmpeg.Text,
                ReducedMotion = motion.IsChecked == true
            };
            if (startup.IsChecked != NativeHostIntegration.StartsWithWindows) NativeHostIntegration.SetStartup(startup.IsChecked == true);
            Settings = Settings.Validate(); committed = true; DialogResult = true;
        }
        catch (Exception ex) { feedback.Text = ex.Message; }
    }
    private void Integration(bool enable)
    {
        try { if (enable) ShellIntegration.Enable(); else ShellIntegration.Remove(); feedback.Text = enable ? "Explorer and PATH enabled. Open a new terminal." : "Explorer and PATH integration removed."; }
        catch (Exception ex) { feedback.Text = ex.Message; }
    }
    private void OpenUrl(string url) { try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch (Exception ex) { feedback.Text = ex.Message; } }
    private static void ValidateEngine(string path, string expected)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || !Path.GetFileName(path).Equals(expected, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Choose an existing absolute path to " + expected + ".");
    }
    private static DockPanel FieldRow(UIElement field, UIElement button)
    {
        var row = new DockPanel(); button.SetValue(MarginProperty, new Thickness(8, 0, 0, 0));
        DockPanel.SetDock(button, Dock.Right); row.Children.Add(button); row.Children.Add(field); return row;
    }
    private static void Detach(FrameworkElement element)
    {
        if (element.Parent is Panel panel) panel.Children.Remove(element);
        else if (element.Parent is Decorator decorator) decorator.Child = null;
    }
}
