using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Threading;
using Konvert;

internal static class PickerTests
{
    public static void VerifyHomeDismissal()
    {
        var home = new MainWindow(preferences: new());
        var closed = false;
        home.Closed += (_, _) => closed = true;
        try
        {
            _ = FindButton(home, "Choose a file");
            _ = FindButton(home, "Open settings");
            home.DismissPopup();
            Require(closed, "Click-away must dismiss the first-launch picker.");
        }
        finally { if (!closed) home.Close(); }
    }

    public static void VerifyRecovery(string directory)
    {
        var missing = new MainWindow(Path.Combine(directory, "missing.csv"), preferences: new());
        try
        {
            Require(HasText(missing, "This file is unavailable. Choose another file."), "Missing files should show a recovery message.");
            _ = FindButton(missing, "Choose a file");
        }
        finally { missing.Close(); }

        var unknownPath = Path.Combine(directory, "unknown.fixture");
        File.WriteAllText(unknownPath, "No route");
        var unknown = new MainWindow(unknownPath, preferences: new());
        try
        {
            Require(HasText(unknown, "There’s no export for this file. Choose another file."), "Unsupported files should show an explanation.");
            _ = FindButton(unknown, "Choose a file");
        }
        finally { unknown.Close(); }

        var officePath = Path.Combine(directory, "optional.doc");
        File.WriteAllText(officePath, "An optional converter is required");
        var office = new MainWindow(officePath, preferences: new());
        try
        {
            Click(FindButton(office, "Open converter setup options"));
            _ = FindButton(office, "Set up PDF with LibreOffice");
        }
        finally { office.Close(); }
    }

    public static void VerifySavePaths(string directory)
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var output = Directory.CreateDirectory(Path.Combine(directory, "picker-output"));
        var input = Path.Combine(directory, "picker.csv");
        File.WriteAllText(input, "name,quantity\nSample,2\n");
        var picker = new MainWindow(input, preferences: new() { OutputDirectory = output.FullName });
        try
        {
            Click(FindButton(picker, "Convert to TXT"));
            AwaitState(() => HasButton(picker, "Show converted file"));
            Require(File.Exists(Path.ChangeExtension(input, "txt")), "A quick conversion must save beside the input.");
            Require(!File.Exists(Path.Combine(output.FullName, "picker.txt")), "A quick conversion must ignore the alternate folder.");

            Click(FindButton(picker, "More formats and options"));
            Click(FindButton(picker, "Convert to JSON with options"));
            AwaitState(() => HasButton(picker, "Show converted file"));
            Require(File.Exists(Path.Combine(output.FullName, "picker.json")), "An expanded conversion must use the selected folder.");
            Require(!File.Exists(Path.ChangeExtension(input, "json")), "An expanded conversion must not write a second output beside the source.");
        }
        finally
        {
            picker.Close();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    public static void VerifyFailedJobCanRetry(string directory)
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var input = Path.Combine(directory, "picker-ragged.csv");
        File.WriteAllText(input, "name,quantity\nSample\n");
        var picker = new MainWindow(input, preferences: new());
        try
        {
            Click(FindButton(picker, "Convert to JSON"));
            AwaitState(() => HasText(picker, "Each table row must have the same number of fields as the header."));
            Require(!File.Exists(Path.ChangeExtension(input, "json")), "A failed conversion must not publish an output.");

            Click(FindButton(picker, "Convert to TXT"));
            AwaitState(() => HasButton(picker, "Show converted file"));
            Require(File.Exists(Path.ChangeExtension(input, "txt")), "The same popup must be able to retry with another format.");
        }
        finally
        {
            picker.Close();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    public static void VerifyBackgroundSave(string directory)
    {
        var previousContext = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
        var input = Path.Combine(directory, "picker-background.log");
        File.WriteAllText(input, new string('x', 1024 * 1024));
        var picker = new MainWindow(input, preferences: new());
        var closed = false;
        picker.Closed += (_, _) => closed = true;
        try
        {
            Require(!picker.ShowInTaskbar, "A file picker must stay a transient popup.");
            Click(FindButton(picker, "Convert to TXT"));
            Require(HasButton(picker, "Cancel conversion"), "The test conversion should be running.");
            picker.DismissPopup();
            Require(!closed, "Dismissing the popup must let its job finish.");
            AwaitState(() => closed);
            Require(new FileInfo(Path.ChangeExtension(input, "txt")).Length == 1024 * 1024,
                "The dismissed conversion must still save the full output.");
        }
        finally
        {
            if (!closed) picker.Close();
            SynchronizationContext.SetSynchronizationContext(previousContext);
        }
    }

    private static void AwaitState(Func<bool> ready)
    {
        var frame = new DispatcherFrame();
        var deadline = System.Diagnostics.Stopwatch.StartNew();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(20) };
        timer.Tick += (_, _) =>
        {
            if (ready() || deadline.Elapsed > TimeSpan.FromSeconds(5))
                frame.Continue = false;
        };
        timer.Start();
        try { Dispatcher.PushFrame(frame); }
        finally { timer.Stop(); }
        Require(ready(), "The picker did not reach the expected conversion state.");
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    private static Button FindButton(Window window, string name) => Buttons(window)
        .Single(button => AutomationProperties.GetName(button) == name);

    private static bool HasButton(Window window, string name) => Buttons(window)
        .Any(button => AutomationProperties.GetName(button) == name);

    private static IEnumerable<Button> Buttons(Window window) => Descendants(window).OfType<Button>();

    private static bool HasText(Window window, string message) => Descendants(window).OfType<TextBlock>()
        .Any(label => label.Text == message);

    private static IEnumerable<DependencyObject> Descendants(DependencyObject element)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
