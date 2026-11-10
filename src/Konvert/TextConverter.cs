using System.IO.Compression;
using System.Net;
using System.Text;
using System.Xml;

namespace Konvert;

internal static class TextConverter
{
    public const long MaxInputBytes = 16 * 1024 * 1024;

    public static void Convert(string input, string output, string source, string target, CancellationToken token)
    {
        if (new FileInfo(input).Length > MaxInputBytes) throw new InvalidDataException("Text and table inputs are limited to 16 MiB in this alpha.");
        if (source is "docx" or "odt")
        {
            ExtractDocumentText(input, output, source, token);
            return;
        }
        // Strict UTF-8 avoids silently replacing invalid bytes. BOM-marked UTF-16/32 is detected.
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true);
        using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
        if (target == "html")
            writer.Write("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Converted text</title><style>body{max-width:80ch;margin:4rem auto;padding:0 2rem;font:16px/1.7 system-ui}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style><pre>");
        var buffer = new char[8193];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length - 1)) > 0)
        {
            token.ThrowIfCancellationRequested();
            // Keep a surrogate pair together before independently HTML-encoding each chunk.
            if (char.IsHighSurrogate(buffer[count - 1]) && reader.Peek() >= 0)
                buffer[count++] = (char)reader.Read();
            if (target == "html") writer.Write(WebUtility.HtmlEncode(new string(buffer, 0, count)));
            else writer.Write(buffer, 0, count);
        }
        if (target == "html") writer.Write("</pre></html>");
    }

    private static void ExtractDocumentText(string input, string output, string source, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(input);
        var entry = archive.GetEntry(source == "docx" ? "word/document.xml" : "content.xml")
            ?? throw new InvalidDataException($"This file is not a valid {source.ToUpperInvariant()} document.");
        if (entry.Length > MaxInputBytes) throw new InvalidDataException("Expanded document text exceeds the 16 MiB limit.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxInputBytes,
            IgnoreComments = true
        });
        using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
        if (source == "docx") ReadWordParagraphs(reader, writer, token);
        else ReadOpenDocumentParagraphs(reader, writer, token);
    }

    private static void ReadWordParagraphs(XmlReader reader, TextWriter writer, CancellationToken token)
    {
        const string word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        var insideText = false;
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            if (reader.NamespaceURI == word)
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.LocalName == "t") insideText = !reader.IsEmptyElement;
                    else if (reader.LocalName == "tab") writer.Write('\t');
                    else if (reader.LocalName is "br" or "cr") writer.WriteLine();
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.LocalName == "t") insideText = false;
                    else if (reader.LocalName == "p") writer.WriteLine();
                }
            }
            else if (insideText && reader.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace) writer.Write(reader.Value);
        }
    }

    private static void ReadOpenDocumentParagraphs(XmlReader reader, TextWriter writer, CancellationToken token)
    {
        const string office = "urn:oasis:names:tc:opendocument:xmlns:office:1.0";
        const string text = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
        if (!reader.ReadToFollowing("text", office))
            throw new InvalidDataException("The ODT document has no text body.");

        using var body = reader.ReadSubtree();
        var paragraph = -1;
        var written = 0L;
        while (body.Read())
        {
            token.ThrowIfCancellationRequested();
            if (body.NodeType == XmlNodeType.Element)
            {
                if ((body.NamespaceURI == office && body.LocalName == "annotation") ||
                    (body.NamespaceURI == text && body.LocalName == "tracked-changes"))
                {
                    using var ignored = body.ReadSubtree();
                    continue;
                }
                if (body.NamespaceURI != text) continue;
                if (body.LocalName is "p" or "h" && paragraph < 0)
                {
                    if (body.IsEmptyElement) writer.WriteLine();
                    else paragraph = body.Depth;
                }
                if (paragraph < 0) continue;
                switch (body.LocalName)
                {
                    case "tab": writer.Write('\t'); break;
                    case "line-break": writer.WriteLine(); break;
                    case "s":
                        var repeat = body.GetAttribute("c", text);
                        if (repeat is null) { WriteText(" "); break; }
                        if (!int.TryParse(repeat, out var count) || count is < 1 or > 8192)
                            throw new InvalidDataException("ODT whitespace exceeds the supported limit.");
                        WriteText(new string(' ', count));
                        break;
                }
            }
            else if (body.NodeType == XmlNodeType.EndElement && body.Depth == paragraph)
            {
                writer.WriteLine();
                paragraph = -1;
            }
            else if (paragraph >= 0 && body.NodeType is XmlNodeType.Text or XmlNodeType.SignificantWhitespace or XmlNodeType.Whitespace)
                WriteText(body.Value);
        }

        void WriteText(string value)
        {
            written += value.Length;
            if (written > MaxInputBytes) throw new InvalidDataException("Expanded ODT text exceeds the 16 MiB limit.");
            writer.Write(value);
        }
    }
}
