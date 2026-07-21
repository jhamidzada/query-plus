using Microsoft.Data.SqlClient;
using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>Builds a connection string for a server/database from a credentials snapshot.</summary>
public static class ConnectionStringFactory
{
    public const string ApplicationName = "QueryPlus";

    public static string Build(ConnectionCredentials credentials, string server, string database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = server,
            InitialCatalog = database,
            ApplicationName = ApplicationName,
            Encrypt = credentials.Encryption switch
            {
                EncryptMode.Optional => SqlConnectionEncryptOption.Optional,
                EncryptMode.Strict => SqlConnectionEncryptOption.Strict,
                _ => SqlConnectionEncryptOption.Mandatory
            },
            TrustServerCertificate = credentials.TrustServerCertificate,
            ConnectTimeout = credentials.ConnectTimeoutSec
        };

        if (credentials.Auth == AuthMode.Sql)
        {
            builder.UserID = credentials.SqlUser;
            builder.Password = credentials.SqlPassword;
        }
        else
        {
            // Windows and WindowsCredentials both use integrated security; for the latter the
            // alternate credentials are supplied by impersonation at connection-open time.
            builder.IntegratedSecurity = true;
        }

        return builder.ConnectionString;
    }
}
