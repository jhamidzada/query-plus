using System.Collections.Concurrent;
using QueryPlus.Core.Execution;

namespace QueryPlus.Core.Tests.Fakes;

/// <summary>A scripted <see cref="ITargetConnection"/> for tests.</summary>
public sealed class FakeTargetConnection : ITargetConnection
{
    /// <summary>Per-batch behavior. Receives the batch text and cancellation token.</summary>
    public Func<string, CancellationToken, Task<BatchExecutionResult>> OnExecute { get; set; }
        = (_, _) => Task.FromResult(new BatchExecutionResult());

    /// <summary>If set, invoked during OpenAsync to simulate a connection failure (throw here).</summary>
    public Action? OpenFailure { get; set; }

    public ConcurrentQueue<string> ExecutedBatches { get; } = new();

    public int DisposeCount;

    public async Task<BatchExecutionResult> ExecuteBatchAsync(string batchText, CancellationToken ct)
    {
        ExecutedBatches.Enqueue(batchText);
        return await OnExecute(batchText, ct).ConfigureAwait(false);
    }

    public ValueTask DisposeAsync()
    {
        Interlocked.Increment(ref DisposeCount);
        return ValueTask.CompletedTask;
    }
}
