using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>
/// The seam between the orchestration engine and real database access. The production
/// implementation uses Microsoft.Data.SqlClient; tests substitute a fake so that
/// parallelism, error isolation, cancellation and progress can be verified without a
/// live SQL Server.
/// </summary>
public interface IBatchExecutor
{
    /// <summary>
    /// Open one connection to the target using the list's auth/connection settings.
    /// A failure here fails only this target.
    /// </summary>
    Task<ITargetConnection> OpenAsync(DistributionList list, Target target, CancellationToken ct);
}
