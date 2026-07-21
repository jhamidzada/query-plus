namespace QueryPlus.Core.Execution;

/// <summary>
/// An open session against one target. Batches execute sequentially over this single
/// connection (order matters). Disposed when the target is done.
/// </summary>
public interface ITargetConnection : IAsyncDisposable
{
    /// <summary>
    /// Execute a single T-SQL batch, returning its result sets and any info messages
    /// emitted during execution. Throwing here is treated as a script-level error by the
    /// engine and isolated to this target.
    /// </summary>
    Task<BatchExecutionResult> ExecuteBatchAsync(string batchText, CancellationToken ct);
}
