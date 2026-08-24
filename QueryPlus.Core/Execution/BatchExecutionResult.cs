using System.Data;

namespace QueryPlus.Core.Execution;

/// <summary>The outcome of executing one T-SQL batch.</summary>
public sealed class BatchExecutionResult
{
    /// <summary>Every result set produced by the batch, loaded into <see cref="DataTable"/>s.</summary>
    public IReadOnlyList<DataTable> Tables { get; init; } = Array.Empty<DataTable>();

    /// <summary>PRINT / low-severity info messages emitted while the batch ran, in order.</summary>
    public IReadOnlyList<string> InfoMessages { get; init; } = Array.Empty<string>();

    /// <summary>
    /// Rows changed by INSERT/UPDATE/DELETE/MERGE in this batch — the number SSMS reports as
    /// "(N rows affected)". Negative (typically -1) when the batch only produced result sets,
    /// which the provider reports as "no rows affected".
    /// </summary>
    public int RowsAffected { get; init; } = -1;
}
