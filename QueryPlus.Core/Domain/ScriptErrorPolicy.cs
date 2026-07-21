namespace QueryPlus.Core.Domain;

/// <summary>What to do when a script errors on a target.</summary>
public enum ScriptErrorPolicy
{
    /// <summary>Keep running the remaining scripts on that target (best for independent scripts).</summary>
    Continue,

    /// <summary>Skip the rest of that target's scripts; other targets keep running (best for sequences). Default.</summary>
    StopTarget,

    /// <summary>Stop launching/continuing work across all targets on the first error.</summary>
    StopRun
}
