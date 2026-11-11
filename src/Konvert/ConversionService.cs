using System.Diagnostics;
using System.Globalization;

namespace Konvert;

public sealed class ConversionService(EngineCapabilities capabilities) : IDisposable
{
    // One heavy conversion per process prevents parallel image decodes from multiplying peak RAM.
    private readonly SemaphoreSlim gate = new(1, 1);

    public Task<ConversionResult> ConvertAsync(string input, string extension, ConversionSettings settings, CancellationToken cancellationToken = default) =>
        Task.Run(() => ConvertCoreAsync(input, extension, settings, cancellationToken), cancellationToken);

    private async Task<ConversionResult> ConvertCoreAsync(string input, string extension, ConversionSettings settings, CancellationToken cancellationToken)
    {
        input = Path.GetFullPath(input);
        settings = settings.Validate();
        if (!File.Exists(input)) throw new FileNotFoundException("The source file no longer exists.", input);
        var route = new FormatRegistry(capabilities).Resolve(input, extension)
            ?? throw new NotSupportedException("This conversion is not available. Check the format and optional engines in Settings.");
        var source = FormatRegistry.SourceExtension(input);
        extension = route.Extension;
        await gate.WaitAsync(cancellationToken);
        var timer = Stopwatch.StartNew();
        string? staging = null;
        try
        {
            var directory = settings.OutputDirectory ?? Path.GetDirectoryName(input)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The output folder does not exist. Choose another folder in Settings.");
            staging = Path.Combine(directory, $".konvert-{Guid.NewGuid():N}.{extension}");
            switch (route.Backend)
            {
                case ConversionBackend.Office:
                    await ConvertOfficeAsync(input, staging, extension, cancellationToken);
                    break;
                case ConversionBackend.Media:
                    await ConvertMediaAsync(input, staging, route, settings, cancellationToken);
                    break;
                case ConversionBackend.Image:
                case ConversionBackend.WindowsImage:
                    await RunStaAsync(() => ImageConverter.Convert(input, staging, extension, settings, cancellationToken), cancellationToken);
                    break;
                case ConversionBackend.Table:
                    TableConverter.Convert(input, staging, source, extension, cancellationToken);
                    break;
                case ConversionBackend.Text:
                    TextConverter.Convert(input, staging, source, extension, cancellationToken);
                    break;
                case ConversionBackend.Notebook:
                    NotebookConverter.Convert(input, staging, extension, cancellationToken);
                    break;
                case ConversionBackend.PdfText:
                    PdfConverter.Extract(input, staging, extension, cancellationToken);
                    break;
                case ConversionBackend.PdfImage:
                    await ConvertPdfImagesAsync(input, staging, extension, settings, cancellationToken);
                    break;
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(staging) || new FileInfo(staging).Length == 0 && extension != "txt")
                throw new IOException("The engine did not produce a valid output file.");
            var output = CommitWithoutOverwrite(staging, directory, Path.GetFileNameWithoutExtension(input), extension);
            return new(output, timer.Elapsed, new FileInfo(output).Length);
        }
        finally
        {
            if (staging is not null)
            {
                try { File.Delete(staging); }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            }
            gate.Release();
        }
    }

