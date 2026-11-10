using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Ellipse = System.Windows.Shapes.Ellipse;
using Shape = System.Windows.Shapes.Shape;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Konvert;

public partial class MainWindow : Window
{
    internal const double RingSize = 224;
    private const int QuickSlots = 3;
    private readonly SettingsStore store = new();
    private readonly CancellationTokenSource lifetime = new();
    private readonly bool filePopup;
    private readonly bool reportToHost;
    private readonly DispatcherTimer progress = new() { Interval = TimeSpan.FromSeconds(1) };
    private PopupAnchor? anchor;
    private readonly Canvas ring = new() { Width = RingSize, Height = RingSize };
    private readonly StackPanel drawer = new();
    private readonly Border drawerCard;
    private readonly Border statusCard;
    private readonly WrapPanel extraFormats = new();
    private readonly List<Button> quickButtons = [];
    private readonly Button center;
    private readonly Button expand;
    private readonly IconControl expandIcon;
    private readonly IconControl centerIcon;
    private readonly TextBlock expandLabel;
    private readonly TextBlock sourceLabel;
    private readonly TextBlock hint;
    private readonly TextBlock details;
    private readonly TextBlock status;
    private readonly TextBlock dropHint;
    private readonly TextBlock shortcutHint;
    private readonly Button folderButton;
    private readonly Button resetFolder;
    private readonly FrameworkElement qualityOptions;
    private readonly FrameworkElement dpiOptions;
    private readonly Slider qualitySlider;
    private readonly Slider dpiSlider;
    private ConversionSettings preferences;
    private ConversionSettings jobSettings;
    private FormatRegistry catalog = new(EngineCapabilities.None);
    private IReadOnlyList<ConversionOption> options = [];
    private Task? discovery;
    private CancellationTokenSource? conversion;
    private OutsideClicks? outsideClicks;
    private ConversionTarget? activeTarget;
    private Stopwatch? jobTime;
    private string? input;
    private string? result;
    private string? error;
    private bool busy, modal, closing, closeAfterCancel, dismissed, discovering, refreshAfterJob;

