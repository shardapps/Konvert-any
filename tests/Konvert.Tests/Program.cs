using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Konvert;

internal static class Program
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), $"Konvert-tests-{Guid.NewGuid():N}");
    private static int failures;
    private static int passed;

    [STAThread]
    public static int Main(string[] args)
    {
        Directory.CreateDirectory(Root);
        try { RunAsync(args).GetAwaiter().GetResult(); }
        finally { Directory.Delete(Root, true); }
        Console.WriteLine($"\n{passed} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }

    private static async Task RunAsync(string[] args)
    {
        await Test("Both windows apply the dark theme to their derived Window types", () =>
        {
            var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var window = new MainWindow();
            var settingsWindow = new SettingsWindow(new(), new(null, null, new HashSet<string>()));
            Equal(Color.FromRgb(17, 22, 28), ((SolidColorBrush)window.Background).Color);
            Equal(Color.FromRgb(17, 22, 28), ((SolidColorBrush)settingsWindow.Background).Color);
            settingsWindow.Close(); window.Close();
        });
        var none = new EngineCapabilities(null, null, new HashSet<string>());
        using var service = new ConversionService(none);
        await Test("Registry gates optional engines and handles case", () =>
        {
            var registry = new FormatRegistry(none);
            Equal(0, registry.GetTargets("music.mp3").Count);
            True(registry.GetTargets("photo.PNG").Any(t => t.Extension == "pdf"));
            True(registry.GetTargets("report.docx").All(t => t.Extension == "txt"));
            Equal(0, registry.GetTargets("unknown.exe").Count);
        });
        await Test("CSV parser handles multiline, quotes, trailing fields", () =>
        {
            var rows = TableConverter.Parse("a,b,c\r\n\"one\ntwo\",\"a\"\"b\",\r\n", ',');
            Equal(2, rows.Count); Equal("one\ntwo", rows[1][0]); Equal("a\"b", rows[1][1]); Equal("", rows[1][2]);
        });
        await Test("CSV parser rejects malformed quotes", () =>
        {
            Throws<InvalidDataException>(() => TableConverter.Parse("a\n\"unterminated", ','));
            Throws<InvalidDataException>(() => TableConverter.Parse("a\n\"x\"oops", ','));
        });
        await TestAsync("HTML escapes active content and preserves Unicode", async () =>
        {
            var source = Put("notes.txt", "<script>alert('x')</script>\nسلام 😀");
            var result = await service.ConvertAsync(source, "html", new());
            var html = File.ReadAllText(result.OutputPath);
            True(!html.Contains("<script>")); True(html.Contains("&lt;script&gt;")); True(html.Contains("سلام"));
        });
        await TestAsync("Existing destination and source stay unchanged", async () =>
        {
            var source = Put("collision.md", "original");
            Put("collision.txt", "keep");
            var results = await Task.WhenAll(service.ConvertAsync(source, "txt", new()), service.ConvertAsync(source, "txt", new()));
            Equal("keep", File.ReadAllText(Path.Combine(Root, "collision.txt")));
            Equal("original", File.ReadAllText(source)); True(results[0].OutputPath != results[1].OutputPath);
        });
        await TestAsync("CSV to JSON keeps every column as text", async () =>
        {
            var source = Put("table.csv", "id,name\n001,\"hello, world\"\n");
            var result = await service.ConvertAsync(source, "json", new());
            using var json = JsonDocument.Parse(File.ReadAllText(result.OutputPath));
            Equal("001", json.RootElement[0].GetProperty("id").GetString());
            Equal("hello, world", json.RootElement[0].GetProperty("name").GetString());
            var back = await service.ConvertAsync(result.OutputPath, "csv", new());
            Equal("hello, world", TableConverter.Parse(File.ReadAllText(back.OutputPath), ',')[1][1]);
        });
        await TestAsync("Ragged CSV fails and cleans staging output", async () =>
        {
            var source = Put("ragged.csv", "a,b\n1,2,3\n");
            await ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(source, "json", new()));
            True(!Directory.EnumerateFiles(Root, ".konvert-*").Any());
        });
        await TestAsync("Nested JSON fails instead of losing information", async () =>
            await ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(Put("nested.json", "[{\"a\":{\"b\":1}}]"), "csv", new())));
        await TestAsync("Duplicate JSON and CSV keys are rejected", async () =>
        {
            await ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(Put("duplicate.json", "[{\"a\":1,\"a\":2}]"), "csv", new()));
            await ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(Put("duplicate.csv", "a,a\n1,2"), "json", new()));
        });
        await TestAsync("Pre-cancelled work produces no output", async () =>
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await ThrowsAsync<OperationCanceledException>(() => service.ConvertAsync(Put("cancel.md", "hello"), "txt", new(), cancel.Token));
            True(!File.Exists(Path.Combine(Root, "cancel.txt")));
        });
        await TestAsync("Missing and unsupported inputs fail clearly", async () =>
        {
            await ThrowsAsync<FileNotFoundException>(() => service.ConvertAsync(Path.Combine(Root, "missing.txt"), "html", new()));
            await ThrowsAsync<NotSupportedException>(() => service.ConvertAsync(Put("source.exe", "test"), "pdf", new()));
        });
        await TestAsync("Text size limit prevents whole-table allocation", async () =>
        {
            var path = Path.Combine(Root, "large.txt"); using (var stream = File.Create(path)) stream.SetLength(TextConverter.MaxInputBytes + 1);
            await ThrowsAsync<InvalidDataException>(() => service.ConvertAsync(path, "html", new()));
        });
        await TestAsync("DOCX extraction preserves paragraphs, tabs and Unicode", async () =>
        {
            var source = MakeDocx("document.docx", "<w:p><w:r><w:t>Hello</w:t><w:tab/><w:t>سلام</w:t></w:r></w:p><w:p><w:r><w:t>World</w:t></w:r></w:p>");
            var result = await service.ConvertAsync(source, "txt", new());
            Equal($"Hello\tسلام{Environment.NewLine}World{Environment.NewLine}", File.ReadAllText(result.OutputPath));
        });
        await TestAsync("DOCX XML rejects external entities", async () =>
        {
            var path = Path.Combine(Root, "xxe.docx");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("word/document.xml").Open()))
                writer.Write("<!DOCTYPE x [<!ENTITY leak SYSTEM 'file:///C:/Windows/win.ini'>]><x>&leak;</x>");
            await ThrowsAsync<System.Xml.XmlException>(() => service.ConvertAsync(path, "txt", new()));
        });
        await Test("Corrupt settings recover and values are bounded", () =>
        {
            var directory = Path.Combine(Root, "settings"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{oops");
            var store = new SettingsStore(directory); Equal(90, store.Load().JpegQuality);
            store.Save(new() { JpegQuality = 999, Dpi = -1, ReducedMotion = true });
            Equal(100, store.Load().JpegQuality); Equal(72, store.Load().Dpi); True(store.Load().ReducedMotion);
        });
        var image = MakeImage();
        foreach (var extension in new[] { "jpg", "bmp", "tiff", "gif", "pdf" })
            await TestAsync($"Real PNG → {extension} conversion", async () =>
            {
                var result = await service.ConvertAsync(image, extension, new() { Dpi = 144 });
                if (extension == "pdf")
                {
                    var data = File.ReadAllBytes(result.OutputPath); True(Encoding.ASCII.GetString(data[..8]).StartsWith("%PDF-1.4"));
                    True(Encoding.ASCII.GetString(data).Contains("/MediaBox [0 0 16 12]"));
                }
                else
                {
                    using var stream = File.OpenRead(result.OutputPath);
                    var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                    Equal(32, decoder.Frames[0].PixelWidth); Equal(24, decoder.Frames[0].PixelHeight);
                    if (extension != "gif") True(Math.Abs(decoder.Frames[0].DpiX - 144) < 1);
                }
            });
        await TestAsync("Invalid image fails without partial output", async () =>
        {
            await ThrowsAsync<Exception>(() => service.ConvertAsync(Put("invalid.png", "not an image"), "jpg", new()));
            True(!Directory.EnumerateFiles(Root, ".konvert-*").Any());
        });
        await TestAsync("External process timeout terminates worker", async () =>
            await ThrowsAsync<TimeoutException>(() => ExternalProcess.RunAsync("ping.exe", ["-n", "20", "127.0.0.1"], TimeSpan.FromMilliseconds(100), CancellationToken.None)));

        var capabilities = await EngineCapabilities.DetectAsync(new());
        if (capabilities.Ffmpeg is null)
        {
            if (args.Contains("--require-ffmpeg")) { failures++; Console.WriteLine("FAIL FFmpeg is required but missing"); }
            else Console.WriteLine("SKIP optional FFmpeg integration (not installed)");
        }
        else
        {
            using var media = new ConversionService(capabilities);
            var wav = Path.Combine(Root, "tone.wav");
            await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.2", wav], TimeSpan.FromSeconds(20), CancellationToken.None);
            foreach (var target in new FormatRegistry(capabilities).GetTargets(wav).Where(t => t.Engine == "FFmpeg"))
                await TestAsync($"FFmpeg WAV → {target.Extension} decodes correctly", async () =>
                {
                    var result = await media.ConvertAsync(wav, target.Extension, new());
                    await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-i", result.OutputPath, "-f", "null", "-"], TimeSpan.FromSeconds(20), CancellationToken.None);
                });
            var video = Path.Combine(Root, "clip.avi");
            await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "color=c=cyan:s=64x64:d=0.2", "-c:v", "mpeg4", video], TimeSpan.FromSeconds(20), CancellationToken.None);
            foreach (var target in new FormatRegistry(capabilities).GetTargets(video).Where(t => t.Engine == "FFmpeg" && new[] { "mp4", "mkv", "webm", "gif" }.Contains(t.Extension)))
                await TestAsync($"FFmpeg video → {target.Extension} decodes correctly", async () =>
                {
                    var result = await media.ConvertAsync(video, target.Extension, new());
                    await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-i", result.OutputPath, "-f", "null", "-"], TimeSpan.FromSeconds(20), CancellationToken.None);
                });
        }
        if (capabilities.LibreOffice is not null)
        {
            using var documents = new ConversionService(capabilities);
            foreach (var extension in new[] { "pdf", "odt", "rtf", "html" })
                await TestAsync($"LibreOffice DOCX → {extension}", async () =>
                {
                    var result = await documents.ConvertAsync(Path.Combine(Root, "document.docx"), extension, new());
                    True(result.OutputBytes > 0);
                    if (extension == "pdf") True(File.ReadAllText(result.OutputPath).StartsWith("%PDF-"));
                });
        }
        else if (args.Contains("--require-office")) { failures++; Console.WriteLine("FAIL LibreOffice is required but missing"); }
        else Console.WriteLine("SKIP optional LibreOffice integration (not installed)");
    }

    private static string MakeDocx(string name, string content)
    {
        var path = Path.Combine(Root, name);
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/word/document.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\"/></Types>");
        Add("_rels/.rels", "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>");
        Add("word/document.xml", $"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>{content}</w:body></w:document>");
        return path;
        void Add(string entry, string text) { using var writer = new StreamWriter(archive.CreateEntry(entry).Open()); writer.Write(text); }
    }
    private static string MakeImage()
    {
        var path = Path.Combine(Root, "sample.png");
        var data = new byte[32 * 24 * 4]; for (var i = 0; i < data.Length; i += 4) { data[i] = 240; data[i + 1] = 180; data[i + 2] = 80; data[i + 3] = 255; }
        var source = BitmapSource.Create(32, 24, 96, 96, PixelFormats.Bgra32, null, data, 32 * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path); encoder.Save(stream); return path;
    }
    private static string Put(string name, string text) { var path = Path.Combine(Root, name); File.WriteAllText(path, text); return path; }
    private static Task Test(string name, Action action) => TestAsync(name, () => { action(); return Task.CompletedTask; });
    private static async Task TestAsync(string name, Func<Task> action)
    {
        try { await action(); passed++; Console.WriteLine($"PASS {name}"); }
        catch (Exception ex) { failures++; Console.WriteLine($"FAIL {name}: {ex}"); }
    }
    private static void True(bool condition) { if (!condition) throw new InvalidOperationException("Assertion failed."); }
    private static void Equal<T>(T expected, T actual) { if (!EqualityComparer<T>.Default.Equals(expected, actual)) throw new InvalidOperationException($"Expected '{expected}', got '{actual}'."); }
    private static void Throws<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
    private static async Task ThrowsAsync<T>(Func<Task> action) where T : Exception { try { await action(); } catch (T) { return; } throw new InvalidOperationException($"Expected {typeof(T).Name}."); }
}
