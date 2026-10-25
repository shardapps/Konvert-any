using System.Runtime.InteropServices;
using System.Windows;

namespace Konvert;

public partial class App : Application
{
    private Mutex? uiMutex;
    protected override async void OnStartup(StartupEventArgs e)
    {
        // This small, mostly static picker does not need a dedicated GPU rendering pipeline.
        System.Windows.Media.RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() is "--install-integration" or "--remove-integration")
        {
            try
            {
                if (e.Args[0] == "--install-integration") ShellIntegration.Enable();
                else { NativeHostIntegration.Stop(); ShellIntegration.Remove(); }
                Shutdown(0);
            }
            catch { Shutdown(1); }
            return;
        }
        if (e.Args.FirstOrDefault() is "--convert" or "--formats" or "--help")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AttachConsole(unchecked((uint)-1));
            var exitCode = await RunCommandAsync(e.Args);
            Shutdown(exitCode);
            return;
        }
        string? input = null;
        double? x = null, y = null;
        for (var i = 0; i < e.Args.Length; i++)
        {
            if (e.Args[i] is "--anchor-x" or "--anchor-y" && i + 1 < e.Args.Length)
            {
                if (double.TryParse(e.Args[++i], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var coordinate) && double.IsFinite(coordinate))
                {
                    if (e.Args[i - 1] == "--anchor-x") x = coordinate; else y = coordinate;
                }
            }
            else if (!e.Args[i].StartsWith("--", StringComparison.Ordinal)) input = e.Args[i];
        }
        ThemeManager.Apply(new SettingsStore().Load().Theme, Resources);
        uiMutex = new Mutex(false, "Local\\Shardapps.Konvert.UI");
        var window = new MainWindow(input, x.HasValue && y.HasValue ? new(x.Value, y.Value) : null, e.Args.Contains("--settings"));
        MainWindow = window;
        window.Show();
    }

    protected override void OnExit(ExitEventArgs e) { uiMutex?.Dispose(); base.OnExit(e); }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        try
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("Konvert 0.2 alpha | --formats <file> | --convert <file> --to <format> [--output <folder>]\nPass a file to open its radial popup. Ctrl+K works in Explorer with KonvertHost running. --settings opens preferences. Existing files are never overwritten.");
                return 0;
            }
            if (args.Length < 2) throw new ArgumentException("Supply an input file.");
            var settings = new SettingsStore().Load();
            var capabilities = await EngineCapabilities.DetectAsync(settings);
            if (args[0] == "--formats")
            {
                Console.WriteLine(string.Join(Environment.NewLine, new FormatRegistry(capabilities).GetTargets(args[1]).Select(t => $"{t.Extension}\t{t.Engine}\t{t.Detail}")));
                return 0;
            }
            string? target = null;
            for (var i = 2; i < args.Length; i += 2)
            {
                if (i + 1 >= args.Length) throw new ArgumentException($"Missing value for {args[i]}.");
                switch (args[i])
                {
                    case "--to": target = args[i + 1]; break;
                    case "--output": settings = settings with { OutputDirectory = args[i + 1] }; break;
                    default: throw new ArgumentException($"Unknown option: {args[i]}");
                }
            }
            using var service = new ConversionService(capabilities);
            var result = await service.ConvertAsync(args[1], target ?? throw new ArgumentException("Supply --to <format>."), settings);
            Console.WriteLine(result.OutputPath);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex.Message); return 1; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(uint processId);
}
