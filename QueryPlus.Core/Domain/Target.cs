namespace QueryPlus.Core.Domain;

/// <summary>A single Server \ Database destination a script is run against.</summary>
public sealed class Target
{
    public Target(string server, string database)
    {
        Server = server;
        Database = database;
    }

    public string Server { get; }

    public string Database { get; }

    /// <summary>
    /// The credentials this target was added with. When null, the engine uses the owning
    /// list's default credentials.
    /// </summary>
    public ConnectionCredentials? Credentials { get; set; }

    public override string ToString() => $"{Server} \\ {Database}";
}
