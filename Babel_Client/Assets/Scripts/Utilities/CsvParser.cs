using System.Collections.Generic;
using System.Globalization;

public static class CsvParser
{
    public static (Dictionary<string, int> header, List<string[]> rows) Parse(string csvText)
    {
        var header = new Dictionary<string, int>();
        var rows = new List<string[]>();

        if (string.IsNullOrEmpty(csvText))
            return (header, rows);

        var lines = csvText.Split('\n');
        bool headerParsed = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Replace("\r", "").Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            var fields = line.Split(',');
            for (int j = 0; j < fields.Length; j++)
                fields[j] = fields[j].Trim();

            if (!headerParsed)
            {
                for (int j = 0; j < fields.Length; j++)
                    header[fields[j].ToLowerInvariant()] = j;
                headerParsed = true;
            }
            else
            {
                rows.Add(fields);
            }
        }

        return (header, rows);
    }

    public static float GetFloat(string[] row, int columnIndex, float defaultValue = 0f)
    {
        if (row == null || columnIndex < 0 || columnIndex >= row.Length)
            return defaultValue;

        if (float.TryParse(row[columnIndex], NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
            return result;

        return defaultValue;
    }

    public static int GetInt(string[] row, int columnIndex, int defaultValue = 0)
    {
        if (row == null || columnIndex < 0 || columnIndex >= row.Length)
            return defaultValue;

        if (int.TryParse(row[columnIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out int result))
            return result;

        return defaultValue;
    }

    public static string GetString(string[] row, int columnIndex, string defaultValue = "")
    {
        if (row == null || columnIndex < 0 || columnIndex >= row.Length)
            return defaultValue;

        return row[columnIndex];
    }
}
