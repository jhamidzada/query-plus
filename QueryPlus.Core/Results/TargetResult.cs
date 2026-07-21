namespace QueryPlus.Core.Results;

/// <summary>Everything produced by running the scripts against a single target.</summary>
public sealed class TargetResult
{
    public TargetResult(string server, string database)
    {
        Server = server;
        Database = database;
    }

    public string Server { get; }

    public string Database { get; }

    public IList<ResultSet> ResultSets { get; } = new List<ResultSet>();

    /// <summary>PRINT / informational output and "batch completed" notices.</summary>
    public IList<string> Messages { get; } = new List<string>();

    public IList<string> Errors { get; } = new List<string>();

    public TimeSpan Elapsed { get; set; }
}
