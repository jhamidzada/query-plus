namespace QueryPlus.Core.Domain;

/// <summary>
/// Channel encryption mode, mapping to Microsoft.Data.SqlClient's
/// <c>SqlConnectionEncryptOption</c>.
/// </summary>
public enum EncryptMode
{
    /// <summary>Encrypt only if the server requires it (legacy "Encrypt=false").</summary>
    Optional,

    /// <summary>Always encrypt the connection ("Encrypt=true").</summary>
    Mandatory,

    /// <summary>
    /// TDS 8.0 strict encryption — required by SQL Server 2022/2025 hosts configured to
    /// "Force Strict Encryption". Legacy SqlClient cannot do this.
    /// </summary>
    Strict
}
