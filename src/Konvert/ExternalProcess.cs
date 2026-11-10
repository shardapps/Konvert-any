using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Konvert;

internal static class ExternalProcess
{
    internal readonly record struct Output(string StandardOutput, string StandardError);

    public static async Task<Output> RunAsync(string executable, IEnumerable<string> arguments, TimeSpan timeout,
        CancellationToken cancellationToken, string? workingDirectory = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(timeout);
        var token = deadline.Token;
        // Launch can block on an offline or streamed executable. The caller's deadline also covers that wait.
        var work = Task.Run(() => RunCoreAsync(executable, arguments, workingDirectory, token), token);
        try { return await work.WaitAsync(token).ConfigureAwait(false); }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            _ = ObserveAsync(work);
            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException("The conversion engine exceeded its time limit.");
        }
    }

    private static async Task<Output> RunCoreAsync(string executable, IEnumerable<string> arguments,
        string? workingDirectory, CancellationToken token)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = workingDirectory ?? ""
        };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = new Process { StartInfo = start };
        token.ThrowIfCancellationRequested();
        if (!process.Start()) throw new IOException("The conversion engine could not start.");
        using var job = ProcessJob.TryAttach(process);
        using var stdout = process.StandardOutput;
        using var stderr = process.StandardError;
        var logs = Task.WhenAll(DrainAsync(stdout, token), DrainAsync(stderr, token));
        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            // Descendants can retain inherited pipe handles after the launcher exits.
            var captured = await logs.WaitAsync(token).ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw new IOException($"Engine exited with code {process.ExitCode}. {captured[1].Trim()}");
            return new(captured[0], captured[1]);
        }
        finally
        {
            if (token.IsCancellationRequested)
            {
                if (job is null)
                {
                    try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                    catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { }
                }
                else job.Dispose();
                _ = ObserveAsync(logs);
            }
        }
    }

    private static async Task ObserveAsync(Task task)
    {
        try { await task.ConfigureAwait(false); }
        catch (Exception) { /* The deadline outcome has already been returned to the caller. */ }
    }

    private static async Task<string> DrainAsync(StreamReader reader, CancellationToken token)
    {
        var retained = new StringBuilder();
        var buffer = new char[2048];
        int count;
        while ((count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false)) > 0)
            if (retained.Length < 64 * 1024) retained.Append(buffer, 0, Math.Min(count, 64 * 1024 - retained.Length));
        return retained.ToString();
    }
}
