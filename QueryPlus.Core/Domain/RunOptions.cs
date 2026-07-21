namespace QueryPlus.Core.Domain;

/// <summary>Options controlling a run.</summary>
public sealed class RunOptions
{
    /// <summary>Maximum number of targets executed concurrently.</summary>
    public int MaxParallel { get; set; } = 8;

    /// <summary>What to do when a script errors on a target.</summary>
    public ScriptErrorPolicy ErrorPolicy { get; set; } = ScriptErrorPolicy.StopTarget;

    /// <summary>
    /// Optional sink notified each time a script finishes on a target. Lets the UI show
    /// per-script progress (kept out of <c>IProgress&lt;RunProgress&gt;</c> so the per-target
    /// counter stays clean).
    /// </summary>
    public IProgress<ScriptProgress>? ScriptProgress { get; set; }
}
