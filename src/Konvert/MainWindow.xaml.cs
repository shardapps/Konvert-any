using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;

namespace Konvert;

public partial class MainWindow : Window
{
    private readonly SettingsStore store = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly bool popup;
    private readonly PopupAnchor? anchor;
    private readonly List<Button> petals = [];
    private ConversionSettings settings;
    private EngineCapabilities capabilities = new(null, null, new HashSet<string>());
    private ConversionService? service;
    private IReadOnlyList<ConversionOption> options = [];
    private Canvas? orbit;
    private StackPanel? center;
    private TextBlock? status;
    private TextBlock? pageLabel;
    private StackPanel? pages;
    private string? input;
    private string? result;
    private int page;
    private bool busy, detecting, modal, activated, closingAfterCancel;
    private CancellationTokenSource? operation;

    public MainWindow(string? initialFile = null, PopupAnchor? anchor = null, bool showSettings = false, ConversionSettings? preferences = null)
    {
        InitializeComponent();
        this.anchor = anchor;
        popup = initialFile is not null;
        settings = preferences ?? store.Load();
        ThemeManager.Apply(settings.Theme, Application.Current.Resources);
        if (popup)
        {
            Width = Height = 560; AllowsTransparency = true; Background = Brushes.Transparent;
            ShowInTaskbar = true; Topmost = true; WindowStartupLocation = WindowStartupLocation.Manual;
            Title = "Konvert · quick convert"; BuildPopup();
        }
        else
        {
            Width = 790; Height = 560; ShowInTaskbar = true; BuildHome();
        }
        SourceInitialized += (_, _) => { if (!popup) WindowEffects.ApplyBackdrop(this); };
        Activated += (_, _) => activated = true;
        Deactivated += (_, _) => { if (popup && activated && !busy && !modal) Close(); };
        PreviewKeyDown += OnKeyDown;
        DragOver += (_, e) => { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; };
        Drop += (_, e) =>
        {
            if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            if (files.Length != 1) { SetStatus("Choose one file at a time."); return; }
            OpenFile(files[0]);
        };
        Loaded += async (_, _) =>
        {
            try { if (File.Exists(NativeHostIntegration.HostPath)) NativeHostIntegration.Start(); }
            catch (Exception ex) { SetStatus(ex.Message); }
            if (initialFile is not null) SelectFile(initialFile);
            await RefreshEnginesAsync();
            if (popup) WindowEffects.PlacePopup(this, anchor);
            if (showSettings) await OpenSettingsAsync();
        };
    }

