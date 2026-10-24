using System.Runtime.InteropServices;
using System.Windows;

namespace Konvert;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.FirstOrDefault() is "--convert" or "--formats" or "--help")
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            AttachConsole(unchecked((uint)-1));
            var exitCode = await RunCommandAsync(e.Args);
            Shutdown(exitCode);
            return;
        }
        var input = e.Args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal));
        var window = new MainWindow(input);
        MainWindow = window;
        window.Show();
    }

    private static async Task<int> RunCommandAsync(string[] args)
    {
        try
        {
            if (args[0] == "--help")
            {
                Console.WriteLine("Konvert 0.1 alpha | --formats <file> | --convert <file> --to <format> [--output <folder>]\nWithout a command, opens the native conversion menu. Existing files are never overwritten.");
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
