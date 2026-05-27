namespace TotalCall.OplImporter;

// Minimal RFC4180-ish CSV reader sufficient for OpenPowerlifting/OpenIPF bulk CSVs.
// Streams a TextReader line-by-line yielding column dictionaries.
public static class CsvReader
{
    public static IEnumerable<IReadOnlyDictionary<string, string>> Read(TextReader reader)
    {
        var headerLine = reader.ReadLine();
        if (headerLine is null)
        {
            yield break;
        }

        var headers = ParseLine(headerLine);

        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            var fields = ParseLine(line);
            var row = new Dictionary<string, string>(headers.Length, StringComparer.Ordinal);
            for (var i = 0; i < headers.Length && i < fields.Length; i++)
            {
                row[headers[i]] = fields[i];
            }
            yield return row;
        }
    }

    private static string[] ParseLine(string line)
    {
        var result = new List<string>(48);
        var span = line.AsSpan();
        var sb = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (inQuotes)
            {
                if (c == '"')
                {
                    if (i + 1 < span.Length && span[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            else
            {
                switch (c)
                {
                    case '"':
                        inQuotes = true;
                        break;
                    case ',':
                        result.Add(sb.ToString());
                        sb.Clear();
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }
        }
        result.Add(sb.ToString());
        return result.ToArray();
    }
}
