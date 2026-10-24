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
        if (source is "csv" or "tsv" && target is "json" or "csv" or "tsv" || source == "json" && target == "csv")
        { TableConverter.Convert(input, output, source, target); return; }
        if (source == "docx") { ExtractDocx(input, output, token); return; }
        // Strict UTF-8 avoids silently replacing invalid bytes. BOM-marked UTF-16/32 is detected.
        using var reader = new StreamReader(input, new UTF8Encoding(false, true), true);
        using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
        if (target == "html")
            writer.Write("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Converted text</title><style>body{max-width:80ch;margin:4rem auto;padding:0 2rem;font:16px/1.7 system-ui}pre{white-space:pre-wrap;overflow-wrap:anywhere}</style><pre>");
        var buffer = new char[8192];
        int count;
        while ((count = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            token.ThrowIfCancellationRequested();
            if (target == "html") writer.Write(WebUtility.HtmlEncode(new string(buffer, 0, count)));
            else writer.Write(buffer, 0, count);
        }
        if (target == "html") writer.Write("</pre></html>");
    }

    private static void ExtractDocx(string input, string output, CancellationToken token)
    {
        using var archive = ZipFile.OpenRead(input);
        var entry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("This file is not a valid DOCX document.");
        if (entry.Length > MaxInputBytes) throw new InvalidDataException("Expanded DOCX text exceeds the 16 MiB limit.");
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxInputBytes,
            IgnoreComments = true
        });
        using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
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
}