    private async Task ConvertPdfImagesAsync(string input, string output, string target, ConversionSettings settings, CancellationToken token)
    {
        PdfConverter.CheckSize(input);
        if (target == "png")
        {
            await RenderPdfAsync(input, output, settings.PdfPage, settings, token);
            return;
        }

        var workspace = Directory.CreateTempSubdirectory("Konvert-pdf-");
        try
        {
            if (target == "zip")
            {
                await RenderPdfAsync(input, workspace.FullName, 0, settings, token);
                await System.IO.Compression.ZipFile.CreateFromDirectoryAsync(workspace.FullName, output,
                    System.IO.Compression.CompressionLevel.NoCompression, includeBaseDirectory: false, cancellationToken: token);
            }
            else
            {
                var png = Path.Combine(workspace.FullName, "page.png");
                await RenderPdfAsync(input, png, settings.PdfPage, settings, token);
                await RunStaAsync(() => ImageConverter.Convert(png, output, "jpg", settings with { MaxImageEdge = 0 }, token), token);
            }
        }
        finally
        {
            try { workspace.Delete(true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private Task<ExternalProcess.Output> RenderPdfAsync(string input, string output, int page, ConversionSettings settings, CancellationToken token) =>
        ExternalProcess.RunAsync(capabilities.PdfRenderer!, ["--render", input, output,
            page.ToString(CultureInfo.InvariantCulture), settings.Dpi.ToString(CultureInfo.InvariantCulture),
            settings.MaxImageEdge.ToString(CultureInfo.InvariantCulture)],
            page == 0 ? TimeSpan.FromMinutes(3) : TimeSpan.FromSeconds(60), token);

    private static string CommitWithoutOverwrite(string staging, string directory, string name, string extension)
    {
        for (var suffix = 0; suffix < 10_000; suffix++)
        {
            var output = Path.Combine(directory, $"{name}{(suffix == 0 ? "" : $" ({suffix})")}.{extension}");
            try { File.Move(staging, output, false); return output; }
            catch (IOException) when (File.Exists(output)) { }
        }
        throw new IOException("Too many files with the same name. Choose another output folder.");
    }

    private async Task ConvertOfficeAsync(string input, string output, string extension, CancellationToken token)
    {
        var workspace = Path.Combine(Path.GetTempPath(), $"Konvert-{Guid.NewGuid():N}");
        Directory.CreateDirectory(workspace);
        try
        {
            var profile = Path.Combine(workspace, "profile");
            Directory.CreateDirectory(Path.Combine(profile, "user"));
            File.WriteAllText(Path.Combine(profile, "user", "registrymodifications.xcu"),
                "<?xml version=\"1.0\"?><oor:items xmlns:oor=\"http://openoffice.org/2001/registry\"><item oor:path=\"/org.openoffice.Office.Common/Security/Scripting\"><prop oor:name=\"MacroSecurityLevel\" oor:op=\"fuse\"><value>3</value></prop></item></oor:items>");
            ExternalProcess.Output diagnostics;
            try
            {
                diagnostics = await ExternalProcess.RunAsync(capabilities.LibreOffice!,
                    [$"-env:UserInstallation={new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri}", "--headless", "--nologo", "--nodefault", "--norestore", "--convert-to", extension, "--outdir", workspace, input],
                    TimeSpan.FromSeconds(60), token, workingDirectory: workspace);
            }
            catch (TimeoutException ex)
            {
                throw new TimeoutException("LibreOffice did not finish within one minute. Check the executable in Preferences → Converters. Use a local LibreOffice installation for files on a streamed drive.", ex);
            }
            var result = Path.Combine(workspace, Path.ChangeExtension(Path.GetFileName(input), extension));
            if (!File.Exists(result))
            {
                var error = diagnostics.StandardError.Trim();
                throw new IOException(string.IsNullOrEmpty(error)
                    ? "LibreOffice could not export this file. Check whether it is password protected or damaged."
                    : $"LibreOffice could not export this file. {error}");
            }
            await using var source = new FileStream(result, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
            await using var destination = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
            await source.CopyToAsync(destination, token);
        }
        finally
        {
            try { Directory.Delete(workspace, true); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task ConvertMediaAsync(string input, string output, ConversionRoute route, ConversionSettings settings, CancellationToken token)
    {
        var threads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4).ToString(CultureInfo.InvariantCulture);
        string[] audio = route.AudioEncoder is { } encoder && capabilities.HasAudio != false ? ["-c:a", encoder] : [];
        string[] resize = [];
        if (route.StillImage && settings.MaxImageEdge > 0)
        {
            var edge = settings.MaxImageEdge.ToString(CultureInfo.InvariantCulture);
            resize = ["-vf", $"scale=w='min(iw,{edge})':h='min(ih,{edge})':force_original_aspect_ratio=decrease:flags=lanczos"];
        }
        string[] arguments =
        [
            "-hide_banner", "-nostdin", "-loglevel", "error", "-n", "-protocol_whitelist", "file,pipe",
            "-threads", threads, "-filter_threads", threads, "-i", input, "-map_metadata", "-1",
            .. route.Arguments!, .. audio, .. resize, "-threads", threads, "-f", route.Muxer!, output
        ];
        await ExternalProcess.RunAsync(capabilities.Ffmpeg!, arguments, TimeSpan.FromMinutes(30), token);
    }

    private static Task RunStaAsync(Action action, CancellationToken token)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            try { token.ThrowIfCancellationRequested(); action(); completion.SetResult(); }
            catch (OperationCanceledException) { completion.SetCanceled(token); }
            catch (Exception ex) { completion.SetException(ex); }
        })
        { IsBackground = true, Name = "Konvert conversion worker" };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return completion.Task;
    }

    public void Dispose() => gate.Dispose();
}
