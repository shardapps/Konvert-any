using System.Diagnostics;

namespace Konvert;

public sealed record EngineCapabilities(string? LibreOffice, string? Ffmpeg, IReadOnlySet<string> Encoders)
{
    private static readonly Dictionary<string, string[]> RequiredEncoders = new()
    {
        ["mp3"] = ["libmp3lame"],
        ["wav"] = ["pcm_s16le"],
        ["flac"] = ["flac"],
        ["ogg"] = ["libvorbis"],
        ["m4a"] = ["aac"],
        ["opus"] = ["libopus"],
        ["mp4"] = ["libx264", "aac"],
        ["mkv"] = ["libx264", "aac"],
        ["webm"] = ["libvpx-vp9", "libopus"],
        ["gif"] = ["gif"]
    };

    public bool CanEncode(string extension) => Ffmpeg is not null &&
        RequiredEncoders.TryGetValue(extension, out var required) && required.All(Encoders.Contains);

    public static async Task<EngineCapabilities> DetectAsync(ConversionSettings settings, CancellationToken cancellationToken = default)
    {
        // Never search the current directory; only explicit paths and trusted installation/PATH locations.
        var office = Resolve(settings.LibreOfficePath, "soffice.exe", "LibreOffice", "program");
        var ffmpeg = Resolve(settings.FfmpegPath, "ffmpeg.exe");
        var encoders = new HashSet<string>(StringComparer.Ordinal);
        if (ffmpeg is not null)
        {
            try
            {
                var output = await ExternalProcess.RunAsync(ffmpeg, ["-hide_banner", "-encoders"], TimeSpan.FromSeconds(10), cancellationToken);
                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && parts[0].Length == 6) encoders.Add(parts[1]);
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or TimeoutException)
            { ffmpeg = null; }
        }
        return new(office, ffmpeg, encoders);
    }

    private static string? Resolve(string? configured, string executable, params string[] subdirectories)
    {
        if (!string.IsNullOrWhiteSpace(configured))
            return Path.IsPathFullyQualified(configured) && File.Exists(configured) ? Path.GetFullPath(configured) : null;
        foreach (var root in new[] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) })
        {
            var candidate = Path.Combine([root, .. subdirectories, executable]);
            if (File.Exists(candidate)) return candidate;
        }
        foreach (var folder in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator))
        {
            if (!Path.IsPathFullyQualified(folder)) continue;
            try { var candidate = Path.Combine(folder, executable); if (File.Exists(candidate)) return candidate; }
            catch (ArgumentException) { }
        }
        return null;
    }
}
