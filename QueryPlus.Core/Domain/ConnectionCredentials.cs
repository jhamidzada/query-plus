namespace QueryPlus.Core.Domain;

/// <summary>
/// A self-contained set of connection settings. A <see cref="Target"/> may carry its own
/// snapshot (the credentials it was added with); otherwise the engine falls back to the
/// owning list's defaults.
/// </summary>
public sealed class ConnectionCredentials
{
    public AuthMode Auth { get; set; } = AuthMode.Windows;

    /// <summary>SQL login (Sql auth) or Windows account name (WindowsCredentials auth).</summary>
    public string SqlUser { get; set; } = string.Empty;

    /// <summary>Password for SQL or Windows-credentials auth. In memory only — never serialized.</summary>
    public string SqlPassword { get; set; } = string.Empty;

    /// <summary>Windows domain for WindowsCredentials auth.</summary>
    public string Domain { get; set; } = string.Empty;

    public EncryptMode Encryption { get; set; } = EncryptMode.Mandatory;

    public bool TrustServerCertificate { get; set; }

    public int ConnectTimeoutSec { get; set; } = 15;

    public int CommandTimeoutSec { get; set; } = 300;

    public ConnectionCredentials Clone() => (ConnectionCredentials)MemberwiseClone();

    /// <summary>True when this credential set needs a password that hasn't been supplied yet.</summary>
    public bool NeedsPassword =>
        (Auth == AuthMode.Sql || Auth == AuthMode.WindowsCredentials) && string.IsNullOrEmpty(SqlPassword);

    /// <summary>Short, password-free description for the UI.</summary>
    public string Summary() => Auth switch
    {
        AuthMode.Sql => $"Sql: {SqlUser}",
        AuthMode.WindowsCredentials => $"Win: {(string.IsNullOrEmpty(Domain) ? string.Empty : Domain + "\\")}{SqlUser}",
        _ => "Windows"
    };
}
