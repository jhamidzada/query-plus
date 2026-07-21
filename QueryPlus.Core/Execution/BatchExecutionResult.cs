using System.Data;

namespace QueryPlus.Core.Execution;

/// <summary>The outcome of executing one T-SQL batch.</summary>
public sealed class BatchExecutionResult
{
    /// <summary>Every result set produced by the batch, loaded into <see cref="DataTable"/>s.</summary>
    public IReadOnlyList<DataTable> Tables { get; init; } = Array.Empty<DataTable>();

    /// <summary>PRINT / low-severity info messages emitted while the batch ran, in order.</summary>
    public IReadOnlyList<string> InfoMessages { get; init; } = Array.Empty<string>();
}
