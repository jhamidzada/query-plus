using FluentAssertions;
using QueryPlus.Core.Scripting;

namespace QueryPlus.Core.Tests;

public class SqlBatchSplitterTests
{
    [Fact]
    public void Splits_on_GO_separator()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\nGO\nSELECT 2");

        batches.Select(b => b.Text).Should().Equal("SELECT 1", "SELECT 2");
        batches.Should().OnlyContain(b => b.Count == 1);
    }

    [Fact]
    public void Is_case_insensitive()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\ngo\nSELECT 2\nGo\nSELECT 3");

        batches.Select(b => b.Text).Should().Equal("SELECT 1", "SELECT 2", "SELECT 3");
    }

    [Fact]
    public void Honors_repeat_count()
    {
        var batches = SqlBatchSplitter.Split("INSERT INTO t VALUES (1)\nGO 3");

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be("INSERT INTO t VALUES (1)");
        batches[0].Count.Should().Be(3);
    }

    [Fact]
    public void Allows_leading_and_trailing_whitespace_on_GO_line()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\n   \tGO\t  \nSELECT 2");

        batches.Select(b => b.Text).Should().Equal("SELECT 1", "SELECT 2");
    }

    [Fact]
    public void Single_batch_when_no_GO()
    {
        var batches = SqlBatchSplitter.Split("SELECT 1\nSELECT 2");

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be("SELECT 1\nSELECT 2");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n  \t ")]
    [InlineData("GO")]
    [InlineData("GO\nGO\nGO")]
    public void Produces_no_batches_for_empty_or_separators_only(string sql)
    {
        SqlBatchSplitter.Split(sql).Should().BeEmpty();
    }

    [Fact]
    public void Does_not_split_on_GO_inside_line_comment()
    {
        var sql = "SELECT 1\n-- GO\nSELECT 2";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be(sql);
    }

    [Fact]
    public void Does_not_split_on_GO_inside_block_comment()
    {
        var sql = "SELECT 1\n/*\nGO\n*/\nSELECT 2";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be(sql);
    }

    [Fact]
    public void Does_not_split_on_GO_inside_string_literal()
    {
        var sql = "SELECT '\nGO\n' AS x";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be(sql);
    }

    [Fact]
    public void Splits_real_GO_after_a_block_comment_that_contained_GO()
    {
        var sql = "/* GO inside */\nSELECT 1\nGO\nSELECT 2";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Select(b => b.Text).Should().Equal("/* GO inside */\nSELECT 1", "SELECT 2");
    }

    [Fact]
    public void Does_not_treat_GO_with_trailing_statement_as_separator()
    {
        var sql = "SELECT 1\nGO SELECT 2";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().ContainSingle();
        batches[0].Text.Should().Be(sql);
    }

    [Fact]
    public void Splits_final_batch_after_dynamic_sql_with_many_escaped_quotes()
    {
        // Mirrors the report script: a big dynamic-SQL string with '''+var+''' sequences,
        // a GO, then a standalone SELECT. The SELECT must be its own batch.
        var sql =
            "SET @sql = @sql1 + 'SELECT CAST('''+@@servername+''' AS NVARCHAR(100)) AS server_name,\n" +
            "        CAST(''' + @dbname + ''' AS NVARCHAR(100)) AS client_db\n" +
            "    FROM ' + @dbname + '.dbo.t\n" +
            "    WHERE d BETWEEN '''+@startDate+''' AND '''+@endDate+''''\n" +
            "EXEC sp_executesql @sql\n" +
            "GO\n" +
            "SELECT * FROM [_temp_ua_report_usage] order by 1";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().HaveCount(2);
        batches[1].Text.Should().Be("SELECT * FROM [_temp_ua_report_usage] order by 1");
    }

    [Fact]
    public void Does_not_treat_identifier_starting_with_go_as_separator()
    {
        var sql = "SELECT 1\nGOTO_LABEL:\nSELECT 2";

        var batches = SqlBatchSplitter.Split(sql);

        batches.Should().ContainSingle();
    }
}
