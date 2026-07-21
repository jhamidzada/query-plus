namespace QueryPlus.Core.Scripting;

/// <summary>A single batch produced by splitting a script on GO separators.</summary>
public sealed class SqlBatch
{
    public SqlBatch(string text, int count = 1)
    {
        Text = text;
        Count = count;
    }

    /// <summary>The batch text (without the trailing GO line).</summary>
    public string Text { get; }

    /// <summary>How many times to execute the batch (from a <c>GO n</c> repeat count). Always &gt;= 1.</summary>
    public int Count { get; }
}
