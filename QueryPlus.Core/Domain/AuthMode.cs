namespace QueryPlus.Core.Domain;

/// <summary>How a connection authenticates to SQL Server.</summary>
public enum AuthMode
{
    /// <summary>Windows / Integrated Security as the currently logged-in user.</summary>
    Windows,

    /// <summary>SQL Server authentication (user id + password).</summary>
    Sql,

    /// <summary>
    /// Windows / Integrated Security using explicit alternate credentials
    /// (Domain + User + Password). Equivalent to <c>runas /netonly</c>: the credentials are
    /// used only for outbound authentication to the server, so the local machine does not
    /// need a trust with that domain.
    /// </summary>
    WindowsCredentials
}
