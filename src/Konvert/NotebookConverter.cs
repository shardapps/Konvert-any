using System.Net;
using System.Text;
using System.Text.Json;

namespace Konvert;

internal static class NotebookConverter
{
    internal static void Convert(string input, string output, string target, CancellationToken token)
    {
        if (new FileInfo(input).Length > TextConverter.MaxInputBytes)
            throw new InvalidDataException("Notebook inputs are limited to 16 MiB.");
        using var stream = File.OpenRead(input);
        using var notebook = JsonDocument.Parse(stream);
        if (notebook.RootElement.ValueKind != JsonValueKind.Object ||
            !notebook.RootElement.TryGetProperty("cells", out var cells) || cells.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("This file does not contain a Jupyter notebook cell list.");

        using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
        if (target == "html")
            writer.Write("""
                <!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width">
                <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src 'unsafe-inline'"><title>Notebook export</title>
                <style>body{max-width:78ch;margin:3rem auto;padding:0 1.5rem;font:16px/1.55 system-ui}section{margin:1.75rem 0}pre{padding:1rem;background:#eff4f8;border-radius:.75rem;white-space:pre-wrap;overflow-wrap:anywhere}h2{font-size:1.1rem}</style>
                <main><h1>Notebook export</h1>
                """);
        var index = 0;
        foreach (var cell in cells.EnumerateArray())
        {
            token.ThrowIfCancellationRequested();
            if (cell.ValueKind != JsonValueKind.Object ||
                !cell.TryGetProperty("cell_type", out var type) || type.ValueKind != JsonValueKind.String)
                throw new InvalidDataException("The notebook contains a cell without a type.");
            var kind = type.GetString() switch
            {
                "markdown" => "Markdown",
                "code" => "Code",
                "raw" => "Raw",
                _ => "Text"
            };
            index++;
            var text = cell.TryGetProperty("source", out var source) ? ReadText(source) : "";
            if (target == "html")
            {
                writer.Write($"<section><h2>{kind} cell {index}</h2><pre>{WebUtility.HtmlEncode(text)}</pre>");
            }
            else if (target == "md" && kind == "Markdown") writer.WriteLine(text);
            else
            {
                writer.WriteLine(target == "md" ? $"### {kind} cell {index}" : $"{kind} cell {index}");
                WritePlain(text);
            }

            if (cell.TryGetProperty("outputs", out var outputs) && outputs.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in outputs.EnumerateArray())
                {
                    token.ThrowIfCancellationRequested();
                    if (item.ValueKind != JsonValueKind.Object) continue;
                    var result = OutputText(item);
                    if (string.IsNullOrEmpty(result)) continue;
                    if (target == "html") writer.Write($"<h3>Output</h3><pre>{WebUtility.HtmlEncode(result)}</pre>");
                    else
                    {
                        writer.WriteLine(target == "md" ? "**Output**" : "Output");
                        WritePlain(result);
                    }
                }
            }
            if (target == "html") writer.WriteLine("</section>");
            else writer.WriteLine();
        }
        if (target == "html") writer.Write("</main></html>");

        void WritePlain(string value)
        {
            if (target != "md") { writer.WriteLine(value); return; }
            using var reader = new StringReader(value);
            while (reader.ReadLine() is { } line)
            {
                token.ThrowIfCancellationRequested();
                writer.WriteLine($"    {line}");
            }
            writer.WriteLine();
        }
    }

    private static string OutputText(JsonElement item)
    {
        if (item.TryGetProperty("text", out var text)) return ReadText(text);
        if (item.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Object &&
            data.TryGetProperty("text/plain", out text)) return ReadText(text);
        if (item.TryGetProperty("traceback", out text)) return ReadText(text);
        return "";
    }

    private static string ReadText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
        if (value.ValueKind == JsonValueKind.Array)
        {
            var text = new StringBuilder();
            foreach (var part in value.EnumerateArray())
            {
                if (part.ValueKind != JsonValueKind.String) throw new InvalidDataException("Notebook text must contain strings.");
                text.Append(part.GetString());
            }
            return text.ToString();
        }
        throw new InvalidDataException("Notebook text must be a string or list of strings.");
    }
}
