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
    private ConversionSettings settings;
    private EngineCapabilities capabilities = new(null, null, new HashSet<string>());
    private ConversionService? service;
    private IReadOnlyList<ConversionTarget> targets = [];
    private readonly List<Button> petals = [];
    private string? input;
    private string? result;
    private int page;
    private bool busy;
    private CancellationTokenSource? operation;
    private bool closingAfterCancel;
    private bool detecting;

    public MainWindow(string? initialFile = null)
    {
        InitializeComponent();
        settings = store.Load();
        Loaded += async (_, _) =>
        {
            if (initialFile is not null) SelectFile(initialFile);
            await RefreshEnginesAsync();
        };
    }

    private async Task RefreshEnginesAsync()
    {
        detecting = true;
        foreach (var petal in petals) petal.IsEnabled = false;
        try
        {
            capabilities = await Task.Run(() => EngineCapabilities.DetectAsync(settings, lifetime.Token));
            service?.Dispose(); service = new(capabilities);
            if (input is not null) UpdateTargets();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { SetStatus("Engine detection needs attention", ex.Message); }
        finally
        {
            detecting = false;
            foreach (var petal in petals) petal.IsEnabled = !busy;
        }
    }

    private void SelectFile(string path)
    {
        if (busy) return;
        if (!File.Exists(path)) { SetStatus("Choose a file", "The file does not exist or is not accessible."); return; }
        input = Path.GetFullPath(path); result = null; page = 0;
        FileName.Text = Path.GetFileName(input); FileName.ToolTip = input;
        FileType.Text = Path.GetExtension(input).TrimStart('.').ToUpperInvariant();
        FileSize.Text = Size(new FileInfo(input).Length);
        EmptyOpen.Visibility = Visibility.Collapsed;
        RevealButton.Visibility = Visibility.Collapsed;
        UpdateTargets();
    }

    private void UpdateTargets()
    {
        targets = new FormatRegistry(capabilities).GetTargets(input!);
        page = Math.Min(page, Math.Max(0, (targets.Count - 1) / 6));
        Subtitle.Text = targets.Count > 0 ? "Choose a format to convert." : "No conversion available for this file yet.";
        FormatHint.Text = targets.Count > 0 ? "Original stays untouched · output gets a fresh filename" : "Check optional engines in Settings, or choose another file.";
        SetStatus("Ready when you are", settings.OutputDirectory is null ? "Output saves beside the original. Change this in Settings." : $"Output folder: {settings.OutputDirectory}");
        RenderPetals();
    }

    private void RenderPetals()
    {
        foreach (var button in petals) Orbit.Children.Remove(button);
        petals.Clear();
        var visible = targets.Skip(page * 6).Take(6).ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var target = visible[i];
            var angle = (-90 + i * 360.0 / visible.Count) * Math.PI / 180;
            var label = new StackPanel();
            label.Children.Add(new TextBlock { Text = target.Label, FontSize = 18, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Center, Foreground = (Brush)FindResource("Accent") });
            label.Children.Add(new TextBlock { Text = target.Engine, FontSize = 10, Margin = new Thickness(0, 4, 0, 0), Foreground = (Brush)FindResource("Muted"), HorizontalAlignment = HorizontalAlignment.Center });
            var button = new Button { Content = label, Width = 116, Height = 76, ToolTip = target.Detail, IsEnabled = !busy && !detecting };
            AutomationProperties.SetName(button, $"Convert to {target.Label}. {target.Detail}. {target.Engine}");
            button.Click += async (_, _) => await ConvertAsync(target);
            Canvas.SetLeft(button, 310 + Math.Cos(angle) * 227 - 58);
            Canvas.SetTop(button, 215 + Math.Sin(angle) * 167 - 38);
            Orbit.Children.Add(button); petals.Add(button);
            if (!settings.ReducedMotion && SystemParameters.ClientAreaAnimation)
            {
                button.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180 + i * 25)));
                var move = new TranslateTransform(0, 8); button.RenderTransform = move;
                move.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
            }
        }
        Pagination.Visibility = targets.Count > 6 ? Visibility.Visible : Visibility.Collapsed;
        PageLabel.Text = $"{page + 1} / {Math.Max(1, (targets.Count + 5) / 6)}";
    }

    private async Task ConvertAsync(ConversionTarget target)
    {
        if (input is null || service is null || busy || detecting) return;
        busy = true; operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        foreach (var petal in petals) petal.IsEnabled = false;
        CancelButton.Visibility = Visibility.Visible; RevealButton.Visibility = Visibility.Collapsed; Progress.Visibility = Visibility.Visible;
        SetStatus($"Converting to {target.Label}…", "Working locally. You can cancel without changing the original.");
        try
        {
            var converted = await service.ConvertAsync(input, target.Extension, settings, operation.Token);
            result = converted.OutputPath;
            SetStatus("A new form, ready.", $"{Path.GetFileName(result)} · {Size(converted.OutputBytes)} · {converted.Elapsed.TotalSeconds:0.00}s");
            RevealButton.Visibility = Visibility.Visible;
        }
        catch (OperationCanceledException) { SetStatus("Conversion cancelled", "The original is unchanged. No partial output was kept."); }
        catch (Exception ex) { SetStatus("Could not convert this file", ex.Message); }
        finally
        {
            busy = false; operation.Dispose(); operation = null;
            CancelButton.Visibility = Visibility.Collapsed; Progress.Visibility = Visibility.Collapsed;
            foreach (var petal in petals) petal.IsEnabled = true;
            if (closingAfterCancel) Close();
        }
    }

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        if (busy) return;
        var dialog = new OpenFileDialog { Title = "Choose a file to Konvert", CheckFileExists = true, Multiselect = false, Filter = "All files|*.*" };
        if (dialog.ShowDialog(this) == true) SelectFile(dialog.FileName);
    }

    private async void OnSettings(object sender, RoutedEventArgs e)
    {
        if (busy || detecting) return;
        var dialog = new SettingsWindow(settings, capabilities) { Owner = this };
        if (dialog.ShowDialog() != true) return;
        try { store.Save(dialog.Settings); settings = dialog.Settings; await RefreshEnginesAsync(); }
        catch (Exception ex) { SetStatus("Could not save settings", ex.Message); }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!busy && e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            if (files.Length == 1) SelectFile(files[0]);
            else SetStatus("One file at a time", "Drop one file. Batch conversion is planned.");
        }
    }
    private void OnDragOver(object sender, DragEventArgs e) { e.Effects = !busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true; }
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { if (busy) operation?.Cancel(); else Close(); }
        if (e.Key == Key.O && Keyboard.Modifiers == ModifierKeys.Control) OnOpen(sender, e);
    }
    private void OnCancel(object sender, RoutedEventArgs e) => operation?.Cancel();
    private void OnPrevious(object sender, RoutedEventArgs e) { if (busy) return; page = Math.Max(0, page - 1); RenderPetals(); }
    private void OnNext(object sender, RoutedEventArgs e) { if (busy) return; page = Math.Min((targets.Count - 1) / 6, page + 1); RenderPetals(); }
    private void OnReveal(object sender, RoutedEventArgs e)
    {
        if (result is null) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{result}\"") { UseShellExecute = true }); }
        catch (Exception ex) { SetStatus("Could not open Explorer", ex.Message); }
    }
    private void SetStatus(string title, string detail) { StatusTitle.Text = title; StatusDetail.Text = detail; }
    private static string Size(long bytes) => bytes >= 1024 * 1024 ? $"{bytes / 1048576.0:0.0} MiB" : $"{bytes / 1024.0:0.#} KiB";

    protected override void OnClosing(CancelEventArgs e)
    {
        if (busy) { e.Cancel = true; closingAfterCancel = true; operation?.Cancel(); }
        else { lifetime.Cancel(); service?.Dispose(); lifetime.Dispose(); }
        base.OnClosing(e);
    }
}
