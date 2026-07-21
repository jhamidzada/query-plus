namespace QueryPlus.Core.Domain;

/// <summary>
/// A named collection of targets plus the connection/auth settings shared by all of them.
/// </summary>
public sealed class DistributionList
{
    public string Name { get; set; } = string.Empty;

    public AuthMode Auth { get; set; } = AuthMode.Windows;

    /// <summary>
    /// Login name. For <see cref="AuthMode.Sql"/> this is the SQL login; for
    /// <see cref="AuthMode.WindowsCredentials"/> it is the Windows account name (paired with
    /// <see cref="Domain"/>).
    /// </summary>
    public string SqlUser { get; set; } = string.Empty;

    /// <summary>Password for SQL or Windows-credentials auth. Kept in memory only — never serialized.</summary>
    public string SqlPassword { get; set; } = string.Empty;

    /// <summary>Windows domain used with <see cref="AuthMode.WindowsCredentials"/>.</summary>
    public string Domain { get; set; } = string.Empty;

    public EncryptMode Encryption { get; set; } = EncryptMode.Mandatory;

    public bool TrustServerCertificate { get; set; }

    public int ConnectTimeoutSec { get; set; } = 15;

    public int CommandTimeoutSec { get; set; } = 300;

    public IList<Target> Targets { get; set; } = new List<Target>();

    /// <summary>The list-level settings as a credentials snapshot (used as the per-target fallback).</summary>
    public ConnectionCredentials DefaultCredentials() => new()
    {
        Auth = Auth,
        SqlUser = SqlUser,
        SqlPassword = SqlPassword,
        Domain = Domain,
        Encryption = Encryption,
        TrustServerCertificate = TrustServerCertificate,
        ConnectTimeoutSec = ConnectTimeoutSec,
        CommandTimeoutSec = CommandTimeoutSec
    };
}