    private void BuildHome()
    {
        var shell = new Grid { Margin = new Thickness(1) };
        shell.RowDefinitions.Add(new() { Height = GridLength.Auto });
        shell.RowDefinitions.Add(new());
        shell.RowDefinitions.Add(new() { Height = GridLength.Auto });
        var header = new DockPanel { Margin = new Thickness(30, 23, 22, 0) };
        header.MouseLeftButtonDown += (_, e) => { if (e.OriginalSource is TextBlock or DockPanel) DragMove(); };
        var controls = new StackPanel { Orientation = Orientation.Horizontal };
        controls.Children.Add(Ui.IconButton("settings", "Settings", async (_, _) => await OpenSettingsAsync()));
        controls.Children.Add(Ui.IconButton("close", "Close window", (_, _) => Close()));
        DockPanel.SetDock(controls, Dock.Right); header.Children.Add(controls);
        var brand = new StackPanel { Orientation = Orientation.Horizontal };
        brand.Children.Add(Ui.Card(Ui.Icon("convert", 25, "Accent"), 15, new Thickness(10)));
        var brandText = new StackPanel { Margin = new Thickness(12, 1, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        brandText.Children.Add(Ui.Text("Konvert", 21, weight: FontWeights.SemiBold));
        brandText.Children.Add(Ui.Text("SHARDAPPS", 9, "Muted", FontWeights.SemiBold)); brand.Children.Add(brandText);
        header.Children.Add(brand); shell.Children.Add(header);
        var content = new Grid { Margin = new Thickness(36, 30, 36, 20) };
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1.35, GridUnitType.Star) });
        content.ColumnDefinitions.Add(new() { Width = new GridLength(1, GridUnitType.Star) });
        var left = new StackPanel { Margin = new Thickness(0, 0, 24, 0) };
        left.Children.Add(Ui.Text("A different format.\nThe same flow.", 32, weight: FontWeights.SemiBold));
        var intro = Ui.Text("Select a file. Choose its next form.\nEverything stays on your device.", 14, "Muted"); intro.Margin = new Thickness(0, 12, 0, 24); left.Children.Add(intro);
        var drop = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        drop.Children.Add(Ui.Icon("file", 32, "Accent"));
        var dropText = Ui.Text("Drop a file here", 16, weight: FontWeights.SemiBold); dropText.Margin = new Thickness(0, 12, 0, 5); drop.Children.Add(dropText);
        drop.Children.Add(Ui.Text("or choose one to get started", 12, "Muted"));
        var browse = Ui.Button("Choose file", "folder", (_, _) => PickFile(), true); browse.Margin = new Thickness(0, 18, 0, 0); drop.Children.Add(browse);
        left.Children.Add(Ui.Card(drop, 28, new Thickness(24))); content.Children.Add(left);
        var right = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var shortcut = new StackPanel();
        shortcut.Children.Add(Ui.Icon("keyboard", 25, "Accent"));
        var shortcutTitle = Ui.Text("Right where you work.", 18, weight: FontWeights.SemiBold); shortcutTitle.Margin = new Thickness(0, 16, 0, 10); shortcut.Children.Add(shortcutTitle);
        shortcut.Children.Add(Ui.Text("Select a file in Explorer or on your desktop, then press", 13, "Muted"));
        var keys = new TextBlock { Text = "Ctrl  +  K", FontSize = 24, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 16, 0, 16) }; keys.SetResourceReference(TextBlock.ForegroundProperty, "Accent"); shortcut.Children.Add(keys);
        shortcut.Children.Add(Ui.Text("A compact ring of formats opens around your selection.", 13, "Muted"));
        var enable = Ui.Button("Enable shortcut", "check", (_, _) =>
        {
            try { NativeHostIntegration.Start(); SetStatus("Ctrl+K is ready in Explorer and on the desktop."); }
            catch (Exception ex) { SetStatus(ex.Message); }
        }); enable.Margin = new Thickness(0, 18, 0, 0); shortcut.Children.Add(enable);
        right.Children.Add(Ui.Card(shortcut, 26)); Grid.SetColumn(right, 1); content.Children.Add(right); Grid.SetRow(content, 1); shell.Children.Add(content);
        status = Ui.Text("A small native listener keeps Ctrl+K ready. Quit it from the tray to stop.", 11, "Muted");
        status.Margin = new Thickness(36, 0, 36, 22); Grid.SetRow(status, 2); shell.Children.Add(status);
        Root.Children.Add(shell);
    }

    private void BuildPopup()
    {
        orbit = new Canvas { Width = 560, Height = 560 };
        var ring = new System.Windows.Shapes.Ellipse { Width = 346, Height = 346, StrokeThickness = 1, Opacity = 0.22 };
        ring.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Accent"); Canvas.SetLeft(ring, 107); Canvas.SetTop(ring, 107); orbit.Children.Add(ring);
        center = new StackPanel { VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
        var source = Ui.Card(center, 76, new Thickness(16)); source.Width = source.Height = 148;
        Canvas.SetLeft(source, 206); Canvas.SetTop(source, 206); orbit.Children.Add(source);
        status = Ui.Text("Checking formats…", 11, "Muted"); status.TextAlignment = TextAlignment.Center; status.Width = 122;
        var toolbar = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        toolbar.Children.Add(Ui.IconButton("folder", "Output folder", async (_, _) => await OpenSettingsAsync("conversion")));
        toolbar.Children.Add(Ui.IconButton("settings", "Settings", async (_, _) => await OpenSettingsAsync()));
        toolbar.Children.Add(Ui.IconButton("close", "Dismiss popup (Escape)", (_, _) => { if (busy) operation?.Cancel(); else Close(); }));
        var dock = Ui.Card(toolbar, 26, new Thickness(6)); Canvas.SetLeft(dock, 211); Canvas.SetTop(dock, 497); orbit.Children.Add(dock);
        pages = new StackPanel { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
        pages.Children.Add(Ui.IconButton("back", "Previous formats", (_, _) => { if (!busy) { page = Math.Max(0, page - 1); RenderPetals(); } }));
        pageLabel = Ui.Text("", 11, "Muted"); pageLabel.VerticalAlignment = VerticalAlignment.Center; pageLabel.Margin = new Thickness(9, 0, 9, 0); pages.Children.Add(pageLabel);
        pages.Children.Add(Ui.IconButton("arrow", "More formats", (_, _) => { if (!busy) { page = Math.Min((options.Count - 1) / 6, page + 1); RenderPetals(); } }));
        Canvas.SetLeft(pages, 218); Canvas.SetTop(pages, 18); orbit.Children.Add(pages);
        Root.Children.Add(new Viewbox { Child = orbit, Stretch = Stretch.Uniform });
    }

    private void OpenFile(string path)
    {
        if (popup) { SelectFile(path); return; }
        var next = new MainWindow(path); next.Show(); Close();
    }

    private void SelectFile(string path)
    {
        if (busy) return;
        try
        {
            if (!File.Exists(path)) throw new FileNotFoundException("This file is unavailable. Choose a local file and try again.");
            input = Path.GetFullPath(path); result = null; page = 0;
            Title = "Konvert · " + Path.GetFileName(input);
            UpdateTargets();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException) { SetStatus(ex.Message); }
    }

    private async Task RefreshEnginesAsync()
    {
        detecting = true;
        foreach (var petal in petals) petal.IsEnabled = false;
        try
        {
            var token = lifetime.Token;
            capabilities = await Task.Run(() => EngineCapabilities.DetectAsync(settings, token), token);
            service?.Dispose(); service = new(capabilities);
            if (input is not null) UpdateTargets();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus("Could not check converters: " + ex.Message); }
        finally { detecting = false; foreach (var petal in petals) petal.IsEnabled = !busy; }
    }

    private void UpdateTargets()
    {
        if (input is null || center is null) return;
        options = new FormatRegistry(capabilities).GetOptions(input);
        page = Math.Min(page, Math.Max(0, (options.Count - 1) / 6));
        center.Children.Clear(); center.Children.Add(Ui.Icon(IconControl.ForExtension(Path.GetExtension(input).TrimStart('.')), 28, "Accent"));
        var format = Ui.Text(Path.GetExtension(input).TrimStart('.').ToUpperInvariant(), 18, weight: FontWeights.SemiBold);
        format.HorizontalAlignment = HorizontalAlignment.Center; format.Margin = new Thickness(0, 8, 0, 2); center.Children.Add(format);
        var name = Ui.Text(Path.GetFileName(input), 11, "Muted"); name.Width = 116; name.TextWrapping = TextWrapping.NoWrap; name.TextTrimming = TextTrimming.CharacterEllipsis; name.TextAlignment = TextAlignment.Center; name.ToolTip = input; center.Children.Add(name);
        status!.Text = options.Count == 0 ? "No route yet" : "Choose a format";
        status.Margin = new Thickness(0, 7, 0, 0); center.Children.Add(status);
        RenderPetals();
    }

    private void RenderPetals()
    {
        if (orbit is null) return;
        foreach (var button in petals) orbit.Children.Remove(button);
        petals.Clear();
        var visible = options.Skip(page * 6).Take(6).ToArray();
        for (var i = 0; i < visible.Length; i++)
        {
            var option = visible[i];
            var target = option.Target;
            var angle = (-90 + i * 360.0 / visible.Length) * Math.PI / 180;
            var content = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            content.Children.Add(Ui.Icon(option.Available ? IconControl.ForExtension(target.Extension) : "download", 25, option.Available ? "Accent" : "Muted"));
            var label = Ui.Text(target.Label, 14, weight: FontWeights.SemiBold); label.HorizontalAlignment = HorizontalAlignment.Center; label.Margin = new Thickness(0, 8, 0, 2); content.Children.Add(label);
            var hint = Ui.Text(option.Available ? "Convert" : "Add engine", 9, "Muted"); hint.HorizontalAlignment = HorizontalAlignment.Center; content.Children.Add(hint);
            var button = new Button { Width = 94, Height = 96, Content = content, Padding = new Thickness(10), ToolTip = option.Available ? target.Detail : "Enable " + target.Engine + " in Settings", IsEnabled = !busy && !detecting };
            AutomationProperties.SetName(button, option.Available ? "Convert to " + target.Label : "Enable " + target.Label + " with " + target.Engine);
            button.Click += async (_, _) => { if (option.Available) await ConvertAsync(target); else await OpenSettingsAsync("engines"); };
            Canvas.SetLeft(button, 280 + Math.Cos(angle) * 177 - 47); Canvas.SetTop(button, 280 + Math.Sin(angle) * 177 - 48);
            orbit.Children.Add(button); petals.Add(button);
            if (!settings.ReducedMotion && SystemParameters.ClientAreaAnimation)
            {
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180 + 18 * i)));
                var move = new TranslateTransform(Math.Cos(angle) * -12, Math.Sin(angle) * -12); button.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }
        pages!.Visibility = options.Count > 6 ? Visibility.Visible : Visibility.Collapsed;
        pageLabel!.Text = $"{page + 1}/{Math.Max(1, (options.Count + 5) / 6)}";
    }

    private async Task ConvertAsync(ConversionTarget target)
    {
        if (input is null || service is null || busy || detecting) return;
        busy = true; operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        foreach (var petal in petals) petal.IsEnabled = false;
        SetStatus("Converting…");
        try
        {
            var converted = await service.ConvertAsync(input, target.Extension, settings, operation.Token);
            result = converted.OutputPath;
            SetStatus("Saved · " + converted.Elapsed.TotalSeconds.ToString("0.00") + "s");
            if (center is not null)
            {
                center.Children.RemoveAt(0);
                var reveal = Ui.IconButton("check", "Show converted file", (_, _) => Reveal());
                center.Children.Insert(0, reveal);
            }
        }
        catch (OperationCanceledException) { SetStatus("Cancelled"); }
        catch (Exception ex) { SetStatus("Could not convert. Open settings or try another format."); if (status is not null) status.ToolTip = ex.Message; }
        finally
        {
            busy = false; operation.Dispose(); operation = null;
            foreach (var petal in petals) petal.IsEnabled = true;
            if (closingAfterCancel) Close();
        }
    }

    private async Task OpenSettingsAsync(string section = "appearance")
    {
        if (busy || detecting) return;
        modal = true;
        try
        {
            var dialog = new SettingsWindow(settings, capabilities, section) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                store.Save(dialog.Settings); settings = dialog.Settings;
                await RefreshEnginesAsync();
            }
        }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally { modal = false; ThemeManager.Apply(settings.Theme, Application.Current.Resources); WindowEffects.ApplyBackdrop(this); }
    }

    private void PickFile()
    {
        if (busy) return;
        modal = true;
        try { var dialog = new OpenFileDialog { Title = "Choose a file to Konvert", CheckFileExists = true }; if (dialog.ShowDialog(this) == true) OpenFile(dialog.FileName); }
        finally { modal = false; }
    }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (busy) operation?.Cancel(); else Close(); e.Handled = true; }
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) { PickFile(); e.Handled = true; }
    }
    private void SetStatus(string message) { if (status is not null) status.Text = message; }
    private void Reveal()
    {
        if (result is null) return;
        var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
        start.ArgumentList.Add("/select," + result);
        try { Process.Start(start); } catch (Exception ex) { SetStatus(ex.Message); }
    }
    protected override void OnClosing(CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; closingAfterCancel = true; operation?.Cancel(); }
        else { lifetime.Cancel(); service?.Dispose(); lifetime.Dispose(); }
        base.OnClosing(e);
    }
}
