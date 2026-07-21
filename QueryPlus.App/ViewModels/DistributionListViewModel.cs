using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using QueryPlus.App.Models;
using QueryPlus.App.Services;
using QueryPlus.Core.Domain;

namespace QueryPlus.App.ViewModels;

/// <summary>
/// An editable distribution list. When <see cref="RememberPassword"/> is on, the password is
/// persisted DPAPI-encrypted (current Windows user); otherwise it lives in memory only.
/// </summary>
public sealed partial class DistributionListViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = "New List";

    [ObservableProperty]
    private AuthMode _auth = AuthMode.Windows;

    [ObservableProperty]
    private string _sqlUser = string.Empty;

    /// <summary>Persisted DPAPI-encrypted when <see cref="RememberPassword"/> is on; else memory-only.</summary>
    [ObservableProperty]
    private string _sqlPassword = string.Empty;

    /// <summary>Whether to remember the password across restarts (DPAPI-encrypted).</summary>
    [ObservableProperty]
    private bool _rememberPassword = true;

    /// <summary>Windows domain for <see cref="AuthMode.WindowsCredentials"/>.</summary>
    [ObservableProperty]
    private string _domain = string.Empty;

    [ObservableProperty]
    private EncryptMode _encryption = EncryptMode.Mandatory;

    [ObservableProperty]
    private bool _trustServerCertificate;

    [ObservableProperty]
    private int _connectTimeoutSec = 15;

    [ObservableProperty]
    private int _commandTimeoutSec = 300;

    public ObservableCollection<TargetViewModel> Targets { get; } = new();

    /// <summary>Targets grouped by server for the tree view.</summary>
    public ObservableCollection<ServerGroupViewModel> ServerGroups { get; } = new();

    public DistributionListViewModel()
    {
        Targets.CollectionChanged += (_, _) => RebuildGroups();
    }

    public void RebuildGroups()
    {
        ServerGroups.Clear();
        foreach (var group in Targets
                     .GroupBy(t => t.Server, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase))
        {
            var node = new ServerGroupViewModel(group.Key);
            foreach (var target in group.OrderBy(t => t.Database, StringComparer.OrdinalIgnoreCase))
                node.Databases.Add(target);
            ServerGroups.Add(node);
        }
    }

    public override string ToString() => Name;

    public static DistributionListViewModel FromConfig(DistributionListConfig config)
    {
        var vm = new DistributionListViewModel
        {
            Name = config.Name,
            Auth = config.Auth,
            SqlUser = config.SqlUser,
            Domain = config.Domain,
            RememberPassword = config.RememberPassword,
            SqlPassword = config.RememberPassword ? SecretProtector.Unprotect(config.EncryptedPassword) : string.Empty,
            Encryption = config.Encryption,
            TrustServerCertificate = config.TrustServerCertificate,
            ConnectTimeoutSec = config.ConnectTimeoutSec,
            CommandTimeoutSec = config.CommandTimeoutSec
        };
        foreach (var t in config.Targets)
            vm.Targets.Add(new TargetViewModel(t.Server, t.Database) { Credentials = FromCredConfig(t.Credentials) });
        vm.RebuildGroups();
        return vm;
    }

    /// <summary>The list's current settings as a credentials snapshot (includes the in-memory password).</summary>
    public ConnectionCredentials CurrentCredentials() => new()
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

    private static ConnectionCredentials? FromCredConfig(TargetCredentialsConfig? c) => c == null ? null : new ConnectionCredentials
    {
        Auth = c.Auth,
        SqlUser = c.SqlUser,
        SqlPassword = SecretProtector.Unprotect(c.EncryptedPassword),
        Domain = c.Domain,
        Encryption = c.Encryption,
        TrustServerCertificate = c.TrustServerCertificate,
        ConnectTimeoutSec = c.ConnectTimeoutSec,
        CommandTimeoutSec = c.CommandTimeoutSec
    };

    private TargetCredentialsConfig? ToCredConfig(ConnectionCredentials? c) => c == null ? null : new TargetCredentialsConfig
    {
        Auth = c.Auth,
        SqlUser = c.SqlUser,
        EncryptedPassword = RememberPassword ? SecretProtector.Protect(c.SqlPassword) : null,
        Domain = c.Domain,
        Encryption = c.Encryption,
        TrustServerCertificate = c.TrustServerCertificate,
        ConnectTimeoutSec = c.ConnectTimeoutSec,
        CommandTimeoutSec = c.CommandTimeoutSec
    };

    public DistributionListConfig ToConfig() => new()
    {
        Name = Name,
        Auth = Auth,
        SqlUser = SqlUser,
        Domain = Domain,
        RememberPassword = RememberPassword,
        EncryptedPassword = RememberPassword ? SecretProtector.Protect(SqlPassword) : null,
        Encryption = Encryption,
        TrustServerCertificate = TrustServerCertificate,
        ConnectTimeoutSec = ConnectTimeoutSec,
        CommandTimeoutSec = CommandTimeoutSec,
        Targets = Targets.Select(t => new TargetConfig
        {
            Server = t.Server,
            Database = t.Database,
            Credentials = ToCredConfig(t.Credentials)
        }).ToList()
    };

    public DistributionList ToRuntime() => new()
    {
        Name = Name,
        Auth = Auth,
        SqlUser = SqlUser,
        SqlPassword = SqlPassword,
        Domain = Domain,
        Encryption = Encryption,
        TrustServerCertificate = TrustServerCertificate,
        ConnectTimeoutSec = ConnectTimeoutSec,
        CommandTimeoutSec = CommandTimeoutSec,
        // Only checked targets run.
        Targets = Targets.Where(t => t.IncludeInRun)
            .Select(t => new Target(t.Server, t.Database) { Credentials = t.Credentials }).ToList()
    };
}
