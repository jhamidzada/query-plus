using System.Collections.Concurrent;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;

namespace QueryPlus.Core.Tests.Fakes;

/// <summary>
/// In-memory <see cref="IBatchExecutor"/> for testing orchestration without a live SQL
/// Server. Behavior per target is supplied by a factory delegate.
/// </summary>
public sealed class FakeBatchExecutor : IBatchExecutor
{
    private readonly Func<DistributionList, Target, FakeTargetConnection> _factory;

    public FakeBatchExecutor(Func<Target, FakeTargetConnection> factory)
        : this((_, target) => factory(target))
    {
    }

    public FakeBatchExecutor(Func<DistributionList, Target, FakeTargetConnection> factory)
    {
        _factory = factory;
    }

    /// <summary>Targets that were asked to open, in completion order of the open call.</summary>
    public ConcurrentBag<Target> Opened { get; } = new();

    public ConcurrentBag<FakeTargetConnection> Connections { get; } = new();

    public Task<ITargetConnection> OpenAsync(DistributionList list, Target target, CancellationToken ct)
    {
        Opened.Add(target);
        var connection = _factory(list, target);
        connection.OpenFailure?.Invoke();
        Connections.Add(connection);
        return Task.FromResult<ITargetConnection>(connection);
    }
}
