namespace Konvert;

internal enum ConversionBackend { Image, WindowsImage, Text, Table, Office, Media, PdfText, PdfImage, Notebook }

internal sealed record ConversionRoute(
    string Extension,
    string Detail,
    ConversionBackend Backend,
    string[] Sources,
    string[]? Arguments = null,
    string[]? Encoders = null,
    bool Video = false,
    string? Muxer = null,
    string? AudioEncoder = null,
    bool StillImage = false)
{
    private bool IsImage => Backend is ConversionBackend.Image or ConversionBackend.WindowsImage;

    public ConversionTarget Target => new(Extension, Extension.ToUpperInvariant(), Detail, Backend switch
    {
        ConversionBackend.WindowsImage => "Windows image codec",
        ConversionBackend.PdfImage => "Windows PDF",
        ConversionBackend.Office => "LibreOffice",
        ConversionBackend.Media => "FFmpeg",
        _ => "Built in"
    })
    {
        SupportsJpegQuality = (IsImage && Extension is "jpg" or "pdf") || (Backend == ConversionBackend.PdfImage && Extension == "jpg"),
        SupportsDpi = (IsImage && Extension is not ("gif" or "ico")) || Backend == ConversionBackend.PdfImage,
        SupportsImageSize = (IsImage || StillImage || Backend == ConversionBackend.PdfImage) && Extension != "ico",
        SupportsPdfPage = Backend == ConversionBackend.PdfImage && Extension != "zip"
    };

    public bool IsAvailable(EngineCapabilities capabilities) => Backend switch
    {
        ConversionBackend.Office => capabilities.LibreOffice is not null,
        ConversionBackend.WindowsImage => capabilities.HasWindowsImageCodec,
        ConversionBackend.PdfText => capabilities.PdfPageCount is null or <= PdfConverter.MaxTextPages,
        ConversionBackend.PdfImage => capabilities.PdfRenderer is not null && !capabilities.PdfRenderFailed &&
            (Extension != "zip" || capabilities.PdfPageCount is null or <= 100),
        ConversionBackend.Media => capabilities.Ffmpeg is not null &&
            Encoders!.All(capabilities.Encoders.Contains) &&
            (Muxer is null || capabilities.Muxers?.Contains(Muxer) != false) &&
            (AudioEncoder is null || capabilities.HasAudio == false || capabilities.Encoders.Contains(AudioEncoder)) &&
            (Video ? capabilities.HasVideo : capabilities.HasAudio) != false,
        _ => true
    };
}

public sealed class FormatRegistry(EngineCapabilities capabilities)
{
    private static readonly string[] Images = ["png", "jpg", "bmp", "gif", "tiff", "ico", "wdp"];
    private static readonly string[] OptionalImages = ["webp", "heic", "heif", "avif"];
    private static readonly string[] PlainText = ["txt", "md", "log", "json", "xml", "yaml", "yml", "csv", "tsv", "html", "css", "js", "cs", "py", "sql"];
    private static readonly string[] Documents = ["docx", "doc", "odt", "rtf", "txt"];
    private static readonly string[] Sheets = ["xlsx", "xls", "ods", "csv", "tsv"];
    private static readonly string[] Slides = ["pptx", "ppt", "odp"];
    private static readonly string[] Video = ["mp4", "mkv", "mov", "webm", "avi", "wmv", "m4v", "mpeg", "mpg", "ts", "flv", "ogv", "3gp", "vob", "asf"];
    private static readonly string[] Media = [.. Video, "mp3", "wav", "flac", "ogg", "m4a", "aac", "aiff", "wma", "opus", "caf", "mka", "ac3", "eac3", "amr"];
    private const string EvenFrame = "pad=ceil(iw/2)*2:ceil(ih/2)*2";
    private static readonly string[] H264 = ["-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-vf", EvenFrame];
    private static readonly (string Extension, string Detail)[] ImageExports =
    [
        ("png", "Lossless image · transparency"),
        ("jpg", "JPEG image · quality control"),
        ("pdf", "Single-page image PDF"),
        ("tiff", "Lossless image · single frame"),
        ("bmp", "Uncompressed image"),
        ("gif", "256 colors · first frame"),
        ("ico", "Windows icon · six sizes · transparency")
    ];
    private static readonly ConversionRoute[] Routes =
    [
        new("txt", "Extract selectable text from all pages", ConversionBackend.PdfText, ["pdf"]),
        new("png", "Render one page · choose the page in Options", ConversionBackend.PdfImage, ["pdf"]),
        new("docx", "Editable text with page breaks · simplified layout", ConversionBackend.PdfText, ["pdf"]),
        new("jpg", "Render one page as JPEG", ConversionBackend.PdfImage, ["pdf"]),
        new("zip", "Render all pages as numbered PNG images", ConversionBackend.PdfImage, ["pdf"]),
        new("html", "Static notebook with code and text output", ConversionBackend.Notebook, ["ipynb"]),
        new("md", "Markdown and code blocks from the notebook", ConversionBackend.Notebook, ["ipynb"]),
        new("txt", "Notebook text and code cells", ConversionBackend.Notebook, ["ipynb"]),
        .. ImageExports.Select(item => new ConversionRoute(item.Extension, item.Detail, ConversionBackend.Image, Images)),
        .. ImageExports.Select(item => new ConversionRoute(item.Extension, item.Detail, ConversionBackend.WindowsImage, OptionalImages)),
        StillRoute("webp", "Lossless WebP · first frame", "libwebp", ["png", "jpg", "bmp", "gif", "tiff", "ico"], ["-lossless", "1"]),
        StillRoute("png", "Lossless PNG · first frame", "png", ["webp"], []),
        new("pdf", "Document export", ConversionBackend.Office, Documents),
        new("txt", "Extract paragraph text", ConversionBackend.Text, ["docx", "odt"]),
        new("txt", "UTF-8 · preserves source text", ConversionBackend.Text, PlainText),
        new("json", "Rows as JSON objects", ConversionBackend.Table, ["csv", "tsv"]),
        new("tsv", "Tab-separated table", ConversionBackend.Table, ["csv", "json"]),
        new("csv", "Comma-separated table", ConversionBackend.Table, ["tsv", "json"]),
        new("html", "Escaped text · readable page", ConversionBackend.Text, PlainText),
        .. Office(Documents, "Document export", ["docx", "odt", "rtf", "html", "txt"]),
        .. Office(Sheets, "Spreadsheet export", ["xlsx", "pdf", "ods", "csv"]),
        .. Office(Slides, "Presentation export", ["pdf", "pptx", "odp"]),
        VideoRoute("mp4", "mp4", "libx264", "aac", H264),
        VideoRoute("webm", "webm", "libvpx-vp9", "libopus", ["-crf", "32", "-b:v", "0"]),
        VideoRoute("mkv", "matroska", "libx264", "aac", H264),
        VideoRoute("mov", "mov", "libx264", "aac", H264),
        VideoRoute("avi", "avi", "mpeg4", "libmp3lame", ["-q:v", "4", "-pix_fmt", "yuv420p", "-vf", EvenFrame]),
        VideoRoute("ts", "mpegts", "libx264", "aac", H264),
        new("gif", "Animated image", ConversionBackend.Media, Video, ["-map", "0:v:0", "-an", "-vf", "fps=12,scale=640:-1:flags=lanczos", "-c:v", "gif"], ["gif"], Video: true, Muxer: "gif"),
        AudioRoute("mp3", "mp3", "libmp3lame", ["-q:a", "2"]),
        AudioRoute("wav", "wav", "pcm_s16le", []),
        AudioRoute("flac", "flac", "flac", []),
        AudioRoute("ogg", "ogg", "libvorbis", ["-q:a", "5"]),
        AudioRoute("m4a", "ipod", "aac", ["-b:a", "192k"]),
        AudioRoute("opus", "opus", "libopus", ["-b:a", "128k"]),
        AudioRoute("aac", "adts", "aac", ["-b:a", "192k"]),
        AudioRoute("aiff", "aiff", "pcm_s16be", []),
        AudioRoute("caf", "caf", "pcm_s16le", [])
    ];

