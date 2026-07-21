using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>Lists the databases available on a server, using a list's auth/connection settings.</summary>
public interface IDatabaseEnumerator
{
    Task<IReadOnlyList<string>> GetDatabasesAsync(DistributionList list, string server, CancellationToken ct);
}
