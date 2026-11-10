using System.Diagnostics;
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
        if (args is ["--hold-pipe"])
        {
            Thread.Sleep(TimeSpan.FromSeconds(15));
            return 0;
        }
        if (args is ["--spawn-pipe-holder", var pidFile])
        {
            var start = SelfStart("--hold-pipe");
            start.RedirectStandardInput = true; // Keep the launcher's stdout and stderr handles in the child.
            using var child = Process.Start(start)!;
            File.WriteAllText(pidFile, child.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Thread.Sleep(150);
            return 0;
        }
        Directory.CreateDirectory(Root);
        try { RunAsync(args).GetAwaiter().GetResult(); }
        finally { Directory.Delete(Root, true); }
        Console.WriteLine($"\n{passed} passed, {failures} failed");
        return failures == 0 ? 0 : 1;
    }

    private static async Task RunAsync(string[] args)
    {
        await Test("Glass defaults, frameless popup and custom settings windows", () =>
        {
            var app = new App { ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown };
            app.InitializeComponent();
            var window = new MainWindow(preferences: new());
            var popup = new MainWindow(Put("popup.txt", "Preview fixture"), preferences: new());
            var settingsWindow = new SettingsWindow(new(), new(null, null, new HashSet<string>()));
            True(((SolidColorBrush)window.Background).Color.A < 255);
            Equal(System.Windows.WindowStyle.None, settingsWindow.WindowStyle);
            True(popup.AllowsTransparency);
            Equal(224.0, popup.Width); Equal(224.0, popup.Height);
            settingsWindow.Close(); popup.Close(); window.Close();
        });
        await Test("Picker explains missing and unsupported inputs and opens converter setup", () => PickerTests.VerifyRecovery(Root));
        await Test("Click-away dismisses the first-launch picker", PickerTests.VerifyHomeDismissal);
        await Test("Picker quick and expanded actions save to their respective folders", () => PickerTests.VerifySavePaths(Root));
        await Test("Picker shows conversion errors and can retry another route", () => PickerTests.VerifyFailedJobCanRetry(Root));
        await Test("Dismissing a busy popup hides it and preserves the conversion", () => PickerTests.VerifyBackgroundSave(Root));
        await Test("Seven persistent themes and high contrast fallback", () =>
        {
            Equal(7, ThemeManager.Themes.Count);
            foreach (var theme in ThemeManager.Themes)
            {
                var resources = new System.Windows.ResourceDictionary();
                ThemeManager.Apply(theme.Id, resources, false);
                Equal(theme.Id, ThemeManager.Current.Id);
                True(Contrast("Ink", "TooltipFill") >= 4.5);
                True(Contrast("Muted", "Raised") >= 4.5);
                True(Contrast("OnAccent", "Accent") >= 4.5);
                Equal(theme.Id, (new ConversionSettings { Theme = theme.Id }).Validate().Theme);

                double Contrast(string foreground, string background)
                {
                    var first = Luminance(((SolidColorBrush)resources[foreground]).Color);
                    var second = Luminance(((SolidColorBrush)resources[background]).Color);
                    return (Math.Max(first, second) + 0.05) / (Math.Min(first, second) + 0.05);
                }
            }
            var contrast = new System.Windows.ResourceDictionary();
            ThemeManager.Apply("glass", contrast, true);
            Equal(System.Windows.SystemColors.WindowBrush, contrast["Surface"]);
            Equal(System.Windows.SystemColors.WindowBrush, contrast["LensFill"]);
            ThemeManager.Apply("glass", contrast, false, reducedTransparency: true);
            Equal((byte)255, ((SolidColorBrush)contrast["LensFill"]).Color.A);
            True(((RadialGradientBrush)contrast["LensLight"]).GradientStops.All(stop => stop.Color.A == 0));
            Equal("glass", (new ConversionSettings { Theme = "invalid" }).Validate().Theme);

            static double Luminance(Color color) => 0.2126 * color.ScR + 0.7152 * color.ScG + 0.0722 * color.ScB;
        });
        await Test("Popup clamps to negative-coordinate and small monitor work areas", () =>
        {
            var left = WindowEffects.Clamp(-1910, 10, 560, 560, -1920, 0, 0, 1080);
            Equal(-1910.0, left.Left); Equal(10.0, left.Top);
            var right = WindowEffects.Clamp(1910, 1070, 560, 560, 0, 0, 1920, 1080);
            Equal(1350.0, right.Left); Equal(510.0, right.Top);
        });
        await Test("The picker opens below the selected row and flips above the taskbar", () =>
        {
            var desktop = new System.Windows.Rect(0, 0, 900, 760);
            var below = WindowEffects.FitBesideSelection(new(400, 160, 130), new(224, 470), 224, desktop);
            Equal(168.0, below.Top);
            var above = WindowEffects.FitBesideSelection(new(855, 690, 660), new(224, 400), 224, desktop);
            Equal(652.0, above.Bottom);
            Equal(890.0, above.Right);
            var shortDrawer = WindowEffects.FitBesideSelection(new(400, 450, 422), new(224, 470), 224, desktop);
            Equal(458.0, shortDrawer.Top);
            Equal(750.0, shortDrawer.Bottom);
            var drawerAtEdge = WindowEffects.FitBesideSelection(new(400, 512, 484), new(224, 470), 224, desktop);
            Equal(476.0, drawerAtEdge.Bottom);
        });
        await Test("Unavailable office routes explain engine setup rather than disappearing", () =>
        {
            var catalog = new FormatRegistry(new(null, null, new HashSet<string>())).GetOptions("sample.docx");
            True(catalog.Any(option => option.Target.Extension == "pdf" && !option.Available));
            True(catalog.Any(option => option.Target.Extension == "txt" && option.Available));
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
        await Test("Resolved routes honor aliases and actual media streams", () =>
        {
            var capabilities = new EngineCapabilities(null, "ffmpeg.exe", new HashSet<string> { "libx264", "aac", "libmp3lame" })
            {
                HasAudio = false,
                HasVideo = true
            };
            var registry = new FormatRegistry(capabilities);
            True(registry.GetTargets("clip.avi").Any(target => target.Extension == "mp4"));
            True(!registry.GetOptions("clip.avi").Any(option => option.Target.Extension == "mp3"));
            True(registry.Resolve("clip.avi", "mp3") is null);
            True(registry.Resolve("photo.JPEG", "jpg") is null);
            Equal("jpg", registry.Resolve("photo.png", ".JPEG")!.Extension);
        });
        await Test("Video requires the installed container and only encoders for present tracks", () =>
        {
            var videoOnly = new EngineCapabilities(null, "ffmpeg.exe", new HashSet<string> { "libx264" })
            {
                HasVideo = true,
                HasAudio = false,
                Muxers = new HashSet<string> { "mov" }
            };
            True(new FormatRegistry(videoOnly).GetTargets("video.mp4").Any(target => target.Extension == "mov"));
            True(new FormatRegistry(videoOnly).GetTargets("video.mp4").All(target => target.Extension != "mkv"));
            True(new FormatRegistry(videoOnly with { HasAudio = true }).GetTargets("video.mp4").All(target => target.Extension != "mov"));
        });
        await TestAsync("Unreadable modern image offers codec guidance without a false-ready export", async () =>
        {
            var input = Put("broken.heic", "not an image");
            var capabilities = await EngineCapabilities.DetectAsync(new(), source: input);
            var options = new FormatRegistry(capabilities).GetOptions(input);
            True(options.Any(option => option.Target.Extension == "jpg" && !option.Available &&
                option.UnavailableReason!.Contains("Windows could not read")));
            True(options.All(option => !option.Available));
        });
        await Test("CSV parser handles multiline, quotes, trailing fields", () =>
        {
            var rows = TableConverter.Parse("a,b,c\r\n\"one\ntwo\",\"a\"\"b\",\r\n", ',').ToList();
            Equal(2, rows.Count); Equal("one\ntwo", rows[1][0]); Equal("a\"b", rows[1][1]); Equal("", rows[1][2]);
        });
        await Test("CSV parser rejects malformed quotes", () =>
        {
            Throws<InvalidDataException>(() => TableConverter.Parse("a\n\"unterminated", ',').ToList());
            Throws<InvalidDataException>(() => TableConverter.Parse("a\n\"x\"oops", ',').ToList());
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
        await TestAsync("HTML preserves emoji across streaming chunk boundaries", async () =>
        {
            var text = new string('a', 8191) + "😀tail";
            var result = await service.ConvertAsync(Put("boundary.txt", text), "html", new());
            var html = File.ReadAllText(result.OutputPath);
            var content = html[(html.IndexOf("<pre>", StringComparison.Ordinal) + 5)..html.LastIndexOf("</pre>", StringComparison.Ordinal)];
            Equal(text, System.Net.WebUtility.HtmlDecode(content));
        });
        await TestAsync("CSV to JSON keeps every column as text", async () =>
        {
            var source = Put("table.csv", "id,name\n001,\"hello, world\"\n");
            var result = await service.ConvertAsync(source, "json", new());
            using var json = JsonDocument.Parse(File.ReadAllText(result.OutputPath));
            Equal("001", json.RootElement[0].GetProperty("id").GetString());
            Equal("hello, world", json.RootElement[0].GetProperty("name").GetString());
            var back = await service.ConvertAsync(result.OutputPath, "csv", new());
            Equal("hello, world", TableConverter.Parse(File.ReadAllText(back.OutputPath), ',').ElementAt(1)[1]);
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
        await TestAsync("ODT extracts displayed paragraphs, tabs and spacing without LibreOffice", async () =>
        {
            var path = Path.Combine(Root, "native.odt");
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            using (var writer = new StreamWriter(archive.CreateEntry("content.xml").Open()))
                writer.Write("""
                    <office:document-content xmlns:office="urn:oasis:names:tc:opendocument:xmlns:office:1.0"
                      xmlns:text="urn:oasis:names:tc:opendocument:xmlns:text:1.0">
                      <office:body><office:text>
                        <text:tracked-changes><text:p>deleted</text:p></text:tracked-changes>
                        <text:h>Résumé</text:h>
                        <text:p>Alpha<text:s text:c="2"/><text:span>β</text:span><text:tab/>One<office:annotation><text:p>comment</text:p></office:annotation><text:line-break/>Two</text:p>
                      </office:text></office:body>
                    </office:document-content>
                    """);
            var output = await service.ConvertAsync(path, "txt", new());
            Equal($"Résumé{Environment.NewLine}Alpha  β\tOne{Environment.NewLine}Two{Environment.NewLine}", File.ReadAllText(output.OutputPath));
        });
        await TestAsync("Flat JSON exports a quoted TSV with the same scalar values", async () =>
        {
            var source = Put("tsv-export.json", """[{"name":"two\tparts","count":2},{"name":"one","count":null}]""");
            var output = await service.ConvertAsync(source, "tsv", new());
            var rows = TableConverter.Parse(File.ReadAllText(output.OutputPath), '\t').ToArray();
            Equal("two\tparts", rows[1][0]); Equal("2", rows[1][1]); Equal("", rows[2][1]);
        });
        await Test("Corrupt settings recover and values are bounded", () =>
        {
            var directory = Path.Combine(Root, "settings"); Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "settings.json"), "{oops");
            var store = new SettingsStore(directory); Equal(90, store.Load().JpegQuality);
            store.Save(new() { JpegQuality = 999, Dpi = -1, MaxImageEdge = 4096, ReducedMotion = true, ReducedTransparency = true });
            var saved = store.Load();
            Equal(100, saved.JpegQuality); Equal(72, saved.Dpi); Equal(3840, saved.MaxImageEdge);
            True(saved.ReducedMotion && saved.ReducedTransparency);
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
        await TestAsync("ICO exports six alpha-aware sizes and reopens at the largest frame", async () =>
        {
            var icon = await service.ConvertAsync(image, "ico", new());
            using (var file = File.OpenRead(icon.OutputPath))
            {
                var decoder = new IconBitmapDecoder(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                Equal("16,32,48,64,128,256", string.Join(',', decoder.Frames.Select(frame => frame.PixelWidth).Order()));
                var largest = decoder.Frames.MaxBy(frame => frame.PixelWidth)!;
                var pixels = new FormatConvertedBitmap(largest, PixelFormats.Bgra32, null, 0);
                var corner = new byte[4];
                pixels.CopyPixels(new System.Windows.Int32Rect(0, 0, 1, 1), corner, 4, 0);
                Equal((byte)0, corner[3]);
            }
            var png = await service.ConvertAsync(icon.OutputPath, "png", new());
            using var stream = File.OpenRead(png.OutputPath);
            var reopened = new PngBitmapDecoder(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            Equal(256, reopened.Frames[0].PixelWidth);
        });
        var largeImage = MakeImage("wide.png", 180, 90);
        await TestAsync("Image size shrinks the longest edge and keeps DPI and proportions", async () =>
        {
            var result = await service.ConvertAsync(largeImage, "jpg", new() { MaxImageEdge = 64, Dpi = 200 });
            using var file = File.OpenRead(result.OutputPath);
            var frame = new JpegBitmapDecoder(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Equal(64, frame.PixelWidth); Equal(32, frame.PixelHeight); True(Math.Abs(frame.DpiX - 200) < 1);
            var noUpscale = await service.ConvertAsync(image, "jpg", new() { MaxImageEdge = 512 });
            using var smaller = File.OpenRead(noUpscale.OutputPath);
            var unchanged = new JpegBitmapDecoder(smaller, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Equal(32, unchanged.PixelWidth); Equal(24, unchanged.PixelHeight);
        });
        await TestAsync("Invalid image fails without partial output", async () =>
        {
            await ThrowsAsync<Exception>(() => service.ConvertAsync(Put("invalid.png", "not an image"), "jpg", new()));
            True(!Directory.EnumerateFiles(Root, ".konvert-*").Any());
        });
        await TestAsync("External process timeout terminates worker", async () =>
            await ThrowsAsync<TimeoutException>(() => ExternalProcess.RunAsync("ping.exe", ["-n", "20", "127.0.0.1"], TimeSpan.FromMilliseconds(100), CancellationToken.None)));
        await TestAsync("An exited launcher cannot hold the conversion open through an inherited pipe", async () =>
        {
            var pidFile = Path.Combine(Root, "pipe-holder.pid");
            var launcher = SelfStart("--spawn-pipe-holder", pidFile);
            var elapsed = Stopwatch.StartNew();
            try
            {
                await ThrowsAsync<TimeoutException>(() => ExternalProcess.RunAsync(launcher.FileName, launcher.ArgumentList,
                    TimeSpan.FromSeconds(2), CancellationToken.None));
                True(File.Exists(pidFile));
                True(elapsed.Elapsed < TimeSpan.FromSeconds(6));
            }
            finally
            {
                if (File.Exists(pidFile) && int.TryParse(File.ReadAllText(pidFile), out var pid))
                {
                    try
                    {
                        using var child = Process.GetProcessById(pid);
                        if (!child.WaitForExit(2000))
                        {
                            child.Kill();
                            throw new InvalidOperationException("The inherited-pipe child escaped process cleanup.");
                        }
                    }
                    catch (ArgumentException) { /* The child already exited. */ }
                }
            }
        });

        var capabilities = await EngineCapabilities.DetectAsync(new());
        if (capabilities.Ffmpeg is null)
        {
            if (args.Contains("--require-ffmpeg")) { failures++; Console.WriteLine("FAIL FFmpeg is required but missing"); }
            else Console.WriteLine("SKIP optional FFmpeg integration (not installed)");
        }
        else
        {
            using var media = new ConversionService(capabilities);
            var registry = new FormatRegistry(capabilities);
            var wav = Path.Combine(Root, "tone.wav");
            await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-f", "lavfi", "-i", "sine=frequency=440:duration=0.2", wav], TimeSpan.FromSeconds(20), CancellationToken.None);
            if (args.Contains("--require-ffmpeg"))
                await Test("Required FFmpeg audio containers are detected", () =>
                    True(new[] { "aac", "aiff", "caf", "mp3", "flac", "ogg", "m4a", "opus" }.All(ext => registry.Resolve(wav, ext) is not null)));
            foreach (var target in registry.GetTargets(wav).Where(t => t.Engine == "FFmpeg"))
                await TestAsync($"FFmpeg WAV → {target.Extension} decodes correctly", async () =>
                {
                    var result = await media.ConvertAsync(wav, target.Extension, new());
                    await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-i", result.OutputPath, "-f", "null", "-"], TimeSpan.FromSeconds(20), CancellationToken.None);
                });
            var video = Path.Combine(Root, "clip.m4v");
            await ExternalProcess.RunAsync(capabilities.Ffmpeg,
                ["-v", "error", "-f", "lavfi", "-i", "color=c=cyan:s=64x64:d=0.4", "-i", wav, "-c:v", "mpeg4", "-c:a", "aac", "-f", "mp4", video],
                TimeSpan.FromSeconds(20), CancellationToken.None);
            string[] videoTargets = ["mp4", "mkv", "webm", "gif", "mov", "avi", "ts"];
            if (args.Contains("--require-ffmpeg"))
                await Test("Required FFmpeg video containers are detected", () => True(videoTargets.All(ext => registry.Resolve(video, ext) is not null)));
            foreach (var target in registry.GetTargets(video).Where(t => t.Engine == "FFmpeg" && videoTargets.Contains(t.Extension)))
                await TestAsync($"FFmpeg video → {target.Extension} decodes correctly", async () =>
                {
                    var result = await media.ConvertAsync(video, target.Extension, new());
                    await ExternalProcess.RunAsync(capabilities.Ffmpeg, ["-v", "error", "-i", result.OutputPath, "-f", "null", "-"], TimeSpan.FromSeconds(20), CancellationToken.None);
                });
            if (args.Contains("--require-ffmpeg") || registry.Resolve(largeImage, "webp") is not null)
                await TestAsync("Lossless WebP export resizes and round-trips through FFmpeg", async () =>
                {
                    var webp = await media.ConvertAsync(largeImage, "webp", new() { MaxImageEdge = 64 });
                    var detected = await EngineCapabilities.DetectAsync(new(), source: webp.OutputPath);
                    var fallback = detected with { HasWindowsImageCodec = false };
                    using var webpService = new ConversionService(fallback);
                    var png = await webpService.ConvertAsync(webp.OutputPath, "png", new());
                    using var file = File.OpenRead(png.OutputPath);
                    var frame = new PngBitmapDecoder(file, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
                    Equal(64, frame.PixelWidth); Equal(32, frame.PixelHeight);
                    if (detected.HasWindowsImageCodec)
                    {
                        using var windows = new ConversionService(detected with { Ffmpeg = null });
                        var jpeg = await windows.ConvertAsync(webp.OutputPath, "jpg", new());
                        True(jpeg.OutputBytes > 0);
                    }
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
            await TestAsync("LibreOffice spreadsheet exports XLSX, ODS and PDF", async () =>
            {
                var table = Put("office-table.csv", "item,quantity\nalpha,2\n");
                var xlsx = await documents.ConvertAsync(table, "xlsx", new());
                using (var archive = ZipFile.OpenRead(xlsx.OutputPath)) True(archive.GetEntry("xl/workbook.xml") is not null);
                var ods = await documents.ConvertAsync(xlsx.OutputPath, "ods", new());
                using (var archive = ZipFile.OpenRead(ods.OutputPath)) True(archive.GetEntry("content.xml") is not null);
                var pdf = await documents.ConvertAsync(xlsx.OutputPath, "pdf", new());
                True(File.ReadAllText(pdf.OutputPath).StartsWith("%PDF-"));
            });
            await TestAsync("LibreOffice presentation exports PPTX and PDF", async () =>
            {
                var presentation = MakeOdp();
                var pptx = await documents.ConvertAsync(presentation, "pptx", new());
                using (var archive = ZipFile.OpenRead(pptx.OutputPath)) True(archive.GetEntry("ppt/presentation.xml") is not null);
                var pdf = await documents.ConvertAsync(pptx.OutputPath, "pdf", new());
                True(File.ReadAllText(pdf.OutputPath).StartsWith("%PDF-"));
            });
        }
        else if (args.Contains("--require-office")) { failures++; Console.WriteLine("FAIL LibreOffice is required but missing"); }
        else Console.WriteLine("SKIP optional LibreOffice integration (not installed)");
    }

    private static ProcessStartInfo SelfStart(params string[] arguments)
    {
        var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, CreateNoWindow = true };
        if (Path.GetFileNameWithoutExtension(start.FileName).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(Program).Assembly.Location);
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        return start;
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
    private static string MakeImage(string name = "sample.png", int width = 32, int height = 24)
    {
        var path = Path.Combine(Root, name);
        var data = new byte[width * height * 4]; for (var i = 0; i < data.Length; i += 4) { data[i] = 240; data[i + 1] = 180; data[i + 2] = 80; data[i + 3] = 255; }
        var source = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, data, width * 4);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path); encoder.Save(stream); return path;
    }
    private static string MakeOdp()
    {
        var path = Path.Combine(Root, "slides.odp");
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        Add("mimetype", "application/vnd.oasis.opendocument.presentation");
        Add("META-INF/manifest.xml", "<manifest:manifest xmlns:manifest=\"urn:oasis:names:tc:opendocument:xmlns:manifest:1.0\" manifest:version=\"1.2\"><manifest:file-entry manifest:full-path=\"/\" manifest:media-type=\"application/vnd.oasis.opendocument.presentation\"/><manifest:file-entry manifest:full-path=\"content.xml\" manifest:media-type=\"text/xml\"/></manifest:manifest>");
        Add("content.xml", "<office:document-content xmlns:office=\"urn:oasis:names:tc:opendocument:xmlns:office:1.0\" xmlns:draw=\"urn:oasis:names:tc:opendocument:xmlns:drawing:1.0\" xmlns:text=\"urn:oasis:names:tc:opendocument:xmlns:text:1.0\" xmlns:svg=\"urn:oasis:names:tc:opendocument:xmlns:svg-compatible:1.0\" xmlns:presentation=\"urn:oasis:names:tc:opendocument:xmlns:presentation:1.0\" office:version=\"1.2\"><office:body><office:presentation><draw:page draw:name=\"page1\"><draw:frame svg:x=\"2cm\" svg:y=\"2cm\" svg:width=\"20cm\" svg:height=\"5cm\" presentation:class=\"title\"><draw:text-box><text:p>Konvert integration fixture</text:p></draw:text-box></draw:frame></draw:page></office:presentation></office:body></office:document-content>");
        return path;
        void Add(string name, string content) { using var writer = new StreamWriter(archive.CreateEntry(name, CompressionLevel.NoCompression).Open(), new UTF8Encoding(false)); writer.Write(content); }
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