    public MainWindow(string? initialFile = null, PopupAnchor? anchor = null, bool showSettings = false,
        ConversionSettings? preferences = null, bool reportToHost = false)
    {
        InitializeComponent();
        this.anchor = anchor;
        filePopup = initialFile is not null;
        this.reportToHost = reportToHost;
        this.preferences = preferences ?? store.Load();
        jobSettings = this.preferences;
        Width = Height = RingSize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = !filePopup;
        Topmost = filePopup;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RingSize) });
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition());
        Root.Children.Add(ring);

        var surface = new Ellipse { Width = 216, Height = 216, StrokeThickness = 1 };
        surface.SetResourceReference(Shape.FillProperty, "Chrome");
        surface.SetResourceReference(Shape.StrokeProperty, "Outline");
        Place(surface, 112, 112, 216, 216);
        var highlight = new Ellipse { Width = 214, Height = 214, IsHitTestVisible = false };
        highlight.SetResourceReference(Shape.FillProperty, "GlassHighlight");
        Place(highlight, 112, 112, 214, 214);

        dropHint = RingCaption("Drop a file here", 47);
        shortcutHint = RingCaption("Explorer   Ctrl + K", 160);

        var centerContent = new StackPanel();
        centerIcon = Ui.Icon("convert", 21, "Accent");
        centerContent.Children.Add(centerIcon);
        sourceLabel = Ui.Text("Konvert", 14, weight: FontWeights.SemiBold);
        sourceLabel.TextAlignment = TextAlignment.Center;
        sourceLabel.TextTrimming = TextTrimming.CharacterEllipsis;
        sourceLabel.TextWrapping = TextWrapping.NoWrap;
        sourceLabel.Margin = new Thickness(0, 4, 0, 0);
        centerContent.Children.Add(sourceLabel);
        hint = Ui.Text("Choose file", 10, "Muted");
        hint.TextAlignment = TextAlignment.Center;
        centerContent.Children.Add(hint);
        center = RoundButton(centerContent, 80, "Choose a file");
        center.SetResourceReference(Control.BackgroundProperty, "CoreFill");
        center.TabIndex = QuickSlots + 1;
        center.Click += (_, _) =>
        {
            if (busy) { CancelJob(); return; }
            if (result is not null) { Reveal(); return; }
            if (input is null || (!discovering && options.Count == 0)) PickFile();
            else if (!options.Any(option => option.Available) && !discovering) SetDrawer(true);
        };
        Place(center, 112, 112, 80, 80);

        expandIcon = Ui.Icon("down", 13, "Muted");
        expandLabel = Ui.Text("Options", 11, "Ink", FontWeights.SemiBold);
        expandLabel.Margin = new Thickness(5, 0, 0, 0);
        var disclosure = new StackPanel { Orientation = Orientation.Horizontal };
        disclosure.Children.Add(expandIcon);
        disclosure.Children.Add(expandLabel);
        expand = new Button { Content = disclosure, Width = 82, Height = 31, Padding = new Thickness(5, 3, 5, 3) };
        AutomationProperties.SetName(expand, "More formats and options");
        expand.TabIndex = QuickSlots;
        expand.Click += (_, _) => ToggleDrawer();
        Place(expand, 112, 185, 82, 31);

        status = Ui.Text("", 12);
        status.TextWrapping = TextWrapping.Wrap;
        status.MaxHeight = 72;
        AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
        statusCard = Ui.Card(status, 14, new Thickness(12, 9, 12, 9));
        statusCard.Margin = new Thickness(0, 0, 0, 4);
        statusCard.Visibility = Visibility.Collapsed;
        Grid.SetRow(statusCard, 1);
        Root.Children.Add(statusCard);

        details = Ui.Text("", 11, "Muted");
        details.Margin = new Thickness(0, 0, 0, 8);
        drawer.Children.Add(details);
        drawer.Children.Add(new ScrollViewer
        {
            Content = extraFormats,
            MaxHeight = 120,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        });
        folderButton = Ui.Button("Choose output folder", "folder", (_, _) => ChooseFolder());
        folderButton.Margin = new Thickness(0, 8, 0, 4);
        drawer.Children.Add(folderButton);
        resetFolder = Ui.Button("Use source folder", "back", (_, _) =>
        {
            jobSettings = jobSettings with { OutputDirectory = null };
            UpdateDetails();
            FitToContent();
        });
        resetFolder.Margin = new Thickness(0, 0, 0, 4);
        drawer.Children.Add(resetFolder);

        qualityOptions = AddSlider("JPEG quality", 10, 100, jobSettings.JpegQuality, "%", value => jobSettings = jobSettings with { JpegQuality = value }, out qualitySlider);
        dpiOptions = AddSlider("Image DPI", 72, 600, jobSettings.Dpi, "", value => jobSettings = jobSettings with { Dpi = value }, out dpiSlider);
        drawer.Children.Add(Ui.Button("Preferences", "settings", async (_, _) => await OpenSettingsAsync()));
        var optionsScroll = new ScrollViewer { Content = drawer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        drawerCard = Ui.Card(optionsScroll, 17, new Thickness(12));
        drawerCard.Visibility = Visibility.Collapsed;
        Grid.SetRow(drawerCard, 2);
        Root.Children.Add(drawerCard);

        Title = initialFile is null ? "Konvert" : "Konvert · " + Path.GetFileName(initialFile);
        if (initialFile is not null) SetSource(initialFile);
        else UpdatePresentation();
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (!IsActive) DismissPopup();
        });
        IsVisibleChanged += (_, _) => WatchVisibility();
        progress.Tick += (_, _) => UpdatePresentation();
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Escape) return;
            if (busy) CancelJob();
            else if (drawerCard.Visibility == Visibility.Visible) ToggleDrawer();
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
            anchor = WindowEffects.PlacePopup(this, anchor, RingSize);
            Activate();
            WatchVisibility();
            if (filePopup && Animate)
            {
                var offset = new TranslateTransform();
                Root.RenderTransform = offset;
                offset.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(145))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            }
            if (IsActive) (quickButtons.FirstOrDefault() ?? (input is null ? center : expand)).Focus();
            if (initialFile is null && File.Exists(NativeHostIntegration.HostPath))
            {
                try { NativeHostIntegration.Start(); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
            if (input is not null) await DiscoverAsync();
            if (showSettings && !closing) await OpenSettingsAsync();
        };
    }

    private bool Animate => !preferences.ReducedMotion && !SystemParameters.HighContrast && SystemParameters.ClientAreaAnimation;

    private void WatchVisibility()
    {
        if (!filePopup || !IsLoaded || !IsVisible)
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
        if (!filePopup || closing || modal || dismissed) return;
        if (!busy)
        {
            Close();
            return;
        }
        dismissed = true;
        Hide();
        if (reportToHost) NativeHostIntegration.PopupWasDismissed();
    }

    private void SetSource(string path)
    {
        if (!File.Exists(path))
        {
            ShowError("This file is unavailable. Choose another file.");
            return;
        }
        input = Path.GetFullPath(path);
        dropHint.Visibility = shortcutHint.Visibility = Visibility.Collapsed;
        center.ToolTip = Path.GetFileName(input);
        AutomationProperties.SetName(center, Path.GetFileName(input));
        RenderFormats();
    }

    private Task DiscoverAsync() => discovery ??= DiscoverCoreAsync();

    private async Task DiscoverCoreAsync()
    {
        var token = lifetime.Token;
        discovering = true;
        UpdatePresentation();
        try
        {
            var capabilities = await Task.Run(() => EngineCapabilities.DetectAsync(preferences, token, input), token);
            if (closing) return;
            catalog = new(capabilities);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!closing) ShowError(ex.Message); }
        finally
        {
            discovering = false;
            if (!closing)
            {
                if (busy) refreshAfterJob = true;
                else RenderFormats();
            }
        }
    }

    private void RenderFormats()
    {
        foreach (var button in quickButtons) ring.Children.Remove(button);
        quickButtons.Clear();
        extraFormats.Children.Clear();
        if (input is null) return;
        options = catalog.GetOptions(input);
        var ready = options.Where(option => option.Available).ToArray();
        (double X, double Y)[] slots = [(112, 39), (183, 106), (41, 106)];
        for (var index = 0; index < Math.Min(ready.Length, QuickSlots); index++)
        {
            var target = ready[index].Target;
            var title = Ui.Text(target.Label, 13, "Ink", FontWeights.SemiBold);
            title.TextAlignment = TextAlignment.Center;
            var category = Ui.Text(Ui.FormatCategory(target.Extension), 9, "Muted");
            category.TextAlignment = TextAlignment.Center;
            var label = new StackPanel();
            label.Children.Add(title);
            label.Children.Add(category);
            var button = RoundButton(label, 55, "Convert to " + target.Label);
            button.TabIndex = index;
            button.ToolTip = $"{Ui.FormatName(target.Extension)} · {target.Engine}\n{target.Detail}";
            button.Click += async (_, _) => await ConvertAsync(target,
                drawerCard.Visibility == Visibility.Visible ? jobSettings : new ConversionSettings());
            Place(button, slots[index].X, slots[index].Y, 55, 55);
            quickButtons.Add(button);
        }
        if (drawerCard.Visibility == Visibility.Visible) RenderExtraFormats();
        UpdatePresentation();
    }

    private void RenderExtraFormats()
    {
        extraFormats.Children.Clear();
        foreach (var option in options)
        {
            var target = option.Target;
            var label = Ui.Text(option.Available ? target.Label : target.Label + " ↗", 11,
                option.Available ? "Accent" : "Muted", FontWeights.SemiBold);
            var button = new Button
            {
                Content = label,
                Width = 60,
                Height = 34,
                Padding = new Thickness(2),
                Margin = new Thickness(0, 0, 4, 4),
                ToolTip = option.Available
                    ? $"{Ui.FormatName(target.Extension)} · {target.Engine}\n{target.Detail}"
                    : $"{Ui.FormatName(target.Extension)} needs {target.Engine}. Open converter settings."
            };
            AutomationProperties.SetName(button, option.Available ? "Convert to " + target.Label + " with options" : "Set up " + target.Label + " with " + target.Engine);
            button.Click += async (_, _) =>
            {
                if (option.Available) await ConvertAsync(target, jobSettings);
                else await OpenSettingsAsync("engines");
            };
            extraFormats.Children.Add(button);
        }
        if (options.Count == 0 && input is not null)
            extraFormats.Children.Add(Ui.Text(discovering ? "Looking for converters…" : "No supported exports for this file.", 11, "Muted"));
        qualityOptions.Visibility = options.Any(option => option.Available && option.Target.SupportsJpegQuality) ? Visibility.Visible : Visibility.Collapsed;
        dpiOptions.Visibility = options.Any(option => option.Available && option.Target.SupportsDpi) ? Visibility.Visible : Visibility.Collapsed;
        UpdateDetails();
    }

    private void ToggleDrawer()
    {
        if (busy || closing) return;
        SetDrawer(drawerCard.Visibility != Visibility.Visible);
    }

    private void SetDrawer(bool open)
    {
        drawerCard.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open) RenderExtraFormats();
        else extraFormats.Children.Clear();
        expandIcon.Kind = open ? "up" : "down";
        expandLabel.Text = open ? "Less" : "Options";
        AutomationProperties.SetName(expand, open ? "Hide options" : "More formats and options");
        FitToContent();
        if (open && IsLoaded && Animate)
            drawerCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
            {
                FillBehavior = FillBehavior.Stop,
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private void UpdateDetails()
    {
        details.Text = jobSettings.OutputDirectory is null
            ? "Save next to the original"
            : "Save to " + jobSettings.OutputDirectory;
        details.ToolTip = jobSettings.OutputDirectory;
        details.MaxHeight = 44;
        folderButton.ToolTip = jobSettings.OutputDirectory;
        var canSave = options.Any(option => option.Available);
        details.Visibility = folderButton.Visibility = canSave ? Visibility.Visible : Visibility.Collapsed;
        resetFolder.Visibility = canSave && jobSettings.OutputDirectory is not null ? Visibility.Visible : Visibility.Collapsed;
    }

    private FrameworkElement AddSlider(string name, int minimum, int maximum, int value, string suffix, Action<int> update, out Slider slider)
    {
        var label = Ui.Text($"{name}  {value}{suffix}", 11, "Muted");
        label.Margin = new Thickness(0, 7, 0, 0);
        slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 2) };
        AutomationProperties.SetName(slider, name);
        slider.ValueChanged += (_, e) =>
        {
            var next = (int)e.NewValue;
            label.Text = $"{name}  {next}{suffix}";
            update(next);
        };
        var section = new StackPanel();
        section.Children.Add(label);
        section.Children.Add(slider);
        drawer.Children.Add(section);
        return section;
    }

    private async Task ConvertAsync(ConversionTarget target, ConversionSettings settings)
    {
        if (input is null || busy || closing) return;
        error = null;
        result = null;
        using var job = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        conversion = job;
        activeTarget = target;
        jobTime = Stopwatch.StartNew();
        busy = true;
        SetControlsEnabled(false);
        if (IsVisible) progress.Start();
        UpdatePresentation();
        string? saved = null;
        try
        {
            using var service = new ConversionService(catalog.Capabilities);
            var converted = await service.ConvertAsync(input, target.Extension, settings, job.Token);
            result = converted.OutputPath;
            saved = result;
        }
        catch (OperationCanceledException) when (job.IsCancellationRequested) { error = "Conversion cancelled. You can try another format."; }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            busy = false;
            conversion = null;
            activeTarget = null;
            progress.Stop();
            SetControlsEnabled(true);
            if (dismissed)
            {
                if (reportToHost && !job.IsCancellationRequested) NativeHostIntegration.ReportBackgroundResult(saved is not null);
                Close();
            }
            else if (closeAfterCancel) Close();
            else if (refreshAfterJob)
            {
                refreshAfterJob = false;
                RenderFormats();
            }
            else UpdatePresentation();
        }
        if (saved is not null && !closing && drawerCard.Visibility != Visibility.Visible)
        {
            try { await Task.Delay(1800, lifetime.Token); }
            catch (OperationCanceledException) { return; }
            if (!closing && !busy && result == saved && drawerCard.Visibility != Visibility.Visible) Close();
        }
    }

    private void SetControlsEnabled(bool enabled)
    {
        foreach (var button in quickButtons) button.IsEnabled = enabled;
        drawerCard.IsEnabled = enabled;
        expand.IsEnabled = enabled;
    }

    private void ShowError(string message)
    {
        error = message;
        UpdatePresentation();
    }

    private void UpdatePresentation()
    {
        var ready = options.Any(option => option.Available);
        var noExports = input is not null && !ready && !discovering;
        var extension = Path.GetExtension(input)?.TrimStart('.') ?? "";
        sourceLabel.Text = busy && activeTarget is not null ? "To " + activeTarget.Label :
            result is not null ? Path.GetExtension(result).TrimStart('.').ToUpperInvariant() :
            input is null ? "Konvert" : extension.Length == 0 ? "FILE" : extension.ToUpperInvariant();
        centerIcon.Kind = result is not null ? "check" : "convert";
        hint.Text = busy ? "Cancel" : result is not null ? "Show file" : input is null ? "Choose file" :
            discovering && !ready ? "Checking…" : noExports ? (options.Count == 0 ? "Choose file" : "Set up") :
            error is not null ? "Try again" : "Source file";
        center.IsTabStop = busy || result is not null || input is null || noExports;
        center.ToolTip = busy ? "Cancel conversion (Esc)" : result ?? (input is null ? "Choose a file" : Path.GetFileName(input));
        var action = busy ? "Cancel conversion" : result is not null ? "Show converted file" :
            input is null || (noExports && options.Count == 0) ? "Choose a file" :
            noExports ? "Open converter setup options" : Path.GetFileName(input);
        AutomationProperties.SetName(center, action);

        var message = busy && activeTarget is not null && jobTime is not null
            ? $"Converting to {activeTarget.Label} · {activeTarget.Engine}\n{(int)jobTime.Elapsed.TotalSeconds}s elapsed"
            : error ?? (result is not null ? $"Saved {Path.GetFileName(result)}" : noExports
                ? options.Count == 0 ? "There’s no export for this file. Choose another file." : "Open Options to set up a converter."
                : "");
        if (message.Length > 360) message = message[..357] + "…";
        status.Text = message;
        status.ToolTip = message;
        statusCard.Visibility = message.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        UpdateDetails();
        FitToContent();
    }

    private void FitToContent()
    {
        var measure = new Size(RingSize, double.PositiveInfinity);
        statusCard.Measure(measure);
        drawerCard.Measure(measure);
        var height = RingSize + statusCard.DesiredSize.Height + drawerCard.DesiredSize.Height;
        if (Math.Abs(Height - height) < 0.5) return;
        Height = height;
        if (IsLoaded && IsVisible) WindowEffects.PlacePopup(this, anchor, RingSize);
    }

    private void ChooseFolder()
    {
        modal = true;
        try
        {
            var dialog = new OpenFolderDialog { Title = "Choose where to save this conversion" };
            if (dialog.ShowDialog(this) == true)
            {
                jobSettings = jobSettings with { OutputDirectory = dialog.FolderName };
                UpdateDetails();
                FitToContent();
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
            if (dialog.ShowDialog() == true)
            {
                store.Save(dialog.Settings);
                preferences = dialog.Settings;
                jobSettings = preferences;
                qualitySlider.Value = jobSettings.JpegQuality;
                dpiSlider.Value = jobSettings.Dpi;
                if (discovery is not null) await discovery;
                discovery = null;
                if (input is not null) await DiscoverAsync();
            }
        }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            modal = false;
            if (!closing) Topmost = wasTopmost;
            ThemeManager.Apply(preferences.Theme, Application.Current.Resources);
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

    private void CancelJob()
    {
        conversion?.Cancel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            closeAfterCancel = true;
            CancelJob();
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

    private static Button RoundButton(UIElement content, double size, string name)
    {
        var button = new Button { Content = content, Width = size, Height = size, Padding = new Thickness(4) };
        button.Style = (Style)Application.Current.FindResource("RoundButton");
        AutomationProperties.SetName(button, name);
        return button;
    }

    private TextBlock RingCaption(string text, double centerY)
    {
        var label = Ui.Text(text, 10, "Muted", FontWeights.SemiBold);
        label.Width = 156;
        label.TextAlignment = TextAlignment.Center;
        label.IsHitTestVisible = false;
        Place(label, 112, centerY, 156, 14);
        return label;
    }

    private void Place(FrameworkElement element, double centerX, double centerY, double width, double height)
    {
        Canvas.SetLeft(element, centerX - width / 2);
        Canvas.SetTop(element, centerY - height / 2);
        ring.Children.Add(element);
    }
}