    internal EngineCapabilities Capabilities => capabilities;

    internal static string SourceExtension(string path) => Normalize(Path.GetExtension(path));

    internal static string Normalize(string extension) => extension.TrimStart('.').ToLowerInvariant() switch
    {
        "jpeg" => "jpg",
        "tif" => "tiff",
        "htm" => "html",
        "aif" or "aifc" => "aiff",
        "jxr" or "hdp" => "wdp",
        "m2ts" or "mts" => "ts",
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
            var route = available ?? routes.First();
            return new ConversionOption(route.Target, available is not null)
            {
                UnavailableReason = available is not null ? null : route.Backend switch
                {
                    ConversionBackend.WindowsImage => "Windows could not read this image. Check the installed image extensions or verify the file.",
                    ConversionBackend.PdfText => "Text export supports up to 500 PDF pages.",
                    ConversionBackend.PdfImage when capabilities.PdfRenderer is null => "Repair Konvert to restore the PDF renderer.",
                    ConversionBackend.PdfImage when capabilities.PdfRenderFailed => "Windows could not read this PDF. Check that it opens without a password.",
                    ConversionBackend.PdfImage => "The image archive supports up to 100 pages. Use PNG or JPG for an individual page.",
                    ConversionBackend.Media when capabilities.Ffmpeg is not null => "This FFmpeg build is missing a required encoder or container.",
                    _ => $"Set up {route.Target.Engine} in Preferences."
                }
            };
        })
        .ToArray();

    public IReadOnlyList<ConversionTarget> GetTargets(string path) => GetOptions(path)
        .Where(option => option.Available)
        .Select(option => option.Target)
        .ToArray();

    private static IEnumerable<ConversionRoute> Office(string[] sources, string detail, string[] extensions) =>
        extensions.Select(extension => new ConversionRoute(extension, detail, ConversionBackend.Office, sources));

    private static ConversionRoute AudioRoute(string extension, string muxer, string encoder, string[] options) =>
        new(extension, "First audio track", ConversionBackend.Media, Media,
            ["-map", "0:a:0", "-vn", "-c:a", encoder, .. options], [encoder], Muxer: muxer);

    private static ConversionRoute VideoRoute(string extension, string muxer, string videoEncoder, string audioEncoder, string[] options) =>
        new(extension, "Video with optional audio", ConversionBackend.Media, Video,
            ["-map", "0:v:0", "-map", "0:a:0?", "-c:v", videoEncoder, .. options], [videoEncoder],
            Video: true, Muxer: muxer, AudioEncoder: audioEncoder);

    private static ConversionRoute StillRoute(string extension, string detail, string encoder, string[] sources, string[] options) =>
        new(extension, detail, ConversionBackend.Media, sources,
            ["-map", "0:v:0", "-frames:v", "1", "-an", "-c:v", encoder, "-update", "1", .. options], [encoder],
            Video: true, Muxer: "image2", StillImage: true);
}
