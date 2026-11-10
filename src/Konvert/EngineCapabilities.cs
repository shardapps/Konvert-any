using System.Collections.Frozen;
using System.Text.Json;

namespace Konvert;

public sealed record EngineCapabilities(string? LibreOffice, string? Ffmpeg, IReadOnlySet<string> Encoders)
{
    public static EngineCapabilities None { get; } = new(null, null, FrozenSet<string>.Empty);
    public bool? HasAudio { get; init; }
    public bool? HasVideo { get; init; }
    internal IReadOnlySet<string>? Muxers { get; init; }
    internal bool HasWindowsImageCodec { get; init; }

    public static async Task<EngineCapabilities> DetectAsync(
        ConversionSettings settings, CancellationToken cancellationToken = default, string? source = null)
    {
        var installed = Locate(settings, source);
        if (source is not null && FormatRegistry.Uses(source, ConversionBackend.WindowsImage))
        {
            var supported = await Task.Run(() => ImageConverter.CanDecode(source), cancellationToken);
            installed = installed with { HasWindowsImageCodec = supported };
        }
        var ffmpeg = installed.Ffmpeg;
        if (ffmpeg is null) return installed;

        EngineCapabilities capabilities;
        try
        {
            var deadline = TimeSpan.FromSeconds(8);
            var results = await Task.WhenAll(
                ExternalProcess.RunAsync(ffmpeg, ["-hide_banner", "-encoders"], deadline, cancellationToken),
                ExternalProcess.RunAsync(ffmpeg, ["-hide_banner", "-muxers"], deadline, cancellationToken));
            capabilities = installed with
            {
                Encoders = ReadCapabilities(results[0].StandardOutput, flags => flags.Length == 6),
                Muxers = ReadCapabilities(results[1].StandardOutput, flags => flags is "E" or ".E" or "DE")
            };
        }
        catch (Exception ex) when (ProbeFailed(ex)) { return installed with { Ffmpeg = null }; }

        var probe = Path.Combine(Path.GetDirectoryName(ffmpeg)!, "ffprobe.exe");
        if (source is null || !File.Exists(probe) || !File.Exists(source)) return capabilities;

        try
        {
            var output = await ExternalProcess.RunAsync(probe,
                ["-v", "error", "-protocol_whitelist", "file,pipe", "-show_entries", "stream=codec_type:stream_disposition=attached_pic",
                    "-of", "json", "-i", Path.GetFullPath(source)], TimeSpan.FromSeconds(5), cancellationToken);
            using var json = JsonDocument.Parse(output.StandardOutput);
            var streams = json.RootElement.GetProperty("streams").EnumerateArray().ToArray();
            return capabilities with
            {
                HasAudio = streams.Any(stream => stream.GetProperty("codec_type").GetString() == "audio"),
                HasVideo = streams.Any(stream => stream.GetProperty("codec_type").GetString() == "video" &&
                    (!stream.TryGetProperty("disposition", out var disposition) ||
                    !disposition.TryGetProperty("attached_pic", out var attached) || attached.GetInt32() == 0))
            };
        }
        catch (Exception ex) when (ProbeFailed(ex) || ex is JsonException or KeyNotFoundException) { return capabilities; }
    }

    private static FrozenSet<string> ReadCapabilities(string output, Func<string, bool> acceptsFlags)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var line in output.Split('\n'))
        {
            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 1 && acceptsFlags(parts[0]) && parts[1] != "=")
                names.UnionWith(parts[1].Split(','));
        }
        return names.ToFrozenSet(StringComparer.Ordinal);
    }

    internal static EngineCapabilities Locate(ConversionSettings settings, string? source = null)
    {
        var office = source is null || FormatRegistry.Uses(source, ConversionBackend.Office)
            ? ResolveOffice(settings.LibreOfficePath)
            : null;
        var ffmpeg = source is null || FormatRegistry.Uses(source, ConversionBackend.Media)
            ? Resolve(settings.FfmpegPath, "ffmpeg.exe")
            : null;
        return new(office, ffmpeg, FrozenSet<string>.Empty);
    }

    private static bool ProbeFailed(Exception ex) =>
        ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException;

    private static string? ResolveOffice(string? configured)
    {
        var engine = Resolve(configured, "soffice.com", "LibreOffice", "program") ??
            (string.IsNullOrWhiteSpace(configured) ? Resolve(null, "soffice.exe", "LibreOffice", "program") : null);
        if (engine is null) return null;
        if (Path.GetFileName(engine).Equals("soffice.com", StringComparison.OrdinalIgnoreCase)) return engine;
        if (!Path.GetFileName(engine).Equals("soffice.exe", StringComparison.OrdinalIgnoreCase)) return null;
        var console = Path.ChangeExtension(engine, ".com");
        return File.Exists(console) ? console : engine;
    }

    private static string? Resolve(string? configured, string executable, params string[] subdirectories)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.IsPathFullyQualified(configured) && File.Exists(configured) ? Path.GetFullPath(configured) : null;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            if (string.IsNullOrEmpty(root)) continue;
            var candidate = Path.Combine([root, .. subdirectories, executable]);
            if (File.Exists(candidate)) return candidate;
        }
        foreach (var entry in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            var folder = entry.Trim().Trim('"');
            if (!Path.IsPathFullyQualified(folder)) continue;
            try
            {
                var candidate = Path.Combine(folder, executable);
                if (File.Exists(candidate)) return candidate;
            }
            catch (ArgumentException) { }
        }
        return null;
    }
}
