using System.Data;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;

namespace QueryPlus.Core.Tests.Fakes;

internal static class TestData
{
    public static DistributionList List(params Target[] targets) => new()
    {
        Name = "test",
        Auth = AuthMode.Windows,
        Targets = targets.ToList()
    };

    public static Target Target(string server, string database) => new(server, database);

    public static DataTable Table(string[] columns, params object?[][] rows)
    {
        var table = new DataTable();
        foreach (var c in columns)
            table.Columns.Add(c, typeof(string));
        foreach (var r in rows)
            table.Rows.Add(r.Select(v => v ?? DBNull.Value).ToArray());
        return table;
    }

    public static BatchExecutionResult Result(params DataTable[] tables) =>
        new() { Tables = tables };

    public static BatchExecutionResult ResultWithMessages(IEnumerable<string> messages, params DataTable[] tables) =>
        new() { Tables = tables, InfoMessages = messages.ToList() };
}
