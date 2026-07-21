using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using Microsoft.Win32.SafeHandles;
using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>
/// Runs an action while impersonating explicit Windows credentials, the way
/// <c>runas /netonly</c> does. Uses <c>LOGON32_LOGON_NEW_CREDENTIALS</c> so the credentials
/// are presented only for outbound network authentication to the SQL Server — the local
/// machine does not need to trust the supplied domain.
/// </summary>
internal static class WindowsImpersonation
{
    private const int LOGON32_LOGON_NEW_CREDENTIALS = 9;
    private const int LOGON32_PROVIDER_WINNT50 = 3;

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool LogonUser(
        string lpszUsername,
        string? lpszDomain,
        string lpszPassword,
        int dwLogonType,
        int dwLogonProvider,
        out SafeAccessTokenHandle phToken);

    /// <summary>Logs on with the alternate credentials and runs <paramref name="action"/> impersonated.</summary>
    public static async Task<T> RunAsync<T>(ConnectionCredentials credentials, Func<Task<T>> action)
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Alternate Windows credentials require Windows.");

        var domain = string.IsNullOrWhiteSpace(credentials.Domain) ? null : credentials.Domain;
        if (!LogonUser(credentials.SqlUser, domain, credentials.SqlPassword,
                LOGON32_LOGON_NEW_CREDENTIALS, LOGON32_PROVIDER_WINNT50, out var token))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Could not log on with the supplied Windows credentials.");
        }

        using (token)
        {
            return await WindowsIdentity.RunImpersonatedAsync(token, action).ConfigureAwait(false);
        }
    }
}
