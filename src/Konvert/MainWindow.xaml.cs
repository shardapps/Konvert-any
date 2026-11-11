using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Konvert;

public partial class MainWindow : Window
{
    internal const double QuickWidth = 264;
    internal const double OptionsWidth = 308;
    private readonly SettingsStore store = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly bool reportToHost;
    private readonly DispatcherTimer progress = new() { Interval = TimeSpan.FromSeconds(1) };
    private PopupAnchor? anchor;
    private ConversionSettings preferences, jobSettings;
    private FormatRegistry catalog = new(EngineCapabilities.None);
    private IReadOnlyList<ConversionOption> options = [];
    private Task? discovery;
    private CancellationTokenSource? conversion;
    private OutsideClicks? outsideClicks;
    private ConversionTarget? activeTarget;
    private Stopwatch? jobTime;
    private TextBlock? elapsed;
    private Button? firstAction;
    private string? input, result, error, selectedExtension;
    private bool advanced, busy, modal, closing, closeAfterCancel, dismissed, discovering;

    public MainWindow(string? initialFile = null, PopupAnchor? anchor = null, bool showSettings = false,
        ConversionSettings? preferences = null, bool reportToHost = false)
    {
        InitializeComponent();
        this.anchor = anchor;
        this.reportToHost = reportToHost;
        this.preferences = preferences ?? store.Load();
        jobSettings = this.preferences with { PdfPage = 1 };
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = initialFile is null;
        Topmost = initialFile is not null;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Title = initialFile is null ? "Konvert" : "Konvert · " + Path.GetFileName(initialFile);
        if (initialFile is not null)
        {
            if (File.Exists(initialFile)) input = Path.GetFullPath(initialFile);
            else error = "This file is unavailable. Choose another file.";
        }
        RefreshOptions();
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsActive) DismissPopup();
        });
        IsVisibleChanged += (_, _) => WatchVisibility();
        ContentScroll.ScrollChanged += (_, e) =>
        {
            if (advanced && IsVisible && Math.Abs(e.ExtentHeightChange) > 1)
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
                {
                    if (!closing && advanced && !busy && result is null && IsVisible)
                        FitToContent(ContentScroll.ExtentHeight);
                });
        };
        progress.Tick += (_, _) => UpdateElapsed();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (busy) conversion?.Cancel();
            else if (advanced && result is null) SetAdvanced(false);
            else Close();
            e.Handled = true;
        };
        DragOver += (_, e) =>
        {
            e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        };
        Drop += (_, e) =>
        {
            if (busy || e.Data.GetData(DataFormats.FileDrop) is not string[] files) return;
            if (files.Length == 1) OpenFile(files[0]);
            else ShowError("Drop one file at a time.");
        };
        Loaded += async (_, _) =>
        {
            this.anchor = WindowEffects.PlacePopup(this, this.anchor, 132);
            Activate();
            if (IsActive) firstAction?.Focus();
            WatchVisibility();
            Animate(Root, 150);
            if (initialFile is null && File.Exists(NativeHostIntegration.HostPath))
            {
                try { NativeHostIntegration.Start(); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
            if (input is not null) await DiscoverAsync();
            if (showSettings && !closing) await OpenSettingsAsync();
        };
    }

    private void WatchVisibility()
    {
        if (!IsLoaded || !IsVisible)
        {
            outsideClicks?.Dispose();
            outsideClicks = null;
            progress.Stop();
            return;
        }
        outsideClicks ??= new OutsideClicks(this, DismissPopup);
        if (busy) progress.Start();
    }

    internal void DismissPopup()
    {
        if (closing || modal || dismissed) return;
        if (!busy) { Close(); return; }
        dismissed = true;
        Hide();
        if (reportToHost) NativeHostIntegration.PopupWasDismissed();
    }

    private Task DiscoverAsync() => discovery ??= DiscoverCoreAsync();

    private async Task DiscoverCoreAsync()
    {
        var token = lifetime.Token;
        discovering = true;
        if (!busy) RenderView();
        try
        {
            var capabilities = await Task.Run(() => EngineCapabilities.DetectAsync(preferences, token, input), token);
            if (!closing) catalog = new(capabilities);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!closing) error = ex.Message; }
        finally
        {
            discovering = false;
            if (!closing) RefreshOptions();
        }
    }

    private void RefreshOptions()
    {
        options = input is null ? [] : catalog.GetOptions(input);
        if (!busy) RenderView();
    }

    private void RenderView()
    {
        var keepFocus = IsKeyboardFocusWithin;
        Header.Content = Footer.Content = null;
        Body.Children.Clear();
        elapsed = null;
        firstAction = null;
        ContentScroll.ScrollToTop();
        Width = advanced && !busy && result is null ? OptionsWidth : QuickWidth;
        if (busy) BuildProgress();
        else if (result is not null) BuildResult();
        else if (input is null) BuildHome();
        else if (advanced) BuildOptions();
        else BuildQuick();
        FitToContent();
        if (keepFocus && IsActive) firstAction?.Focus();
    }

    private void BuildHome()
    {
        Header.Content = TitleRow("convert", "Konvert", "Local file conversion",
            SmallIcon("close", "Close picker", (_, _) => Close()));
        if (error is not null) Body.Children.Add(Message(error));
        Body.Children.Add(Message("Drop a file here, or select one in Explorer and press Ctrl+K."));
        firstAction = Action("Choose a file", "folder", (_, _) => PickFile(), primary: true);
        Footer.Content = Actions(firstAction,
            SmallIcon("settings", "Open settings", async (_, _) => await OpenSettingsAsync()));
    }

    private void BuildQuick()
    {
        var extension = FormatRegistry.SourceExtension(input!);
        Header.Content = TitleRow(Ui.FormatIcon(extension), Path.GetFileName(input!),
            catalog.Capabilities.PdfPageCount is { } count
                ? $"PDF · {count} {(count == 1 ? "page" : "pages")}"
                : extension.Length == 0 ? "Selected file" :
                    options.Count > 0 ? Ui.FormatName(extension) : $"{extension.ToUpperInvariant()} file",
            SmallIcon("close", "Close picker", (_, _) => Close()));
        if (error is not null) Body.Children.Add(Message(error));
        var quick = options.Where(option => option.Available).Take(3).ToArray();
        if (quick.Length == 0)
        {
            var unavailable = options.Count > 0;
            Body.Children.Add(Message(discovering ? "Checking available formats…"
                : unavailable ? options[0].UnavailableReason ?? "This file needs a converter."
                : "There’s no export for this file type yet."));
            firstAction = Action("Choose another file", "folder", (_, _) => PickFile(), primary: !unavailable);
            Footer.Content = Actions(firstAction,
                unavailable ? Action("Options", "next", (_, _) => SetAdvanced(true)) : null);
            return;
        }

        var row = new UniformGrid { Rows = 1, Columns = quick.Length, Margin = new Thickness(-2, 0, -2, 0) };
        foreach (var option in quick)
        {
            var target = option.Target;
            var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            content.Children.Add(Badge(Ui.FormatIcon(target.Extension), 25, 14));
            var caption = Ui.Text(target.Label, 12, weight: FontWeights.SemiBold);
            caption.Margin = new Thickness(0, 2, 0, 0);
            caption.TextAlignment = TextAlignment.Center;
            content.Children.Add(caption);
            var button = new Button
            {
                Content = content,
                Height = 55,
                Margin = new Thickness(2, 0, 2, 0),
                Padding = new Thickness(2),
                ToolTip = $"{Ui.FormatName(target.Extension)}\n{target.Detail}\nSaves next to the original."
            };
            AutomationProperties.SetName(button, "Convert to " + target.Label);
            button.Click += async (_, _) => await ConvertAsync(target, new ConversionSettings());
            firstAction ??= button;
            row.Children.Add(button);
        }
        Body.Children.Add(row);
        var saveHint = Ui.Text("Save beside original", 11, "Muted");
        saveHint.VerticalAlignment = VerticalAlignment.Center;
        Footer.Content = Actions(saveHint, Action("Options", "next", (_, _) => SetAdvanced(true)));
    }

    private void BuildOptions()
    {
        var back = SmallIcon("back", "Back to quick formats", (_, _) => SetAdvanced(false));
        Header.Content = TitleRow("convert", "Conversion options", Path.GetFileName(input!),
            SmallIcon("settings", "Open settings", async (_, _) => await OpenSettingsAsync()), back);
        if (error is not null) Body.Children.Add(Message(error));
        Body.Children.Add(SectionLabel("Format"));
        var formats = new WrapPanel { Margin = new Thickness(-2, 2, 0, 2) };
        var choices = new List<(ConversionOption Option, Button Chip)>();
        var tuning = new StackPanel();
        var selected = options.FirstOrDefault(option => option.Target.Extension == selectedExtension)
            ?? options.FirstOrDefault(option => option.Available) ?? options.FirstOrDefault();
        var convert = Action("Convert", "convert", async (_, _) =>
        {
            if (selected?.Available == true) await ConvertAsync(selected.Target, jobSettings);
        }, primary: true);
        convert.IsDefault = true;
        Footer.Content = convert;
        Body.Children.Add(formats);
        Body.Children.Add(tuning);
        foreach (var option in options)
        {
            var chip = new Button
            {
                Content = Ui.Text(option.Target.Label, 12, option.Available ? "Ink" : "Muted", FontWeights.SemiBold),
                MinWidth = 49,
                Height = 32,
                Margin = new Thickness(2, 0, 3, 5),
                Padding = new Thickness(7, 2, 7, 2),
                ToolTip = option.Available ? option.Target.Detail : option.UnavailableReason
            };
            AutomationProperties.SetName(chip, "Select " + option.Target.Label);
            chip.Click += (_, _) =>
            {
                selected = option;
                UpdateSelection();
                FitToContent();
            };
            choices.Add((option, chip));
            if (option == selected) firstAction = chip;
            formats.Children.Add(chip);
        }
        UpdateSelection();

        void UpdateSelection()
        {
            tuning.Children.Clear();
            selectedExtension = selected?.Target.Extension;
            foreach (var (option, chip) in choices)
            {
                var active = option == selected;
                chip.SetResourceReference(Control.BorderBrushProperty, active ? "Accent" : "Outline");
                chip.SetResourceReference(Control.BackgroundProperty, active ? "StateTint" : "Raised");
                AutomationProperties.SetItemStatus(chip, active ? "Selected" : option.Available ? "Ready" : "Unavailable");
            }
            convert.IsEnabled = selected?.Available == true;
            if (convert.Content is StackPanel content && content.Children.OfType<TextBlock>().FirstOrDefault() is { } caption)
                caption.Text = selected is null ? "Convert" : "Convert to " + selected.Target.Label;
            AutomationProperties.SetName(convert, selected is null ? "Convert" : "Convert with options to " + selected.Target.Label);
            if (selected is null)
            {
                tuning.Children.Add(Message(discovering ? "Checking available formats…" : "There’s no export for this file type yet."));
                return;
            }
            if (!selected.Available)
            {
                tuning.Children.Add(Message(selected.UnavailableReason ?? "This format is unavailable."));
                if (selected.Target.Engine is "LibreOffice" or "FFmpeg" or "Windows image codec")
                    tuning.Children.Add(Action("Converter settings", "settings", async (_, _) => await OpenSettingsAsync("engines")));
                return;
            }
            BuildTuning(tuning, selected.Target, convert);
        }
    }

    private void BuildTuning(Panel panel, ConversionTarget target, Button convert)
    {
        panel.Children.Add(Message(target.Detail));
        if (target.SupportsPdfPage) AddPdfPage(panel, convert);
        if (target.SupportsJpegQuality)
            AddSlider(panel, "JPEG quality", 10, 100, jobSettings.JpegQuality, value => $"{value}%",
                value => jobSettings = jobSettings with { JpegQuality = value });
        if (target.SupportsDpi)
            AddSlider(panel, "Resolution", 72, 600, jobSettings.Dpi, value => $"{value} DPI",
                value => jobSettings = jobSettings with { Dpi = value });
        if (target.SupportsImageSize)
            AddSlider(panel, "Image size", 0, Ui.ImageSizeLimits.Count - 1, Ui.ImageSizeIndex(jobSettings.MaxImageEdge),
                Ui.ImageSizeLabel, index => jobSettings = jobSettings with { MaxImageEdge = Ui.ImageSizeLimits[index] });
        var destination = SectionLabel("Save to");
        destination.Margin = new Thickness(0, 8, 0, 4);
        panel.Children.Add(destination);
        var path = Ui.Text(jobSettings.OutputDirectory ?? "Same folder as the original", 11, "Muted");
        path.TextWrapping = TextWrapping.NoWrap;
        path.TextTrimming = TextTrimming.CharacterEllipsis;
        path.ToolTip = jobSettings.OutputDirectory;
        panel.Children.Add(path);
        var reset = jobSettings.OutputDirectory is not null ? Action("Use original", "back", (_, _) =>
        {
            jobSettings = jobSettings with { OutputDirectory = null };
            RenderView();
        }) : null;
        panel.Children.Add(Actions(Action("Choose folder", "folder", (_, _) => ChooseFolder()), reset));
    }

    private void AddPdfPage(Panel panel, Button convert)
    {
        var maximum = Math.Min(catalog.Capabilities.PdfPageCount ?? 10_000, 10_000);
        var page = new TextBox { Width = 60, Height = 34, Padding = new Thickness(7, 5, 7, 5), MaxLength = 5, Text = jobSettings.PdfPage.ToString(CultureInfo.InvariantCulture) };
        AutomationProperties.SetName(page, "PDF page");
        var hint = SectionLabel("Page");
        hint.VerticalAlignment = VerticalAlignment.Center;
        panel.Children.Add(Actions(hint, page));
        page.TextChanged += (_, _) => Update();
        Update();

        void Update()
        {
            var valid = int.TryParse(page.Text, NumberStyles.None, CultureInfo.InvariantCulture, out var value) &&
                value >= 1 && value <= maximum;
            if (valid) jobSettings = jobSettings with { PdfPage = value };
            convert.IsEnabled = valid;
            hint.Text = valid && catalog.Capabilities.PdfPageCount is null ? "Page" : $"Page · 1–{maximum}";
        }
    }

    private void BuildProgress()
    {
        Header.Content = TitleRow("convert", "Converting to " + activeTarget!.Label, Path.GetFileName(input!));
        elapsed = Message("");
        AutomationProperties.SetLiveSetting(elapsed, AutomationLiveSetting.Polite);
        Body.Children.Add(elapsed);
        UpdateElapsed();
        firstAction = Action("Cancel conversion", "close", (_, _) => conversion?.Cancel());
        Footer.Content = firstAction;
    }

    private void BuildResult()
    {
        Header.Content = TitleRow("check", "File saved", Path.GetFileName(result!),
            SmallIcon("close", "Close picker", (_, _) => Close()));
        Body.Children.Add(Message(Path.GetDirectoryName(result!) == Path.GetDirectoryName(input!)
            ? "Saved beside the original." : Path.GetDirectoryName(result!)!));
        if (error is not null) Body.Children.Add(Message(error));
        firstAction = Action("Show in Explorer", "folder", (_, _) => Reveal(), primary: true, name: "Show converted file");
        Footer.Content = Actions(firstAction,
            Action("Formats", "back", (_, _) => { result = null; error = null; RenderView(); }, name: "Back to formats"));
    }

    private void UpdateElapsed()
    {
        if (elapsed is not null && activeTarget is not null && jobTime is not null)
            elapsed.Text = $"{activeTarget.Engine} · {(int)jobTime.Elapsed.TotalSeconds}s elapsed";
    }

    private void SetAdvanced(bool value)
    {
        advanced = value;
        RenderView();
        Animate(Body, 130);
    }

    private void FitToContent(double? measuredBodyHeight = null)
    {
        var border = Root.BorderThickness;
        var margin = Layout.Margin;
        var contentWidth = Width - border.Left - border.Right - margin.Left - margin.Right;
        var available = new Size(contentWidth, double.PositiveInfinity);
        Header.Measure(available);
        Body.Measure(available);
        Footer.Measure(available);
        var contentHeight = Header.DesiredSize.Height + (measuredBodyHeight ?? Body.DesiredSize.Height) + Footer.DesiredSize.Height;
        Height = Math.Clamp(contentHeight + border.Top + border.Bottom + margin.Top + margin.Bottom + 3, 90, 505);
        if (IsLoaded && IsVisible) anchor = WindowEffects.PlacePopup(this, anchor, 132);
    }

    private void Animate(FrameworkElement visual, int duration)
    {
        if (!IsLoaded || preferences.ReducedMotion || SystemParameters.HighContrast || !SystemParameters.ClientAreaAnimation) return;
        visual.BeginAnimation(OpacityProperty, new DoubleAnimation(0.38, 1, TimeSpan.FromMilliseconds(duration))
        {
            FillBehavior = FillBehavior.Stop,
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private async Task ConvertAsync(ConversionTarget target, ConversionSettings settings)
    {
        if (input is null || busy || closing) return;
        error = result = null;
        using var job = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        conversion = job;
        activeTarget = target;
        jobTime = Stopwatch.StartNew();
        busy = true;
        if (IsVisible) progress.Start();
        RenderView();
        try
        {
            using var service = new ConversionService(catalog.Capabilities);
            result = (await service.ConvertAsync(input, target.Extension, settings, job.Token)).OutputPath;
        }
        catch (OperationCanceledException) when (job.IsCancellationRequested) { error = "Conversion cancelled. You can choose a format again."; }
        catch (Exception ex) { error = ex.Message; }
        finally
        {
            busy = false;
            conversion = null;
            activeTarget = null;
            progress.Stop();
            if (dismissed)
            {
                if (reportToHost && !job.IsCancellationRequested) NativeHostIntegration.ReportBackgroundResult(result is not null);
                Close();
            }
            else if (closeAfterCancel) Close();
            else RenderView();
        }
        if (result is { } saved && !closing && !advanced)
        {
            try { await Task.Delay(3000, lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (!closing && result == saved && !busy) Close();
        }
    }

    private void ChooseFolder()
    {
        modal = true;
        try
        {
            var dialog = new OpenFolderDialog { Title = "Choose an output folder" };
            if (dialog.ShowDialog(this) == true)
            {
                jobSettings = jobSettings with { OutputDirectory = dialog.FolderName };
                RenderView();
            }
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { modal = false; }
    }

    private void PickFile()
    {
        modal = true;
        try
        {
            var dialog = new OpenFileDialog { Title = "Choose a file to convert", CheckFileExists = true };
            if (dialog.ShowDialog(this) == true) OpenFile(dialog.FileName);
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally { modal = false; }
    }

    private void OpenFile(string path)
    {
        var next = new MainWindow(path, preferences: preferences, reportToHost: reportToHost);
        next.Show();
        Close();
    }

    private async Task OpenSettingsAsync(string section = "appearance")
    {
        if (modal || closing) return;
        modal = true;
        var wasTopmost = Topmost;
        Topmost = false;
        try
        {
            var engines = await Task.Run(() => EngineCapabilities.Locate(preferences));
            if (closing) return;
            var dialog = new SettingsWindow(preferences, engines, section) { Owner = this };
            if (dialog.ShowDialog() != true) return;
            store.Save(dialog.Settings);
            preferences = dialog.Settings;
            jobSettings = preferences with { PdfPage = jobSettings.PdfPage };
            if (discovery is not null) await discovery;
            discovery = null;
            if (input is not null) await DiscoverAsync();
            else RenderView();
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            modal = false;
            if (!closing) Topmost = wasTopmost;
            ThemeManager.Apply(preferences.Theme, Application.Current.Resources, reducedTransparency: preferences.ReducedTransparency);
        }
    }

    private void Reveal()
    {
        if (result is null) return;
        try
        {
            var start = new ProcessStartInfo("explorer.exe") { UseShellExecute = true };
            start.ArgumentList.Add("/select," + result);
            Process.Start(start)?.Dispose();
            Close();
        }
        catch (Exception ex) { ShowError(ex.Message); }
    }

    private void ShowError(string message)
    {
        error = message;
        RenderView();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            closeAfterCancel = true;
            conversion?.Cancel();
        }
        else closing = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        progress.Stop();
        outsideClicks?.Dispose();
        outsideClicks = null;
        lifetime.Cancel();
        lifetime.Dispose();
        base.OnClosed(e);
    }

    private static DockPanel TitleRow(string icon, string title, string subtitle, UIElement? trailing = null, FrameworkElement? leading = null)
    {
        var row = new DockPanel();
        if (trailing is not null) { DockPanel.SetDock(trailing, Dock.Right); row.Children.Add(trailing); }
        var marker = leading ?? Badge(icon, 34, 18);
        marker.Margin = new Thickness(0, 0, 8, 0);
        row.Children.Add(marker);
        var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(TitleText(title, 14, "Ink", FontWeights.SemiBold));
        text.Children.Add(TitleText(subtitle, 11, "Muted", FontWeights.Normal));
        row.Children.Add(text);
        return row;
    }

    private static TextBlock TitleText(string value, int size, string color, FontWeight weight)
    {
        var text = Ui.Text(value, size, color, weight);
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        text.ToolTip = value;
        return text;
    }

    private static Border Badge(string icon, double diameter, double size)
    {
        var badge = new Border
        {
            Width = diameter,
            Height = diameter,
            CornerRadius = new CornerRadius(diameter / 2),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = Ui.Icon(icon, size, "Accent")
        };
        badge.SetResourceReference(Border.BackgroundProperty, "CoreFill");
        return badge;
    }

    private static Button Action(string text, string icon, RoutedEventHandler click, bool primary = false, string? name = null)
    {
        var button = Ui.Button(text, icon, click, primary);
        button.Padding = new Thickness(9, 7, 9, 7);
        if (name is not null) AutomationProperties.SetName(button, name);
        return button;
    }

    private static Button SmallIcon(string icon, string text, RoutedEventHandler click)
    {
        var button = Ui.IconButton(icon, text, click);
        button.Width = button.Height = 32;
        button.Padding = new Thickness(5);
        button.Background = button.BorderBrush = Brushes.Transparent;
        return button;
    }

    private static DockPanel Actions(UIElement first, UIElement? last)
    {
        var row = new DockPanel { Margin = new Thickness(0, 4, 0, 0) };
        if (last is not null)
        {
            if (last is FrameworkElement element) element.Margin = new Thickness(6, 0, 0, 0);
            DockPanel.SetDock(last, Dock.Right);
            row.Children.Add(last);
        }
        row.Children.Add(first);
        return row;
    }

    private static TextBlock SectionLabel(string value) => Ui.Text(value, 12, "Ink", FontWeights.SemiBold);

    private static TextBlock Message(string value)
    {
        var message = Ui.Text(value.Length > 320 ? value[..317] + "…" : value, 12, "Muted");
        message.Margin = new Thickness(0, 2, 0, 7);
        if (value.Length > 100) message.ToolTip = value;
        return message;
    }

    private static void AddSlider(Panel panel, string name, int min, int max, int value, Func<int, string> format, Action<int> update)
    {
        var label = Ui.Text($"{name} · {format(value)}", 11, "Muted");
        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            Value = value,
            IsSnapToTickEnabled = true,
            TickFrequency = 1,
            Margin = new Thickness(0, 0, 0, 4)
        };
        AutomationProperties.SetName(slider, name);
        AutomationProperties.SetHelpText(slider, format(value));
        slider.ValueChanged += (_, e) =>
        {
            var next = (int)e.NewValue;
            label.Text = $"{name} · {format(next)}";
            AutomationProperties.SetHelpText(slider, format(next));
            update(next);
        };
        panel.Children.Add(label);
        panel.Children.Add(slider);
    }
}
