using QueryPlus.Core.Domain;
using QueryPlus.Core.Results;

namespace QueryPlus.Core.Engine;

/// <summary>Runs scripts against a distribution list's targets in parallel.</summary>
public interface IScriptRunner
{
    Task<RunReport> RunAsync(
        DistributionList list,
        IReadOnlyList<ScriptItem> scripts,
        RunOptions options,
        IProgress<RunProgress>? progress,
        CancellationToken ct);
}
