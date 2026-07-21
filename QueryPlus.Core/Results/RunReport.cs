namespace QueryPlus.Core.Results;

/// <summary>The complete output of a run, one entry per target (in target order).</summary>
public sealed class RunReport
{
    public RunReport(IReadOnlyList<TargetResult> targets)
    {
        Targets = targets;
    }

    public IReadOnlyList<TargetResult> Targets { get; }
}
