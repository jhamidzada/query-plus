namespace QueryPlus.Core.Domain;

/// <summary>
/// How a connection authenticates to SQL Server. The member names are persisted in config,
/// so they are kept stable; see <see cref="AuthModeNames"/> for the text shown in the UI.
/// </summary>
public enum AuthMode
{
    /// <summary>
    /// "Windows Authentication" — Integrated Security as the user running the app.
    /// </summary>
    Windows,

    /// <summary>
    /// "SQL Server Authentication" — a SQL login and password held on the server.
    /// </summary>
    Sql,

    /// <summary>
    /// "Windows Authentication (different account)" — Integrated Security using explicit
    /// alternate credentials (Domain + User + Password). Equivalent to <c>runas /netonly</c>:
    /// the credentials are used only for outbound authentication to the server, so the local
    /// machine does not need a trust with that domain.
    /// </summary>
    WindowsCredentials
}

/// <summary>UI names for <see cref="AuthMode"/>, matching SQL Server Management Studio's wording.</summary>
public static class AuthModeNames
{
    /// <summary>Full name, e.g. for the authentication drop-down.</summary>
    public static string Display(AuthMode mode) => mode switch
    {
        AuthMode.Windows => "Windows Authentication",
        AuthMode.Sql => "SQL Server Authentication",
        AuthMode.WindowsCredentials => "Windows Authentication (different account)",
        _ => mode.ToString()
    };

    /// <summary>Compact name for dense lists, e.g. a target's credential label.</summary>
    public static string Short(AuthMode mode) => mode switch
    {
        AuthMode.Windows => "Windows",
        AuthMode.Sql => "SQL login",
        AuthMode.WindowsCredentials => "Windows acct",
        _ => mode.ToString()
    };
}
