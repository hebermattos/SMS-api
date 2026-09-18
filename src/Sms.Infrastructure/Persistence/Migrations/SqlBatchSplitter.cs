using System.Text;

namespace Sms.Infrastructure.Persistence.Migrations;

public static class SqlBatchSplitter
{
    public static IReadOnlyList<string> Split(string script)
    {
        ArgumentNullException.ThrowIfNull(script);
        var batches = new List<string>();
        var current = new StringBuilder();
        using var reader = new StringReader(script);

        while (reader.ReadLine() is { } line)
        {
            if (line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase))
            {
                AddBatch(batches, current);
                continue;
            }

            current.AppendLine(line);
        }

        AddBatch(batches, current);
        return batches;
    }

    private static void AddBatch(List<string> batches, StringBuilder current)
    {
        var batch = current.ToString().Trim();
        if (batch.Length > 0) batches.Add(batch);
        current.Clear();
    }
}
