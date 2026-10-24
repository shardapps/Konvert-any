using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Konvert;

internal static class TableConverter
{
    private const int MaxRows = 100_000;
    private const int MaxColumns = 1_000;

    public static List<string[]> Parse(string text, char delimiter)
    {
        var rows = new List<string[]>();
        var row = new List<string>();
        var field = new StringBuilder();
        var quoted = false;
        var closedQuote = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closedQuote = true; }
                }
                else field.Append(c);
                continue;
            }
            if (c == '"' && field.Length == 0 && !closedQuote) { quoted = true; continue; }
            if (c == delimiter) { AddField(); continue; }
            if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                AddRow();
                continue;
            }
            if (closedQuote || c == '"') throw new InvalidDataException("Malformed table: unexpected text around a quote.");
            field.Append(c);
        }
        if (quoted) throw new InvalidDataException("Malformed table: unclosed quoted field.");
        if (field.Length > 0 || row.Count > 0 || closedQuote) AddRow();
        return rows;

        void AddField()
        {
            if (row.Count >= MaxColumns) throw new InvalidDataException("Tables are limited to 1,000 columns.");
            row.Add(field.ToString()); field.Clear(); closedQuote = false;
        }
        void AddRow()
        {
            if (rows.Count >= MaxRows) throw new InvalidDataException("Tables are limited to 100,000 rows.");
            AddField(); rows.Add(row.ToArray()); row.Clear();
        }
    }

    public static void Convert(string input, string output, string source, string target)
    {
        if (source == "json")
        {
            using var document = JsonDocument.Parse(File.ReadAllText(input));
            if (document.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidDataException("CSV export needs a JSON array of flat objects.");
            if (document.RootElement.GetArrayLength() > MaxRows) throw new InvalidDataException("Tables are limited to 100,000 rows.");
            var columns = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Every JSON row must be an object.");
                var keys = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in item.EnumerateObject())
                {
                    if (!keys.Add(property.Name)) throw new InvalidDataException("JSON rows cannot contain duplicate keys.");
                    if (property.Value.ValueKind is JsonValueKind.Object or JsonValueKind.Array) throw new InvalidDataException("Nested JSON cannot be represented as a flat CSV table.");
                    if (seen.Add(property.Name)) columns.Add(property.Name);
                    if (columns.Count > MaxColumns) throw new InvalidDataException("Tables are limited to 1,000 columns.");
                }
            }
            using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
            WriteRow(writer, columns, ',');
            foreach (var item in document.RootElement.EnumerateArray())
                WriteRow(writer, columns.Select(column => item.TryGetProperty(column, out var value) ? Scalar(value) : ""), ',');
            return;
        }
        var rows = Parse(File.ReadAllText(input), source == "tsv" ? '\t' : ',');
        if (target == "json")
        {
            if (rows.Count == 0) { File.WriteAllText(output, "[]"); return; }
            var headers = rows[0];
            if (headers.Any(string.IsNullOrWhiteSpace) || headers.Distinct(StringComparer.Ordinal).Count() != headers.Length)
                throw new InvalidDataException("JSON export needs unique, non-empty column headings.");
            using var stream = File.Create(output);
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartArray();
            foreach (var row in rows.Skip(1))
            {
                if (row.Length != headers.Length) throw new InvalidDataException("Each table row must have the same number of fields as the header.");
                writer.WriteStartObject();
                for (var i = 0; i < headers.Length; i++) writer.WriteString(headers[i], row[i]);
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        else
        {
            using var writer = new StreamWriter(output, false, new UTF8Encoding(false));
            foreach (var row in rows) WriteRow(writer, row, target == "tsv" ? '\t' : ',');
        }
    }

    private static string Scalar(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => value.GetString() ?? "",
        JsonValueKind.Null => "",
        _ => value.GetRawText()
    };

    private static void WriteRow(TextWriter writer, IEnumerable<string> fields, char delimiter)
    {
        writer.WriteLine(string.Join(delimiter, fields.Select(field =>
            field.IndexOfAny([delimiter, '"', '\r', '\n']) >= 0 ? $"\"{field.Replace("\"", "\"\"")}\"" : field)));
    }
}
