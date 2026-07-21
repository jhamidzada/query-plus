using System.Data;
using FluentAssertions;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Results;
using QueryPlus.Core.Tests.Fakes;

namespace QueryPlus.Core.Tests;

public class ResultMergerTests
{
    private static RunReport Report(params TargetResult[] targets) => new(targets);

    private static TargetResult Target(string server, string db, params ResultSet[] sets)
    {
        var tr = new TargetResult(server, db);
        foreach (var s in sets)
            tr.ResultSets.Add(s);
        return tr;
    }

    [Fact]
    public void Prepends_Server_Database_Script_columns()
    {
        var report = Report(Target("S1", "DB1",
            new ResultSet("a.sql", TestData.Table(new[] { "Value" }, new object?[] { "x" }))));

        var merged = ResultMerger.BuildMergedTable(report);

        merged.Columns.Cast<DataColumn>().Take(3).Select(c => c.ColumnName)
            .Should().Equal("Server", "Database", "Script");
        merged.Rows[0]["Server"].Should().Be("S1");
        merged.Rows[0]["Database"].Should().Be("DB1");
        merged.Rows[0]["Script"].Should().Be("a.sql");
        merged.Rows[0]["Value"].Should().Be("x");
    }

    [Fact]
    public void Unions_heterogeneous_schemas()
    {
        var report = Report(
            Target("S1", "DB1", new ResultSet("a.sql",
                TestData.Table(new[] { "A", "B" }, new object?[] { "1", "2" }))),
            Target("S2", "DB2", new ResultSet("a.sql",
                TestData.Table(new[] { "B", "C" }, new object?[] { "3", "4" }))));

        var merged = ResultMerger.BuildMergedTable(report);

        merged.Columns.Cast<DataColumn>().Select(c => c.ColumnName)
            .Should().Equal("Server", "Database", "Script", "A", "B", "C");

        merged.Rows.Should().HaveCount(2);

        // First row has A,B but no C.
        merged.Rows[0]["A"].Should().Be("1");
        merged.Rows[0]["B"].Should().Be("2");
        merged.Rows[0]["C"].Should().Be(DBNull.Value);

        // Second row has B,C but no A.
        merged.Rows[1]["A"].Should().Be(DBNull.Value);
        merged.Rows[1]["B"].Should().Be("3");
        merged.Rows[1]["C"].Should().Be("4");
    }

    [Fact]
    public void Preserves_DBNull()
    {
        var report = Report(Target("S1", "DB1", new ResultSet("a.sql",
            TestData.Table(new[] { "Value" }, new object?[] { null }))));

        var merged = ResultMerger.BuildMergedTable(report);

        merged.Rows[0]["Value"].Should().Be(DBNull.Value);
    }

    [Fact]
    public void Stores_values_as_strings()
    {
        var typed = new DataTable();
        typed.Columns.Add("Num", typeof(int));
        typed.Rows.Add(42);
        var report = Report(Target("S1", "DB1", new ResultSet("a.sql", typed)));

        var merged = ResultMerger.BuildMergedTable(report);

        merged.Columns["Num"]!.DataType.Should().Be(typeof(string));
        merged.Rows[0]["Num"].Should().Be("42");
    }

    [Fact]
    public void Suffixes_columns_that_collide_with_prepended_names()
    {
        var report = Report(Target("S1", "DB1", new ResultSet("a.sql",
            TestData.Table(new[] { "Server", "Database", "Script", "Other" },
                new object?[] { "rsServer", "rsDb", "rsScript", "ok" }))));

        var merged = ResultMerger.BuildMergedTable(report);

        merged.Columns.Cast<DataColumn>().Select(c => c.ColumnName)
            .Should().Equal("Server", "Database", "Script", "Server_1", "Database_1", "Script_1", "Other");

        var row = merged.Rows[0];
        // Prepended columns keep the run metadata...
        row["Server"].Should().Be("S1");
        row["Database"].Should().Be("DB1");
        row["Script"].Should().Be("a.sql");
        // ...and the result set's own columns land in the suffixed ones.
        row["Server_1"].Should().Be("rsServer");
        row["Database_1"].Should().Be("rsDb");
        row["Script_1"].Should().Be("rsScript");
        row["Other"].Should().Be("ok");
    }

    [Fact]
    public void AppendTarget_caps_rows_at_maxRows_but_keeps_columns()
    {
        var merged = ResultMerger.CreateMergedTable();
        var target = Target("S", "DB", new ResultSet("a.sql",
            TestData.Table(new[] { "V" }, new object?[] { "1" }, new object?[] { "2" }, new object?[] { "3" })));

        ResultMerger.AppendTarget(merged, target, maxRows: 2);

        merged.Rows.Count.Should().Be(2);
        merged.Columns.Contains("V").Should().BeTrue();
    }

    [Fact]
    public void WriteCsv_emits_header_and_rows_with_escaping_and_dbnull()
    {
        var report = Report(Target("S1", "DB1", new ResultSet("a.sql",
            TestData.Table(new[] { "Name" }, new object?[] { "has \"quote\"" }, new object?[] { null }))));

        var sw = new System.IO.StringWriter();
        ResultMerger.WriteCsv(report, sw);
        var lines = sw.ToString().Replace("\r\n", "\n").TrimEnd('\n').Split('\n');

        lines[0].Should().Be("\"Server\",\"Database\",\"Script\",\"Name\"");
        lines[1].Should().Be("\"S1\",\"DB1\",\"a.sql\",\"has \"\"quote\"\"\"");
        lines[2].Should().Be("\"S1\",\"DB1\",\"a.sql\",\"\"");
    }

    [Fact]
    public void Empty_report_yields_just_the_prepended_columns()
    {
        var merged = ResultMerger.BuildMergedTable(Report());

        merged.Columns.Cast<DataColumn>().Select(c => c.ColumnName)
            .Should().Equal("Server", "Database", "Script");
        merged.Rows.Should().BeEmpty();
    }
}
