using CommunityToolkit.Mvvm.ComponentModel;
using QueryPlus.Core.Domain;

namespace QueryPlus.App.ViewModels;

/// <summary>A single Server \ Database target, with the credentials it was added with.</summary>
public sealed partial class TargetViewModel : ObservableObject
{
    [ObservableProperty]
    private string _server = string.Empty;

    [ObservableProperty]
    private string _database = string.Empty;

    /// <summary>Transient multi-selection state for the targets list (not persisted).</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Whether this target is included in the next run (checkbox in the Targets tree).</summary>
    [ObservableProperty]
    private bool _includeInRun = true;

    /// <summary>Credentials snapshot this target connects with; null = use the list default.</summary>
    [ObservableProperty]
    private ConnectionCredentials? _credentials;

    public TargetViewModel() { }

    public TargetViewModel(string server, string database)
    {
        _server = server;
        _database = database;
    }

    public string Display
    {
        get
        {
            var text = $"{Server} \\ {Database}";
            text += Credentials is { } c
                ? $"   (override: {c.Summary()}{(c.NeedsPassword ? " — needs password" : string.Empty)})"
                : "   (list credentials)";
            return text;
        }
    }

    partial void OnCredentialsChanged(ConnectionCredentials? value) => OnPropertyChanged(nameof(Display));

    partial void OnServerChanged(string value) => OnPropertyChanged(nameof(Display));

    partial void OnDatabaseChanged(string value) => OnPropertyChanged(nameof(Display));
}
