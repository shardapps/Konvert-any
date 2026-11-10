namespace Konvert;

internal enum ConversionBackend { Image, Text, Table, Office, Media }

internal sealed record ConversionRoute(
    string Extension,
    string Detail,
    ConversionBackend Backend,
    string[] Sources,
    string[]? Arguments = null,
    string[]? Encoders = null,
    bool Video = false)
{
    public ConversionTarget Target => new(Extension, Extension.ToUpperInvariant(), Detail, Backend switch
    {
        ConversionBackend.Office => "LibreOffice",
        ConversionBackend.Media => "FFmpeg",
        _ => "Built in"
    })
    {
        SupportsJpegQuality = Backend == ConversionBackend.Image && Extension is "jpg" or "pdf",
        SupportsDpi = Backend == ConversionBackend.Image && Extension != "gif"
    };

    public bool IsAvailable(EngineCapabilities capabilities) => Backend switch
    {
        ConversionBackend.Office => capabilities.LibreOffice is not null,
        ConversionBackend.Media => capabilities.Ffmpeg is not null &&
            Encoders!.All(capabilities.Encoders.Contains) &&
            (Video ? capabilities.HasVideo : capabilities.HasAudio) != false,
        _ => true
    };
}

public sealed class FormatRegistry(EngineCapabilities capabilities)
{
    private static readonly string[] Images = ["png", "jpg", "bmp", "gif", "tiff", "ico", "wdp"];
    private static readonly string[] PlainText = ["txt", "md", "log", "json", "xml", "yaml", "yml", "csv", "tsv", "html", "css", "js", "cs", "py", "sql"];
    private static readonly string[] Documents = ["docx", "doc", "odt", "rtf", "txt"];
    private static readonly string[] Sheets = ["xlsx", "xls", "ods", "csv", "tsv"];
    private static readonly string[] Slides = ["pptx", "ppt", "odp"];
    private static readonly string[] Video = ["mp4", "mkv", "mov", "webm", "avi", "wmv", "m4v", "mpeg", "mpg"];
    private static readonly string[] Media = [.. Video, "mp3", "wav", "flac", "ogg", "m4a", "aac", "aiff", "wma", "opus"];
    private static readonly ConversionRoute[] Routes =
    [
        new("png", "Lossless image · transparency", ConversionBackend.Image, Images),
        new("jpg", "JPEG image · quality control", ConversionBackend.Image, Images),
        new("pdf", "Single-page image PDF", ConversionBackend.Image, Images),
        new("tiff", "Lossless image · single frame", ConversionBackend.Image, Images),
        new("bmp", "Uncompressed image", ConversionBackend.Image, Images),
        new("gif", "256 colors · first frame", ConversionBackend.Image, Images),
        new("pdf", "Document export", ConversionBackend.Office, Documents),
        new("txt", "Extract paragraph text", ConversionBackend.Text, ["docx"]),
        new("txt", "UTF-8 · preserves source text", ConversionBackend.Text, PlainText),
        new("json", "Rows as JSON objects", ConversionBackend.Table, ["csv", "tsv"]),
        new("tsv", "Tab-separated table", ConversionBackend.Table, ["csv"]),
        new("csv", "Comma-separated table", ConversionBackend.Table, ["tsv", "json"]),
        new("html", "Escaped text · readable page", ConversionBackend.Text, PlainText),
        .. Office(Documents, "Document export", ["docx", "odt", "rtf", "html", "txt"]),
        .. Office(Sheets, "Spreadsheet export", ["xlsx", "pdf", "ods", "csv"]),
        .. Office(Slides, "Presentation export", ["pdf", "pptx", "odp"]),
        VideoRoute("mp4", ["libx264", "aac"], ["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac"]),
        VideoRoute("webm", ["libvpx-vp9", "libopus"], ["-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-c:a", "libopus"]),
        VideoRoute("mkv", ["libx264", "aac"], ["-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac"]),
        new("gif", "Animated image", ConversionBackend.Media, Video, ["-map", "0:v:0", "-an", "-vf", "fps=12,scale=640:-1:flags=lanczos", "-c:v", "gif"], ["gif"], Video: true),
        AudioRoute("mp3", "libmp3lame", ["-q:a", "2"]),
        AudioRoute("wav", "pcm_s16le", []),
        AudioRoute("flac", "flac", []),
        AudioRoute("ogg", "libvorbis", ["-q:a", "5"]),
        AudioRoute("m4a", "aac", ["-b:a", "192k"]),
        AudioRoute("opus", "libopus", ["-b:a", "128k"])
    ];

    internal EngineCapabilities Capabilities => capabilities;

    internal static string SourceExtension(string path) => Normalize(Path.GetExtension(path));

    internal static string Normalize(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
    {
        "jpeg" => "jpg",
        "tif" => "tiff",
        "htm" => "html",
        var normalized => normalized
    };

    internal static bool Uses(string path, ConversionBackend backend) =>
        MatchingRoutes(path).Any(route => route.Backend == backend);

    private static IEnumerable<ConversionRoute> MatchingRoutes(string path)
    {
        var source = SourceExtension(path);
        return Routes.Where(route => route.Extension != source && route.Sources.Contains(source));
    }

    internal ConversionRoute? Resolve(string path, string extension) =>
        MatchingRoutes(path).FirstOrDefault(route => route.Extension == Normalize(extension) && route.IsAvailable(capabilities));

    public IReadOnlyList<ConversionOption> GetOptions(string path) => MatchingRoutes(path)
        .Where(route => route.Backend != ConversionBackend.Media ||
            (route.Video ? capabilities.HasVideo : capabilities.HasAudio) != false)
        .GroupBy(route => route.Extension)
        .Select(routes =>
        {
            var available = routes.FirstOrDefault(route => route.IsAvailable(capabilities));
            return new ConversionOption((available ?? routes.First()).Target, available is not null);
        })
        .ToArray();

    public IReadOnlyList<ConversionTarget> GetTargets(string path) => GetOptions(path)
        .Where(option => option.Available)
        .Select(option => option.Target)
        .ToArray();

    private static IEnumerable<ConversionRoute> Office(string[] sources, string detail, string[] extensions) =>
        extensions.Select(extension => new ConversionRoute(extension, detail, ConversionBackend.Office, sources));

    private static ConversionRoute AudioRoute(string extension, string encoder, string[] options) =>
        new(extension, "First audio track", ConversionBackend.Media, Media,
            ["-map", "0:a:0", "-vn", "-c:a", encoder, .. options], [encoder]);

    private static ConversionRoute VideoRoute(string extension, string[] encoders, string[] options) =>
        new(extension, "Video with optional audio", ConversionBackend.Media, Video,
            ["-map", "0:v:0", "-map", "0:a:0?", .. options], encoders, Video: true);
}
