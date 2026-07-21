namespace QueryPlus.Core.Results;

/// <summary>Progress reported as each target finishes.</summary>
public sealed class RunProgress
{
    public int CompletedTargets { get; init; }

    public int TotalTargets { get; init; }

    /// <summary>
    /// The target that just completed (the cause of this progress tick), if any.
    /// Lets the UI append results live per target as the run proceeds.
    /// </summary>
    public TargetResult? CompletedTarget { get; init; }
}
