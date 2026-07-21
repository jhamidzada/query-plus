using System.Data;
using System.Globalization;
using QueryPlus.Core.Results;

namespace QueryPlus.Core.Engine;

/// <summary>Merges result sets into one flat table for the results grid.</summary>
public static class ResultMerger
{
    private const string ServerColumn = "Server";
    private const string DatabaseColumn = "Database";
    private const string ScriptColumn = "Script";

    private static readonly string[] Reserved = { ServerColumn, DatabaseColumn, ScriptColumn };

    /// <summary>
    /// Builds a single <see cref="DataTable"/> with leading <c>Server</c>, <c>Database</c>,
    /// <c>Script</c> columns followed by the union of all result columns encountered. Values
    /// are stored as strings; <see cref="DBNull"/> is preserved. A result column literally
    /// named Server/Database/Script is suffixed (e.g. <c>Server_1</c>) so it cannot clobber
    /// the prepended columns.
    /// </summary>
    public static DataTable BuildMergedTable(RunReport report)
    {
        var merged = CreateMergedTable();
        foreach (var target in report.Targets)
            AppendTarget(merged, target);
        return merged;
    }

    /// <summary>Creates an empty merged table with just the leading columns.</summary>
    public static DataTable CreateMergedTable()
    {
        var merged = new DataTable("Merged") { Locale = CultureInfo.InvariantCulture };
        merged.Columns.Add(ServerColumn, typeof(string));
        merged.Columns.Add(DatabaseColumn, typeof(string));
        merged.Columns.Add(ScriptColumn, typeof(string));
        return merged;
    }

    /// <summary>
    /// Appends one target's result sets to an existing merged table, adding any new data
    /// columns as they are first seen. Lets the UI grow the grid live as targets complete.
    /// <paramref name="maxRows"/> caps the merged table's row count (columns are still added)
    /// so the grid stays responsive with huge result sets; the full data is exported via CSV.
    /// </summary>
    public static void AppendTarget(DataTable merged, TargetResult target, int maxRows = int.MaxValue)
    {
        foreach (var rs in target.ResultSets)
        {
            foreach (DataColumn col in rs.Table.Columns)
            {
                var name = MergedColumnName(col.ColumnName);
                if (!merged.Columns.Contains(name))
                    merged.Columns.Add(name, typeof(string));
            }

            if (merged.Rows.Count >= maxRows)
                continue;

            foreach (DataRow source in rs.Table.Rows)
            {
                if (merged.Rows.Count >= maxRows)
                    break;

                var row = merged.NewRow();
                row[ServerColumn] = target.Server;
                row[DatabaseColumn] = target.Database;
                row[ScriptColumn] = rs.ScriptName;

                foreach (DataColumn col in rs.Table.Columns)
                {
                    var value = source[col];
                    row[MergedColumnName(col.ColumnName)] = value is DBNull
                        ? DBNull.Value
                        : Convert.ToString(value, CultureInfo.InvariantCulture);
                }

                merged.Rows.Add(row);
            }
        }
    }

    /// <summary>The merged column order: the leading columns then the first-seen union of result columns.</summary>
    public static IReadOnlyList<string> MergedColumnNames(RunReport report)
    {
        var columns = new List<string> { ServerColumn, DatabaseColumn, ScriptColumn };
        var seen = new HashSet<string>(columns, StringComparer.Ordinal);
        foreach (var target in report.Targets)
        foreach (var rs in target.ResultSets)
        foreach (DataColumn col in rs.Table.Columns)
        {
            var name = MergedColumnName(col.ColumnName);
            if (seen.Add(name))
                columns.Add(name);
        }
        return columns;
    }

    /// <summary>
    /// Streams each merged row as a string array aligned to <paramref name="columns"/>. A null
    /// cell means DBNull/empty. Lazy — safe for millions of rows without building a table.
    /// </summary>
    public static IEnumerable<string?[]> EnumerateMergedRows(RunReport report, IReadOnlyList<string> columns)
    {
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < columns.Count; i++)
            index[columns[i]] = i;

        foreach (var target in report.Targets)
        foreach (var rs in target.ResultSets)
        foreach (DataRow source in rs.Table.Rows)
        {
            var cells = new string?[columns.Count];
            cells[0] = target.Server;
            cells[1] = target.Database;
            cells[2] = rs.ScriptName;

            foreach (DataColumn col in rs.Table.Columns)
            {
                var value = source[col];
                cells[index[MergedColumnName(col.ColumnName)]] =
                    value is DBNull ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            yield return cells;
        }
    }

    /// <summary>
    /// Streams every result row to CSV (Server/Database/Script + the union of result columns),
    /// without building a giant in-memory table. Safe for millions of rows.
    /// </summary>
    public static void WriteCsv(RunReport report, TextWriter writer)
    {
        var columns = MergedColumnNames(report);
        writer.WriteLine(string.Join(",", columns.Select(CsvField)));
        foreach (var cells in EnumerateMergedRows(report, columns))
            writer.WriteLine(string.Join(",", cells.Select(CsvField)));
    }

    private static string CsvField(string? value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    /// <summary>
    /// Maps a source column name to its merged-table name. Reserved names get a single
    /// <c>_1</c> suffix so they never clobber the prepended columns; the mapping is
    /// deterministic and stable across calls so the same source name always unions to the
    /// same merged column.
    /// </summary>
    private static string MergedColumnName(string sourceName) =>
        Array.Exists(Reserved, r => string.Equals(r, sourceName, StringComparison.Ordinal))
            ? sourceName + "_1"
            : sourceName;
}
