using System.IO.Compression;
using System.Text;
using System.Xml;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Exceptions;

namespace Konvert;

internal static class PdfConverter
{
    internal const int MaxTextPages = 500;
    private const long MaxPdfBytes = 128L * 1024 * 1024;
    private const string Word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    internal static void CheckSize(string input)
    {
        if (new FileInfo(input).Length > MaxPdfBytes)
            throw new InvalidDataException("PDF inputs are limited to 128 MiB.");
    }

    internal static void Extract(string input, string output, string target, CancellationToken token)
    {
        CheckSize(input);
        using var document = Open(input);
        if (document.NumberOfPages > MaxTextPages)
            throw new InvalidDataException("PDF text export is limited to 500 pages.");

        var pages = ReadText(document, token);
        if (target == "docx") WriteWord(output, pages);
        else
        {
            using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
            foreach (var text in pages)
            {
                writer.WriteLine(text);
                writer.WriteLine();
            }
        }
    }

    private static PdfDocument Open(string input)
    {
        try { return PdfDocument.Open(input); }
        catch (PdfDocumentEncryptedException ex)
        {
            throw new InvalidDataException("Open and unlock this PDF before converting it.", ex);
        }
        catch (PdfDocumentFormatException ex)
        {
            throw new InvalidDataException("Konvert could not read this PDF. Check that the file opens and try again.", ex);
        }
    }

    private static IEnumerable<string> ReadText(PdfDocument document, CancellationToken token)
    {
        var total = 0L;
        var hasText = false;
        for (var number = 1; number <= document.NumberOfPages; number++)
        {
            token.ThrowIfCancellationRequested();
            var text = ContentOrderTextExtractor.GetText(document.GetPage(number), addDoubleNewline: true);
            total += text.Length;
            if (total > TextConverter.MaxInputBytes)
                throw new InvalidDataException("Expanded PDF text exceeds the 16 MiB text limit.");
            hasText |= !string.IsNullOrWhiteSpace(text);
            yield return text;
        }
        if (!hasText)
            throw new InvalidDataException("This PDF has no selectable text. Use PNG, JPG, or a page archive for scanned pages.");
    }

    private static void WriteWord(string output, IEnumerable<string> pages)
    {
        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        Add("[Content_Types].xml", """
            <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
              <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
              <Default Extension="xml" ContentType="application/xml"/>
              <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
            </Types>
            """);
        Add("_rels/.rels", """
            <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
              <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
            </Relationships>
            """);
        using var stream = archive.CreateEntry("word/document.xml").Open();
        using var xml = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false) });
        xml.WriteStartElement("w", "document", Word);
        xml.WriteStartElement("w", "body", Word);
        var first = true;
        foreach (var page in pages)
        {
            if (!first)
            {
                xml.WriteStartElement("w", "p", Word);
                xml.WriteStartElement("w", "r", Word);
                xml.WriteStartElement("w", "br", Word);
                xml.WriteAttributeString("w", "type", Word, "page");
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
            first = false;
            using var lines = new StringReader(page);
            while (lines.ReadLine() is { } line)
            {
                xml.WriteStartElement("w", "p", Word);
                xml.WriteStartElement("w", "r", Word);
                xml.WriteStartElement("w", "t", Word);
                xml.WriteAttributeString("xml", "space", null, "preserve");
                xml.WriteString(XmlText(line));
                xml.WriteEndElement();
                xml.WriteEndElement();
                xml.WriteEndElement();
            }
        }
        xml.WriteEndElement();
        xml.WriteEndElement();

        void Add(string name, string content)
        {
            using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private static string XmlText(string line)
    {
        try { return XmlConvert.VerifyXmlChars(line); }
        catch (XmlException)
        {
            var clean = new StringBuilder(line.Length);
            foreach (var rune in line.EnumerateRunes())
                clean.Append(rune.Value is 9 or >= 32 and <= 0xd7ff or >= 0xe000 and <= 0xfffd or >= 0x10000 ? rune.ToString() : "�");
            return clean.ToString();
        }
    }
}
