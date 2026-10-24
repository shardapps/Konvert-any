namespace Konvert;

public sealed class FormatRegistry(EngineCapabilities capabilities)
{
    public static readonly string[] Images = ["png", "jpg", "jpeg", "bmp", "gif", "tif", "tiff", "ico", "wdp"];
    public static readonly string[] Text = ["txt", "md", "log", "json", "xml", "yaml", "yml", "csv", "tsv", "html", "htm", "css", "js", "cs", "py", "sql"];
    public static readonly string[] Documents = ["docx", "doc", "odt", "rtf", "txt"];
    public static readonly string[] Sheets = ["xlsx", "xls", "ods", "csv", "tsv"];
    public static readonly string[] Slides = ["pptx", "ppt", "odp"];
    public static readonly string[] Audio = ["mp3", "wav", "flac", "ogg", "m4a", "aac", "aiff", "wma", "opus"];
    public static readonly string[] Video = ["mp4", "mkv", "mov", "webm", "avi", "wmv", "m4v", "mpeg", "mpg"];

    public IReadOnlyList<ConversionTarget> GetTargets(string path)
    {
        var source = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        var targets = new List<ConversionTarget>();
        void Add(string extension, string detail, string engine = "Built in")
        {
            if (extension != source && !targets.Any(t => t.Extension == extension))
                targets.Add(new(extension, extension.ToUpperInvariant(), detail, engine));
        }

        if (Images.Contains(source))
        {
            foreach (var extension in new[] { "png", "jpg", "bmp", "tiff", "gif", "pdf" })
                Add(extension, extension switch
                {
                    "png" => "Lossless · transparency",
                    "jpg" => "Compact · quality control",
                    "pdf" => "Single page · white background",
                    "tiff" => "Lossless · single frame",
                    "gif" => "256 colors · first frame",
                    _ => "Uncompressed · first frame"
                });
        }
        if (Text.Contains(source))
        {
            Add("txt", "UTF-8 · preserves source text");
            if (source is not "html" and not "htm") Add("html", "Escaped text · readable page");
        }
        if (source == "docx") Add("txt", "Extract paragraph text");
        if (source is "csv" or "tsv") Add("json", "Rows as JSON objects");
        if (source == "csv") Add("tsv", "Tab-separated table");
        if (source == "tsv") Add("csv", "Comma-separated table");
        if (source == "json") Add("csv", "Array of flat objects");

        if (capabilities.LibreOffice is not null)
        {
            if (Documents.Contains(source))
                foreach (var extension in new[] { "pdf", "docx", "odt", "rtf", "html", "txt" }) Add(extension, "Document export", "LibreOffice");
            if (Sheets.Contains(source))
                foreach (var extension in new[] { "pdf", "xlsx", "ods", "csv" }) Add(extension, "Spreadsheet export", "LibreOffice");
            if (Slides.Contains(source))
                foreach (var extension in new[] { "pdf", "pptx", "odp" }) Add(extension, "Presentation export", "LibreOffice");
        }
        if (Audio.Contains(source) || Video.Contains(source))
        {
            foreach (var extension in new[] { "mp3", "wav", "flac", "ogg", "m4a", "opus" })
                if (capabilities.CanEncode(extension)) Add(extension, "Audio · first audio track", "FFmpeg");
            if (Video.Contains(source))
                foreach (var extension in new[] { "mp4", "webm", "mkv", "gif" })
                    if (capabilities.CanEncode(extension)) Add(extension, "Video · bounded worker threads", "FFmpeg");
        }
        return targets;
    }
}
