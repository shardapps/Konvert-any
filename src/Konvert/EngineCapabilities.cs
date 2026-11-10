using System.Collections.Frozen;
using System.Text.Json;

namespace Konvert;

public sealed record EngineCapabilities(string? LibreOffice, string? Ffmpeg, IReadOnlySet<string> Encoders)
{
    public static EngineCapabilities None { get; } = new(null, null, FrozenSet<string>.Empty);
    public bool? HasAudio { get; init; }
    public bool? HasVideo { get; init; }

    public static async Task<EngineCapabilities> DetectAsync(
        ConversionSettings settings, CancellationToken cancellationToken = default, string? source = null)
    {
        var installed = Locate(settings, source);
        var ffmpeg = installed.Ffmpeg;
        if (ffmpeg is null) return installed;

        var encoders = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            var output = await ExternalProcess.RunAsync(ffmpeg, ["-hide_banner", "-encoders"], TimeSpan.FromSeconds(8), cancellationToken);
            foreach (var line in output.StandardOutput.Split('\n'))
            {
                var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 1 && parts[0].Length == 6) encoders.Add(parts[1]);
            }
        }
        catch (Exception ex) when (ProbeFailed(ex)) { return installed with { Ffmpeg = null }; }

        var capabilities = installed with { Encoders = encoders.ToFrozenSet(StringComparer.Ordinal) };
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
