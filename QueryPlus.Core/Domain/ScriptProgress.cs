namespace QueryPlus.Core.Domain;

/// <summary>How a script ended on a particular target.</summary>
public enum ScriptOutcome
{
    /// <summary>Ran without error.</summary>
    Completed,

    /// <summary>Errored while executing.</summary>
    Faulted,

    /// <summary>Did not run (an earlier script stopped the target, or the connection failed).</summary>
    Skipped
}

/// <summary>Reported each time a script finishes (or is skipped) on a target, for per-script progress.</summary>
public sealed class ScriptProgress
{
    public ScriptProgress(string scriptName, ScriptOutcome outcome)
    {
        ScriptName = scriptName;
        Outcome = outcome;
    }

    public string ScriptName { get; }

    public ScriptOutcome Outcome { get; }
}
