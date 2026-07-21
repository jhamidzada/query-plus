using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;

namespace QueryPlus.App.ViewModels;

/// <summary>
/// Backs the Manage Targets dialog. Targets are edited with a two-pane shuttle: connect to a
/// server to list its databases (Available), then move them to/from this list's Targets in
/// bulk via multi-selection.
/// </summary>
public sealed partial class ManageTargetsViewModel : ObservableObject
{
    private readonly IDatabaseEnumerator _enumerator;
    private readonly List<string> _serverDatabases = new();
    private string _connectedServer = string.Empty;

    public MainViewModel Main { get; }

    public IEnumerable<AuthMode> AuthModes { get; } = Enum.GetValues<AuthMode>();

    public IEnumerable<EncryptMode> EncryptModes { get; } = Enum.GetValues<EncryptMode>();

    /// <summary>Databases on the connected server that are not yet targets (the "Available" pane).</summary>
    public ObservableCollection<DatabaseChoice> Databases { get; } = new();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveListCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddAllCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedTargetsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ApplyCredentialsToSelectedCommand))]
    [NotifyCanExecuteChangedFor(nameof(UseListCredentialsCommand))]
    [NotifyCanExecuteChangedFor(nameof(AddTargetCommand))]
    private DistributionListViewModel? _selectedList;

    [ObservableProperty]
    private string _newServer = string.Empty;

    [ObservableProperty]
    private string _newDatabase = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private string _serverToConnect = string.Empty;

    [ObservableProperty]
    private string _connectStatus = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand))]
    private bool _isConnecting;

    public ManageTargetsViewModel(MainViewModel main, IDatabaseEnumerator enumerator)
    {
        Main = main;
        _enumerator = enumerator;
        SelectedList = main.SelectedList ?? main.Lists.FirstOrDefault();
    }

    partial void OnSelectedListChanged(DistributionListViewModel? value)
    {
        // A different list may use different auth; reset the connected-server state.
        _serverDatabases.Clear();
        _connectedServer = string.Empty;
        Databases.Clear();
        ConnectStatus = string.Empty;
    }

    [RelayCommand]
    private void AddList()
    {
        var list = new DistributionListViewModel { Name = $"List {Main.Lists.Count + 1}" };
        Main.Lists.Add(list);
        SelectedList = list;
    }

    private bool CanRemoveList() => SelectedList != null;

    [RelayCommand(CanExecute = nameof(CanRemoveList))]
    private void RemoveList()
    {
        if (SelectedList == null)
            return;
        var index = Main.Lists.IndexOf(SelectedList);
        Main.Lists.Remove(SelectedList);
        SelectedList = Main.Lists.Count == 0
            ? null
            : Main.Lists[Math.Min(index, Main.Lists.Count - 1)];
    }

    private bool CanConnect() => !IsConnecting && SelectedList != null && !string.IsNullOrWhiteSpace(ServerToConnect);

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        var list = SelectedList;
        if (list == null)
            return;

        var server = ServerToConnect.Trim();
        IsConnecting = true;
        _serverDatabases.Clear();
        Databases.Clear();
        ConnectStatus = $"Connecting to {server}…";
        try
        {
            var databases = await _enumerator.GetDatabasesAsync(list.ToRuntime(), server, CancellationToken.None);
            _serverDatabases.AddRange(databases);
            _connectedServer = server;
            RebuildAvailable();
            ConnectStatus = databases.Count == 0
                ? $"Connected, but no databases are visible to this login on {server}."
                : $"Found {databases.Count} database(s) on {server}.";
        }
        catch (Exception ex)
        {
            _connectedServer = string.Empty;
            ConnectStatus = $"Connect failed: {ex.Message}";
        }
        finally
        {
            IsConnecting = false;
        }
    }

    private bool CanAddFromServer() => SelectedList != null;

    [RelayCommand(CanExecute = nameof(CanAddFromServer))]
    private void AddSelected()
    {
        foreach (var choice in Databases.Where(d => d.IsSelected).ToList())
            AddTargetIfNew(_connectedServer, choice.Name);
        RebuildAvailable();
    }

    [RelayCommand(CanExecute = nameof(CanAddFromServer))]
    private void AddAll()
    {
        foreach (var choice in Databases.ToList())
            AddTargetIfNew(_connectedServer, choice.Name);
        RebuildAvailable();
    }

    [RelayCommand(CanExecute = nameof(CanAddFromServer))]
    private void RemoveSelectedTargets()
    {
        if (SelectedList == null)
            return;
        foreach (var target in SelectedList.Targets.Where(t => t.IsSelected).ToList())
            SelectedList.Targets.Remove(target);
        RebuildAvailable();
    }

    /// <summary>Overrides selected targets with the list's current credentials (incl. the typed password).</summary>
    [RelayCommand(CanExecute = nameof(CanAddFromServer))]
    private void ApplyCredentialsToSelected()
    {
        if (SelectedList == null)
            return;
        foreach (var target in SelectedList.Targets.Where(t => t.IsSelected))
            target.Credentials = SelectedList.CurrentCredentials();
    }

    /// <summary>Clears any per-target override on selected targets so they use the list's credentials.</summary>
    [RelayCommand(CanExecute = nameof(CanAddFromServer))]
    private void UseListCredentials()
    {
        if (SelectedList == null)
            return;
        foreach (var target in SelectedList.Targets.Where(t => t.IsSelected))
            target.Credentials = null;
    }

    private bool CanAddTarget() => SelectedList != null && !string.IsNullOrWhiteSpace(NewServer) && !string.IsNullOrWhiteSpace(NewDatabase);

    [RelayCommand(CanExecute = nameof(CanAddTarget))]
    private void AddTarget()
    {
        AddTargetIfNew(NewServer.Trim(), NewDatabase.Trim());
        NewServer = string.Empty;
        NewDatabase = string.Empty;
        RebuildAvailable();
    }

    /// <summary>Refresh the Available pane = connected server's databases that aren't already targets.</summary>
    private void RebuildAvailable()
    {
        Databases.Clear();
        if (SelectedList == null || _connectedServer.Length == 0)
            return;

        foreach (var name in _serverDatabases)
        {
            var isTarget = SelectedList.Targets.Any(t =>
                string.Equals(t.Server, _connectedServer, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(t.Database, name, StringComparison.OrdinalIgnoreCase));
            if (!isTarget)
                Databases.Add(new DatabaseChoice(name));
        }
    }

    private bool AddTargetIfNew(string server, string database)
    {
        if (SelectedList == null || server.Length == 0 || database.Length == 0)
            return false;

        var exists = SelectedList.Targets.Any(t =>
            string.Equals(t.Server, server, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(t.Database, database, StringComparison.OrdinalIgnoreCase));
        if (exists)
            return false;

        // Targets inherit the list's credentials by default (Credentials = null). Use
        // "Override selected with current credentials" only for mixed-credential lists.
        SelectedList.Targets.Add(new TargetViewModel(server, database));
        return true;
    }

    partial void OnNewServerChanged(string value) => AddTargetCommand.NotifyCanExecuteChanged();

    partial void OnNewDatabaseChanged(string value) => AddTargetCommand.NotifyCanExecuteChanged();
}
