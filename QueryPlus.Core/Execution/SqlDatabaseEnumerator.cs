using Microsoft.Data.SqlClient;
using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>
/// Production <see cref="IDatabaseEnumerator"/>: connects to the server's <c>master</c>
/// database with the list's auth/encryption settings and returns the online databases the
/// login can access.
/// </summary>
public sealed class SqlDatabaseEnumerator : IDatabaseEnumerator
{
    public async Task<IReadOnlyList<string>> GetDatabasesAsync(
        DistributionList list, string server, CancellationToken ct)
    {
        // Connect uses the list's current settings (the credentials being edited).
        var credentials = list.DefaultCredentials();
        var connectionString = ConnectionStringFactory.Build(credentials, server, "master");

        if (credentials.Auth == AuthMode.WindowsCredentials)
            return await WindowsImpersonation.RunAsync(credentials, () => QueryAsync(connectionString, credentials, ct)).ConfigureAwait(false);

        return await QueryAsync(connectionString, credentials, ct).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<string>> QueryAsync(
        string connectionString, ConnectionCredentials credentials, CancellationToken ct)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(ct).ConfigureAwait(false);

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sys.databases WHERE state = 0 AND HAS_DBACCESS(name) = 1 ORDER BY name";
        command.CommandTimeout = credentials.CommandTimeoutSec;

        var names = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
        while (await reader.ReadAsync(ct).ConfigureAwait(false))
            names.Add(reader.GetString(0));

        return names;
    }
}
