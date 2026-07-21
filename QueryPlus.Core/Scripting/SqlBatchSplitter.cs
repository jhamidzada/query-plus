using System.Globalization;
using System.Text.RegularExpressions;

namespace QueryPlus.Core.Scripting;

/// <summary>
/// Splits a T-SQL script into batches on <c>GO</c> separators.
///
/// Improvement over the PowerShell oracle (which uses a naive per-line regex): this
/// splitter is comment- and string-literal-aware. A <c>GO</c> is only a separator when it
/// stands alone on a line that begins in normal code — never when it appears inside a
/// <c>--</c> line comment, a <c>/* */</c> block comment (nesting supported), or a
/// <c>'...'</c> string literal. Bracketed <c>[..]</c> and quoted <c>"..."</c> identifiers
/// are tracked too. <c>GO n</c> repeat counts are honored.
/// </summary>
public static class SqlBatchSplitter
{
    private enum St
    {
        Code,
        LineComment,
        BlockComment,
        SingleQuote,
        DoubleQuote,
        Bracket
    }

    // A line that is nothing but GO (optionally a repeat count, optionally a trailing comment).
    private static readonly Regex GoLine = new(
        @"^\s*go(?:[ \t]+(\d+))?[ \t]*(?:--.*)?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SqlBatch> Split(string? sql)
    {
        var batches = new List<SqlBatch>();
        if (string.IsNullOrEmpty(sql))
            return batches;

        // Pass 1: record the start index of every line and the parser state in effect there.
        var lineStart = new List<int> { 0 };
        var lineStartState = new List<St> { St.Code };

        var state = St.Code;
        var blockDepth = 0;
        var n = sql.Length;
        var i = 0;
        while (i < n)
        {
            var c = sql[i];
            var next = i + 1 < n ? sql[i + 1] : '\0';

            switch (state)
            {
                case St.Code:
                    if (c == '-' && next == '-') { state = St.LineComment; i += 2; continue; }
                    if (c == '/' && next == '*') { state = St.BlockComment; blockDepth = 1; i += 2; continue; }
                    if (c == '\'') { state = St.SingleQuote; i++; continue; }
                    if (c == '"') { state = St.DoubleQuote; i++; continue; }
                    if (c == '[') { state = St.Bracket; i++; continue; }
                    break;
                case St.LineComment:
                    if (c == '\n') state = St.Code; // comment ends at end of line
                    break;
                case St.BlockComment:
                    if (c == '/' && next == '*') { blockDepth++; i += 2; continue; }
                    if (c == '*' && next == '/') { blockDepth--; i += 2; if (blockDepth == 0) state = St.Code; continue; }
                    break;
                case St.SingleQuote:
                    if (c == '\'') { if (next == '\'') { i += 2; continue; } state = St.Code; }
                    break;
                case St.DoubleQuote:
                    if (c == '"') { if (next == '"') { i += 2; continue; } state = St.Code; }
                    break;
                case St.Bracket:
                    if (c == ']') { if (next == ']') { i += 2; continue; } state = St.Code; }
                    break;
            }

            if (c == '\n')
            {
                lineStart.Add(i + 1);
                lineStartState.Add(state);
            }

            i++;
        }

        // Pass 2: slice batches between GO separator lines.
        var batchStart = 0;
        for (var k = 0; k < lineStart.Count; k++)
        {
            var start = lineStart[k];
            var end = k + 1 < lineStart.Count ? lineStart[k + 1] : n;
            if (lineStartState[k] != St.Code)
                continue; // line begins inside a comment/string — GO here is not a separator

            var lineText = sql.Substring(start, end - start);
            var m = GoLine.Match(lineText);
            if (!m.Success)
                continue;

            var count = 1;
            if (m.Groups[1].Success)
                count = Math.Max(1, int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));

            AddBatch(batches, sql, batchStart, start, count);
            batchStart = end;
        }

        AddBatch(batches, sql, batchStart, n, 1);
        return batches;
    }

    private static void AddBatch(List<SqlBatch> batches, string sql, int start, int end, int count)
    {
        if (end <= start)
            return;
        var text = sql.Substring(start, end - start).Trim();
        if (text.Length == 0)
            return;
        batches.Add(new SqlBatch(text, count));
    }
}
