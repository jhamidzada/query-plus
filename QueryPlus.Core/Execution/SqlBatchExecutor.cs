using System.Collections.Concurrent;
using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using QueryPlus.Core.Domain;

namespace QueryPlus.Core.Execution;

/// <summary>
/// Production <see cref="IBatchExecutor"/> backed by Microsoft.Data.SqlClient. One
/// <see cref="SqlConnection"/> per target, with PRINT/info output captured via
/// <see cref="SqlConnection.InfoMessage"/>.
/// </summary>
public sealed class SqlBatchExecutor : IBatchExecutor
{
    public async Task<ITargetConnection> OpenAsync(DistributionList list, Target target, CancellationToken ct)
    {
        // Each target uses its own override when usable; otherwise (incl. an override missing its
        // password) it falls back to the list's credentials.
        var credentials = CredentialResolver.Resolve(list, target);
        var connectionString = ConnectionStringFactory.Build(credentials, target.Server, target.Database);

        if (credentials.Auth == AuthMode.WindowsCredentials)
        {
            // Open under impersonation so the alternate Windows credentials authenticate to the
            // server; once opened, the session is authenticated and later commands need no impersonation.
            return await WindowsImpersonation.RunAsync(credentials,
                () => OpenCoreAsync(connectionString, credentials.CommandTimeoutSec, ct)).ConfigureAwait(false);
        }

        return await OpenCoreAsync(connectionString, credentials.CommandTimeoutSec, ct).ConfigureAwait(false);
    }

    private static async Task<ITargetConnection> OpenCoreAsync(string connectionString, int commandTimeoutSec, CancellationToken ct)
    {
        var connection = new SqlConnection(connectionString);
        try
        {
            var session = new SqlTargetConnection(connection, commandTimeoutSec);
            await connection.OpenAsync(ct).ConfigureAwait(false);
            return session;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class SqlTargetConnection : ITargetConnection
    {
        private readonly SqlConnection _connection;
        private readonly int _commandTimeoutSec;
        private readonly ConcurrentQueue<string> _infoMessages = new();

        public SqlTargetConnection(SqlConnection connection, int commandTimeoutSec)
        {
            _connection = connection;
            _commandTimeoutSec = commandTimeoutSec;
            _connection.InfoMessage += OnInfoMessage;
        }

        private void OnInfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            foreach (SqlError error in e.Errors)
                _infoMessages.Enqueue(error.Message);
        }

        public async Task<BatchExecutionResult> ExecuteBatchAsync(string batchText, CancellationToken ct)
        {
            // Drain messages buffered since the previous batch so each batch reports its own.
            while (_infoMessages.TryDequeue(out _)) { }

            var tables = new List<DataTable>();
            using var command = _connection.CreateCommand();
            command.CommandText = batchText;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = _commandTimeoutSec;

            await using var reader = await command.ExecuteReaderAsync(ct).ConfigureAwait(false);
            do
            {
                if (reader.FieldCount > 0)
                    tables.Add(await ReadTableAsync(reader, ct).ConfigureAwait(false));
            }
            while (await reader.NextResultAsync(ct).ConfigureAwait(false));

            var messages = new List<string>();
            while (_infoMessages.TryDequeue(out var message))
                messages.Add(message);

            return new BatchExecutionResult { Tables = tables, InfoMessages = messages };
        }

        private static async Task<DataTable> ReadTableAsync(SqlDataReader reader, CancellationToken ct)
        {
            var table = new DataTable { Locale = CultureInfo.InvariantCulture };
            var used = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                var name = reader.GetName(i);
                if (string.IsNullOrEmpty(name))
                    name = $"Column{i + 1}";
                var unique = name;
                var suffix = 1;
                while (!used.Add(unique))
                    unique = $"{name}_{suffix++}";
                table.Columns.Add(unique, reader.GetFieldType(i) ?? typeof(object));
            }

            var values = new object[reader.FieldCount];
            while (await reader.ReadAsync(ct).ConfigureAwait(false))
            {
                reader.GetValues(values);
                table.Rows.Add((object[])values.Clone());
            }

            return table;
        }

        public async ValueTask DisposeAsync()
        {
            _connection.InfoMessage -= OnInfoMessage;
            await _connection.DisposeAsync().ConfigureAwait(false);
        }
    }
}
