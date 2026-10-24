using System.Diagnostics;
using System.Text;

namespace Konvert;

public sealed class ConversionService(EngineCapabilities capabilities) : IDisposable
{
    // One heavy conversion per process prevents parallel image decodes from multiplying peak RAM.
    private readonly SemaphoreSlim gate = new(1, 1);

    public async Task<ConversionResult> ConvertAsync(string input, string extension, ConversionSettings settings, CancellationToken cancellationToken = default)
    {
        input = Path.GetFullPath(input);
        settings = settings.Validate();
        extension = extension.TrimStart('.').ToLowerInvariant();
        if (!File.Exists(input)) throw new FileNotFoundException("The source file no longer exists.", input);
        var target = new FormatRegistry(capabilities).GetTargets(input).FirstOrDefault(t => t.Extension == extension)
            ?? throw new NotSupportedException("This conversion is not available. Check the format and optional engines in Settings.");
        await gate.WaitAsync(cancellationToken);
        var timer = Stopwatch.StartNew();
        string? staging = null;
        try
        {
            var directory = settings.OutputDirectory ?? Path.GetDirectoryName(input)!;
            if (!Directory.Exists(directory)) throw new DirectoryNotFoundException("The output folder does not exist. Choose another folder in Settings.");
            staging = Path.Combine(directory, $".konvert-{Guid.NewGuid():N}.{extension}");
            if (target.Engine == "LibreOffice") await ConvertOfficeAsync(input, staging, extension, cancellationToken);
            else if (target.Engine == "FFmpeg") await ConvertMediaAsync(input, staging, extension, cancellationToken);
            else await RunStaAsync(() =>
            {
                if (FormatRegistry.Images.Contains(Path.GetExtension(input).TrimStart('.').ToLowerInvariant()))
                    ImageConverter.Convert(input, staging, extension, settings, cancellationToken);
                else TextConverter.Convert(input, staging, Path.GetExtension(input).TrimStart('.').ToLowerInvariant(), extension, cancellationToken);
            }, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(staging) || new FileInfo(staging).Length == 0 && extension != "txt")
                throw new IOException("The engine did not produce a valid output file.");
            var output = CommitWithoutOverwrite(staging, directory, Path.GetFileNameWithoutExtension(input), extension);
            return new(output, timer.Elapsed, new FileInfo(output).Length);
        }
        finally
        {
            if (staging is not null) { try { File.Delete(staging); } catch (IOException) { } }
            gate.Release();
        }
    }

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
            await ExternalProcess.RunAsync(capabilities.LibreOffice!,
                [$"-env:UserInstallation={new Uri(profile + Path.DirectorySeparatorChar).AbsoluteUri}", "--headless", "--nologo", "--nodefault", "--norestore", "--convert-to", extension, "--outdir", workspace, input],
                TimeSpan.FromMinutes(3), token);
            var result = Path.Combine(workspace, Path.ChangeExtension(Path.GetFileName(input), extension));
            if (!File.Exists(result)) throw new IOException("LibreOffice could not export this file. It may be corrupt, encrypted, or unsupported.");
            File.Copy(result, output, false);
        }
        finally { try { Directory.Delete(workspace, true); } catch (IOException) { } }
    }

    private async Task ConvertMediaAsync(string input, string output, string extension, CancellationToken token)
    {
        var arguments = new List<string> { "-hide_banner", "-nostdin", "-loglevel", "error", "-n", "-protocol_whitelist", "file,pipe", "-threads", "2", "-filter_threads", "2", "-i", input, "-map_metadata", "-1" };
        arguments.AddRange(extension switch
        {
            "mp3" => ["-map", "0:a:0", "-vn", "-c:a", "libmp3lame", "-q:a", "2"],
            "wav" => ["-map", "0:a:0", "-vn", "-c:a", "pcm_s16le"],
            "flac" => ["-map", "0:a:0", "-vn", "-c:a", "flac"],
            "ogg" => ["-map", "0:a:0", "-vn", "-c:a", "libvorbis", "-q:a", "5"],
            "opus" => ["-map", "0:a:0", "-vn", "-c:a", "libopus", "-b:a", "128k"],
            "m4a" => ["-map", "0:a:0", "-vn", "-c:a", "aac", "-b:a", "192k"],
            "gif" => ["-map", "0:v:0", "-an", "-vf", "fps=12,scale=640:-1:flags=lanczos", "-c:v", "gif"],
            "webm" => ["-map", "0:v:0", "-map", "0:a:0?", "-c:v", "libvpx-vp9", "-crf", "32", "-b:v", "0", "-c:a", "libopus"],
            _ => ["-map", "0:v:0", "-map", "0:a:0?", "-c:v", "libx264", "-preset", "fast", "-crf", "23", "-pix_fmt", "yuv420p", "-c:a", "aac"]
        });
        arguments.AddRange(["-threads", "2", output]);
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
