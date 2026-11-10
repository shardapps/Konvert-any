using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private PopupAnchor? anchor;
    private readonly Canvas ring = new() { Width = RingSize, Height = RingSize };
    private readonly StackPanel drawer = new();
    private readonly Border drawerCard;
    private readonly WrapPanel extraFormats = new();
    private readonly List<Button> quickButtons = [];
    private readonly Button center;
    private readonly Button expand;
    private readonly IconControl expandIcon;
    private readonly TextBlock sourceLabel;
    private readonly TextBlock hint;
    private readonly TextBlock details;
    private readonly Button folderButton;
    private readonly FrameworkElement qualityOptions;
    private readonly FrameworkElement dpiOptions;
    private ConversionSettings preferences;
    private ConversionSettings jobSettings;
    private FormatRegistry catalog = new(EngineCapabilities.None);
    private IReadOnlyList<ConversionOption> options = [];
    private Task? discovery;
    private string? input;
    private string? result;
    private string? error;
    private bool busy, modal, closing, closeAfterCancel, activated;

    public MainWindow(string? initialFile = null, PopupAnchor? anchor = null, bool showSettings = false, ConversionSettings? preferences = null)
    {
        InitializeComponent();
        this.anchor = anchor;
        this.preferences = preferences ?? store.Load();
        jobSettings = this.preferences;
        Width = Height = RingSize;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = true;
        Topmost = true;
        WindowStartupLocation = WindowStartupLocation.Manual;
        UseLayoutRounding = true;
        SnapsToDevicePixels = true;

        Root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(RingSize) });
        Root.RowDefinitions.Add(new RowDefinition());
        Root.Children.Add(ring);

        var surface = new Ellipse { Width = 216, Height = 216, StrokeThickness = 1 };
        surface.SetResourceReference(Shape.FillProperty, "Chrome");
        surface.SetResourceReference(Shape.StrokeProperty, "Outline");
        Place(surface, 112, 112, 216, 216);
        var highlight = new Ellipse { Width = 214, Height = 214, IsHitTestVisible = false };
        highlight.SetResourceReference(Shape.FillProperty, "GlassHighlight");
        Place(highlight, 112, 112, 214, 214);

        var centerContent = new StackPanel();
        centerContent.Children.Add(Ui.Icon("convert", 22, "Accent"));
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
        center.TabIndex = QuickSlots + 1;
        center.Click += (_, _) =>
        {
            if (busy) { CancelJob(); return; }
            if (result is not null) { Reveal(); return; }
            if (input is null) PickFile();
        };
        Place(center, 112, 112, 80, 80);

        expandIcon = Ui.Icon("down", 17);
        expand = RoundButton(expandIcon, 36, "More formats and options");
        expand.TabIndex = QuickSlots;
        expand.Click += (_, _) => ToggleDrawer();
        Place(expand, 112, 187, 36, 36);

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
        drawer.Children.Add(Ui.Button("Use source folder", "back", (_, _) =>
        {
            jobSettings = jobSettings with { OutputDirectory = null };
            UpdateDetails();
        }));

        qualityOptions = AddSlider("JPEG quality", 10, 100, jobSettings.JpegQuality, "%", value => jobSettings = jobSettings with { JpegQuality = value });
        dpiOptions = AddSlider("Image DPI", 72, 600, jobSettings.Dpi, "", value => jobSettings = jobSettings with { Dpi = value });
        drawer.Children.Add(Ui.Button("Preferences", "settings", async (_, _) => await OpenSettingsAsync()));
        var optionsScroll = new ScrollViewer { Content = drawer, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        drawerCard = Ui.Card(optionsScroll, 17, new Thickness(12));
        drawerCard.Visibility = Visibility.Collapsed;
        Grid.SetRow(drawerCard, 1);
        Root.Children.Add(drawerCard);

        Title = initialFile is null ? "Konvert" : "Konvert · " + Path.GetFileName(initialFile);
        if (initialFile is not null) SetSource(initialFile);
        Activated += (_, _) => activated = true;
        Deactivated += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            if (input is not null && activated && !IsActive && !closing && !busy && !modal)
                Close();
        });
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
            if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: 1 } files)
                OpenFile(files[0]);
        };
        Loaded += async (_, _) =>
        {
            anchor = WindowEffects.PlacePopup(this, anchor, RingSize);
            Activate();
            if (initialFile is null && File.Exists(NativeHostIntegration.HostPath))
            {
                try { NativeHostIntegration.Start(); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
            if (input is not null) await DiscoverAsync();
            if (showSettings && !closing) await OpenSettingsAsync();
        };
    }

    private void SetSource(string path)
    {
        if (!File.Exists(path))
        {
            ShowError("This file is unavailable. Choose a local file.");
            return;
        }
        input = Path.GetFullPath(path);
        sourceLabel.Text = Path.GetExtension(input).TrimStart('.').ToUpperInvariant();
        if (sourceLabel.Text.Length == 0) sourceLabel.Text = "FILE";
        center.ToolTip = Path.GetFileName(input);
        AutomationProperties.SetName(center, Path.GetFileName(input));
        RenderFormats();
    }

    private Task DiscoverAsync() => discovery ??= DiscoverCoreAsync();

    private async Task DiscoverCoreAsync()
    {
        var token = lifetime.Token;
        try
        {
            var capabilities = await Task.Run(() => EngineCapabilities.DetectAsync(preferences, token, input), token);
            if (closing) return;
            catalog = new(capabilities);
            if (!busy) RenderFormats();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { if (!closing) ShowError(ex.Message); }
    }

    private void RenderFormats()
    {
        foreach (var button in quickButtons) ring.Children.Remove(button);
        quickButtons.Clear();
        extraFormats.Children.Clear();
        if (input is null) return;
        options = catalog.GetOptions(input);
        var ready = options.Where(option => option.Available).ToArray();
        if (result is null && error is null)
            hint.Text = ready.Length > 0 ? "Convert to" : discovery is { IsCompleted: false } ? "Checking…" : "More options";
        (double X, double Y)[] slots = [(112, 39), (183, 106), (41, 106)];
        for (var index = 0; index < Math.Min(ready.Length, QuickSlots); index++)
        {
            var target = ready[index].Target;
            var label = Ui.Text(target.Label, 13, "Accent", FontWeights.SemiBold);
            var button = RoundButton(label, 55, "Convert to " + target.Label);
            button.TabIndex = index;
            button.ToolTip = target.Detail;
            button.Click += async (_, _) => await ConvertAsync(target,
                drawerCard.Visibility == Visibility.Visible ? jobSettings : new ConversionSettings());
            Place(button, slots[index].X, slots[index].Y, 55, 55);
            quickButtons.Add(button);
        }
        if (drawerCard.Visibility == Visibility.Visible) RenderExtraFormats();
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
                ToolTip = option.Available ? target.Detail : "Set up " + target.Engine
            };
            AutomationProperties.SetName(button, option.Available ? "Convert to " + target.Label + " with options" : "Set up " + target.Label + " with " + target.Engine);
            button.Click += async (_, _) =>
            {
                if (option.Available) await ConvertAsync(target, jobSettings);
                else await OpenSettingsAsync("engines");
            };
            extraFormats.Children.Add(button);
        }
        if (options.Count == 0) extraFormats.Children.Add(Ui.Text("No supported exports for this file.", 11, "Muted"));
        qualityOptions.Visibility = options.Any(option => option.Available && option.Target.SupportsJpegQuality) ? Visibility.Visible : Visibility.Collapsed;
        dpiOptions.Visibility = options.Any(option => option.Available && option.Target.SupportsDpi) ? Visibility.Visible : Visibility.Collapsed;
        UpdateDetails();
    }

    private void ToggleDrawer()
    {
        if (busy || closing) return;
        SetDrawer(drawerCard.Visibility != Visibility.Visible);
        if (drawerCard.Visibility == Visibility.Visible) UpdateDetails();
    }

    private void SetDrawer(bool open)
    {
        drawerCard.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        if (open) RenderExtraFormats();
        else extraFormats.Children.Clear();
        expandIcon.Kind = open ? "up" : "down";
        AutomationProperties.SetName(expand, open ? "Hide options" : "More formats and options");
        drawerCard.Measure(new Size(RingSize, double.PositiveInfinity));
        Height = RingSize + drawerCard.DesiredSize.Height;
        if (IsLoaded) WindowEffects.PlacePopup(this, anchor, RingSize);
    }

    private void UpdateDetails()
    {
        details.Text = error ?? (jobSettings.OutputDirectory is null
            ? "Save next to the original"
            : "Save to " + jobSettings.OutputDirectory);
        details.ToolTip = error ?? jobSettings.OutputDirectory;
        details.MaxHeight = 44;
        folderButton.ToolTip = jobSettings.OutputDirectory;
    }

    private FrameworkElement AddSlider(string name, int minimum, int maximum, int value, string suffix, Action<int> update)
    {
        var label = Ui.Text($"{name}  {value}{suffix}", 11, "Muted");
        label.Margin = new Thickness(0, 7, 0, 0);
        var slider = new Slider { Minimum = minimum, Maximum = maximum, Value = value, TickFrequency = 1, IsSnapToTickEnabled = true, Margin = new Thickness(0, 2, 0, 2) };
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
        UpdateDetails();
        busy = true;
        SetControlsEnabled(false);
        hint.Text = "Working…";
        AutomationProperties.SetName(center, "Cancel conversion");
        string? saved = null;
        try
        {
            using var service = new ConversionService(catalog.Capabilities);
            var converted = await service.ConvertAsync(input, target.Extension, settings, lifetime.Token);
            result = converted.OutputPath;
            saved = result;
            hint.Text = "Saved";
            sourceLabel.Text = target.Label;
            center.ToolTip = converted.OutputPath;
            AutomationProperties.SetName(center, "Show converted file");
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { hint.Text = "Cancelled"; }
        catch (Exception ex) { ShowError(ex.Message); }
        finally
        {
            busy = false;
            SetControlsEnabled(true);
            if (result is null) AutomationProperties.SetName(center, Path.GetFileName(input));
            if (closeAfterCancel) Close();
        }
        if (saved is not null && !closing && drawerCard.Visibility != Visibility.Visible)
        {
            try { await Task.Delay(1100, lifetime.Token); }
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
        hint.Text = "Check options";
        error = message;
        center.ToolTip = message;
        UpdateDetails();
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
            }
        }
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
        finally { modal = false; }
    }

    private void OpenFile(string path)
    {
        var next = new MainWindow(path, preferences: preferences);
        next.Show();
        Close();
    }

    private async Task OpenSettingsAsync(string section = "appearance")
    {
        modal = true;
        var wasTopmost = Topmost;
        Topmost = false;
        try
        {
            if (discovery is not null) await discovery;
            if (closing) return;
            var dialog = new SettingsWindow(preferences, catalog.Capabilities, section) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                preferences = dialog.Settings;
                jobSettings = preferences;
                store.Save(preferences);
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
        closeAfterCancel = true;
        lifetime.Cancel();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (busy)
        {
            e.Cancel = true;
            CancelJob();
        }
        else closing = true;
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
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

    private void Place(FrameworkElement element, double centerX, double centerY, double width, double height)
    {
        Canvas.SetLeft(element, centerX - width / 2);
        Canvas.SetTop(element, centerY - height / 2);
        ring.Children.Add(element);
    }
}
