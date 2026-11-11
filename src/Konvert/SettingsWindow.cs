using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private readonly Slider size = new() { Minimum = 0, Maximum = Ui.ImageSizeLimits.Count - 1, TickFrequency = 1, IsSnapToTickEnabled = true };
    private readonly TextBox folder = new(), office = new(), ffmpeg = new();
    private readonly CheckBox motion = new() { Content = "Reduce motion" };
    private readonly CheckBox transparency = new() { Content = "Reduce transparency" };
    private readonly CheckBox startup = new() { Content = "Keep Ctrl+K ready when I sign in" };
    private readonly TextBlock qualityLabel = Ui.Text("", 14, weight: FontWeights.SemiBold);
    private readonly TextBlock dpiLabel = Ui.Text("", 14, weight: FontWeights.SemiBold);
    private readonly TextBlock sizeLabel = Ui.Text("", 14, weight: FontWeights.SemiBold);
    private bool committed;
    public ConversionSettings Settings { get; private set; }

    public SettingsWindow(ConversionSettings settings, EngineCapabilities capabilities, string section = "appearance")
    {
        original = Settings = settings; this.capabilities = capabilities; selectedTheme = settings.Theme;
        Style = (Style)FindResource(typeof(Window));
        Title = "Konvert · Settings";
        Width = Math.Min(780, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(640, SystemParameters.WorkArea.Height - 24);
        MinWidth = Math.Min(610, Width);
        MinHeight = Math.Min(420, Height);
        WindowStyle = WindowStyle.None; ResizeMode = ResizeMode.CanResizeWithGrip; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        quality.Value = settings.JpegQuality; dpi.Value = settings.Dpi; folder.Text = settings.OutputDirectory ?? "";
        size.Value = Ui.ImageSizeIndex(settings.MaxImageEdge);
        sizeLabel.Text = $"Image size · {Ui.ImageSizeLabel((int)size.Value)}";
        qualityLabel.Text = $"JPEG / image PDF quality · {quality.Value:0}%";
        dpiLabel.Text = $"Print size · {dpi.Value:0} DPI";
        quality.ValueChanged += (_, _) => qualityLabel.Text = $"JPEG / image PDF quality · {quality.Value:0}%";
        dpi.ValueChanged += (_, _) => dpiLabel.Text = $"Print size · {dpi.Value:0} DPI";
        size.ValueChanged += (_, _) => sizeLabel.Text = $"Image size · {Ui.ImageSizeLabel((int)size.Value)}";
        office.Text = settings.LibreOfficePath ?? ""; ffmpeg.Text = settings.FfmpegPath ?? ""; motion.IsChecked = settings.ReducedMotion;
        transparency.IsChecked = settings.ReducedTransparency;
        transparency.Checked += (_, _) => PreviewTheme();
        transparency.Unchecked += (_, _) => PreviewTheme();
        startup.IsChecked = NativeHostIntegration.StartsWithWindows;
        AutomationProperties.SetName(folder, "Output folder. Blank saves beside the original.");
        AutomationProperties.SetName(office, "LibreOffice executable path");
        AutomationProperties.SetName(ffmpeg, "FFmpeg executable path");
        AutomationProperties.SetName(quality, "JPEG and image PDF quality, percent");
        AutomationProperties.SetName(dpi, "Image output DPI. Changes the print size.");
        AutomationProperties.SetName(size, "Maximum image width or height. Never enlarges images.");
        var root = new Grid { Margin = new Thickness(1) };
        root.ColumnDefinitions.Add(new() { Width = new GridLength(170) }); root.ColumnDefinitions.Add(new());
        root.RowDefinitions.Add(new() { Height = new GridLength(60) }); root.RowDefinitions.Add(new()); root.RowDefinitions.Add(new() { Height = new GridLength(78) });
        var title = new DockPanel { Margin = new Thickness(24, 12, 14, 0) };
        title.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or DockPanel) DragMove(); };
        var close = Ui.IconButton("close", "Close settings", (_, _) => Close()); DockPanel.SetDock(close, Dock.Right); title.Children.Add(close);
        var heading = Ui.Text("Konvert Settings", 16, weight: FontWeights.SemiBold); heading.VerticalAlignment = VerticalAlignment.Center; title.Children.Add(heading);
        Grid.SetColumnSpan(title, 2); root.Children.Add(title);
        var sidebar = new StackPanel { Margin = new Thickness(18, 24, 12, 0) };
        AddNav(sidebar, "appearance", "Appearance", "palette"); AddNav(sidebar, "conversion", "Conversion", "convert");
        AddNav(sidebar, "windows", "Windows", "keyboard"); AddNav(sidebar, "engines", "Converters", "download"); AddNav(sidebar, "about", "About", "info");
        var navigationScroll = new ScrollViewer { Content = sidebar, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(navigationScroll, 1);
        root.Children.Add(navigationScroll);
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
        Closed += (_, _) =>
        {
            if (!committed)
                ThemeManager.Apply(original.Theme, Application.Current.Resources, reducedTransparency: original.ReducedTransparency);
        };
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
        {
            var current = item.Key == id;
            item.Value.SetResourceReference(Control.BorderBrushProperty, current ? "Accent" : "Outline");
            item.Value.SetResourceReference(Control.BackgroundProperty, current ? "StateTint" : "Surface");
        }
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
        var content = Section("Appearance", "Choose a theme to preview it. Select Save to keep your changes.");
        var effects = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
        Detach(motion);
        Detach(transparency);
        effects.Children.Add(motion);
        effects.Children.Add(transparency);
        content.Children.Add(effects);
        var tiles = new WrapPanel();
        var choices = new List<(AppTheme Theme, Button Tile, IconControl Selected)>();
        foreach (var theme in ThemeManager.Themes)
        {
            var preview = new StackPanel { Width = 128 };
            preview.Children.Add(ThemeSwatch(theme));
            var title = new DockPanel { Margin = new Thickness(0, 8, 0, 3) };
            var selected = Ui.Icon("check", 14, "Accent");
            DockPanel.SetDock(selected, Dock.Right);
            title.Children.Add(selected);
            title.Children.Add(Ui.Text(theme.Name, 13, weight: FontWeights.SemiBold));
            preview.Children.Add(title);
            preview.Children.Add(Ui.Text(theme.Description, 10, "Muted"));
            var button = new Button { Content = preview, Padding = new Thickness(10), Margin = new Thickness(0, 0, 10, 10) };
            choices.Add((theme, button, selected));
            button.Click += (_, _) =>
            {
                selectedTheme = theme.Id;
                PreviewTheme();
                RefreshSelection();
            };
            tiles.Children.Add(button);
        }
        RefreshSelection();
        content.Children.Add(tiles);
        content.Children.Add(Ui.Text("Glass follows Windows transparency and contrast settings.", 11, "Muted"));
        return content;

        void RefreshSelection()
        {
            foreach (var (theme, tile, mark) in choices)
            {
                var current = theme.Id == selectedTheme;
                mark.Visibility = current ? Visibility.Visible : Visibility.Hidden;
                tile.SetResourceReference(Control.BorderBrushProperty, current ? "Accent" : "Outline");
                tile.SetResourceReference(Control.BackgroundProperty, current ? "StateTint" : "Surface");
                AutomationProperties.SetName(tile, current ? $"{theme.Name} theme, selected" : $"{theme.Name} theme");
            }
        }
    }
    private void PreviewTheme()
    {
        ThemeManager.Apply(selectedTheme, Application.Current.Resources, reducedTransparency: transparency.IsChecked == true);
        WindowEffects.ApplyBackdrop(this);
    }

    private static Border ThemeSwatch(AppTheme theme)
    {
        var accent = ThemeManager.Brush(theme.Accent);
        var card = new StackPanel { Width = 66, Height = 46, Margin = new Thickness(4) };
        var title = new DockPanel { Margin = new Thickness(0, 0, 0, 5) };
        title.Children.Add(new IconControl { Kind = "convert", Ink = accent, Width = 12, Height = 12 });
        title.Children.Add(new Border
        {
            Height = 3,
            Margin = new Thickness(5, 0, 12, 0),
            CornerRadius = new CornerRadius(1.5),
            Background = ThemeManager.Brush(theme.Ink)
        });
        card.Children.Add(title);
        var formats = new System.Windows.Controls.Primitives.UniformGrid { Rows = 1 };
        for (var index = 0; index < 3; index++)
        {
            formats.Children.Add(new Border
            {
                Height = 15,
                Margin = new Thickness(0, 0, 3, 0),
                CornerRadius = new CornerRadius(4),
                Background = ThemeManager.Brush(theme.Raised),
                BorderThickness = new Thickness(1),
                BorderBrush = index == 0 ? accent : ThemeManager.Brush(theme.Outline)
            });
        }
        card.Children.Add(formats);
        return new Border
        {
            Background = ThemeManager.Brush(theme.Chrome),
            CornerRadius = new CornerRadius(10),
            BorderBrush = ThemeManager.Brush(theme.Outline),
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            Padding = new Thickness(2, 6, 0, 0),
            Child = card
        };
    }
    private UIElement ConversionPage()
    {
        var content = Section("Conversion", "Choose default output settings. Existing files are kept and new copies get a numbered suffix.");
        var card = new StackPanel(); card.Children.Add(Ui.Text("Save converted files", 15, weight: FontWeights.SemiBold));
        var hint = Ui.Text("These defaults apply when you open the popup’s options. A quick conversion saves beside the original.", 12, "Muted"); hint.Margin = new Thickness(0, 7, 0, 12); card.Children.Add(hint);
        Detach(folder); var browse = Ui.IconButton("folder", "Choose output folder", (_, _) => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) folder.Text = dialog.FolderName; });
        card.Children.Add(FieldRow(folder, browse)); content.Children.Add(Ui.Card(card));
        var image = new StackPanel { Margin = new Thickness(0, 18, 0, 0) };
        Detach(qualityLabel);
        image.Children.Add(qualityLabel); Detach(quality); image.Children.Add(quality);
        Detach(dpiLabel);
        image.Children.Add(dpiLabel); Detach(dpi); image.Children.Add(dpi);
        Detach(sizeLabel); image.Children.Add(sizeLabel); Detach(size); image.Children.Add(size);
        image.Children.Add(Ui.Text("Image size limits the longer edge and keeps proportions. DPI controls print size. JPEG quality controls compression.", 12, "Muted"));
        content.Children.Add(image); return content;
    }
    private UIElement WindowsPage()
    {
        var content = Section("Windows integration", "Select one file in Explorer or on the desktop, then press Ctrl+K. Other apps keep their own Ctrl+K shortcut.");
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
        var content = Section("Converters", "Native image, text and table conversions work immediately. Add local engines for office documents and media.");
        content.Children.Add(EngineCard("LibreOffice", capabilities.LibreOffice is not null, "Layout-aware Office and PDF export. DOCX and ODT text extraction work with Konvert alone.", office, "soffice.com", "https://www.libreoffice.org/download/download-libreoffice/", "soffice.exe"));
        var second = EngineCard("FFmpeg", capabilities.Ffmpeg is not null, "Audio, video, and WebP. Konvert checks the installed build for the needed encoders and containers.", ffmpeg, "ffmpeg.exe", "https://ffmpeg.org/download.html");
        second.Margin = new Thickness(0, 16, 0, 0); content.Children.Add(second);
        var codecs = new StackPanel();
        codecs.Children.Add(Ui.Text("Windows image codecs", 16, weight: FontWeights.SemiBold));
        var help = Ui.Text("Konvert checks whether Windows can read each WebP, HEIC, HEIF, or AVIF image. Image extensions and codec availability vary by device.", 12, "Muted");
        help.Margin = new Thickness(0, 8, 0, 12);
        codecs.Children.Add(help);
        codecs.Children.Add(Ui.Button("View Windows codec guide", "link", (_, _) =>
            OpenUrl("https://learn.microsoft.com/windows/win32/wic/native-wic-codecs")));
        var codecCard = Ui.Card(codecs);
        codecCard.Margin = new Thickness(0, 16, 0, 0);
        content.Children.Add(codecCard);
        return content;
    }
    private Border EngineCard(string name, bool ready, string description, TextBox box, string executable, string url, string? alternate = null)
    {
        var panel = new StackPanel();
        panel.Children.Add(Ui.Text(name + (ready ? "  ·  Found" : "  ·  Not found"), 16, weight: FontWeights.SemiBold));
        var copy = Ui.Text(description, 12, "Muted"); copy.Margin = new Thickness(0, 8, 0, 14); panel.Children.Add(copy);
        Detach(box); panel.Children.Add(FieldRow(box, Ui.IconButton("folder", "Choose " + executable, (_, _) =>
        {
            var filter = alternate is null ? executable : $"{executable};{alternate}";
            var dialog = new OpenFileDialog { Title = $"Choose {name} executable", Filter = $"{name}|{filter}", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) box.Text = dialog.FileName;
        })));
        var button = Ui.Button("Get " + name, "download", (_, _) => OpenUrl(url)); button.Margin = new Thickness(0, 12, 0, 0); panel.Children.Add(button);
        return Ui.Card(panel);
    }
    private UIElement AboutPage()
    {
        var version = typeof(SettingsWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
        var content = Section("Useful. Quiet. Yours.", $"Konvert {version} · by Shardapps");
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
            ValidateEngine(office.Text, "soffice.com", "soffice.exe"); ValidateEngine(ffmpeg.Text, "ffmpeg.exe");
            Settings = original with
            {
                Theme = selectedTheme,
                JpegQuality = (int)quality.Value,
                Dpi = (int)dpi.Value,
                MaxImageEdge = Ui.ImageSizeLimits[(int)size.Value],
                OutputDirectory = folder.Text,
                LibreOfficePath = string.IsNullOrWhiteSpace(office.Text) ? null : office.Text,
                FfmpegPath = string.IsNullOrWhiteSpace(ffmpeg.Text) ? null : ffmpeg.Text,
                ReducedMotion = motion.IsChecked == true,
                ReducedTransparency = transparency.IsChecked == true
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
    private static void ValidateEngine(string path, params string[] expected)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        if (!Path.IsPathFullyQualified(path) || !File.Exists(path) || !expected.Contains(Path.GetFileName(path), StringComparer.OrdinalIgnoreCase))
            throw new IOException("Choose an existing path to " + string.Join(" or ", expected) + ".");
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
