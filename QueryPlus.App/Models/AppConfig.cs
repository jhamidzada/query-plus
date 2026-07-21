using QueryPlus.Core.Domain;

namespace QueryPlus.App.Models;

/// <summary>
/// The persisted shape of configuration: distribution lists and their targets only.
/// SQL passwords are deliberately NOT part of this model — they live in memory only.
/// </summary>
public sealed class AppConfig
{
    public List<DistributionListConfig> Lists { get; set; } = new();
}

public sealed class DistributionListConfig
{
    public string Name { get; set; } = string.Empty;
    public AuthMode Auth { get; set; } = AuthMode.Windows;
    public string SqlUser { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;

    /// <summary>Whether to persist the password (DPAPI-encrypted) for this list.</summary>
    public bool RememberPassword { get; set; } = true;

    /// <summary>DPAPI-encrypted password (current Windows user). Never plaintext. Null if not remembered.</summary>
    public string? EncryptedPassword { get; set; }

    public EncryptMode Encryption { get; set; } = EncryptMode.Mandatory;
    public bool TrustServerCertificate { get; set; }
    public int ConnectTimeoutSec { get; set; } = 15;
    public int CommandTimeoutSec { get; set; } = 300;
    public List<TargetConfig> Targets { get; set; } = new();
}

public sealed class TargetConfig
{
    public string Server { get; set; } = string.Empty;
    public string Database { get; set; } = string.Empty;

    /// <summary>
    /// Per-target credentials snapshot (non-secret fields only). Null = use the list default.
    /// Absent in older config files, which keeps them on the list default.
    /// </summary>
    public TargetCredentialsConfig? Credentials { get; set; }
}

/// <summary>Persisted per-target credentials. Passwords are intentionally NOT included.</summary>
public sealed class TargetCredentialsConfig
{
    public AuthMode Auth { get; set; } = AuthMode.Windows;
    public string SqlUser { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;

    /// <summary>DPAPI-encrypted override password (current Windows user). Null if not remembered.</summary>
    public string? EncryptedPassword { get; set; }
    public EncryptMode Encryption { get; set; } = EncryptMode.Mandatory;
    public bool TrustServerCertificate { get; set; }
    public int ConnectTimeoutSec { get; set; } = 15;
    public int CommandTimeoutSec { get; set; } = 300;
}
