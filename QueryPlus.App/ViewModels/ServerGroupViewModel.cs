using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace QueryPlus.App.ViewModels;

/// <summary>A server node in the targets tree, grouping its databases.</summary>
public sealed partial class ServerGroupViewModel : ObservableObject
{
    private bool _suppress;

    public ServerGroupViewModel(string server)
    {
        Server = server;
        Databases.CollectionChanged += OnDatabasesChanged;
    }

    public string Server { get; }

    public ObservableCollection<TargetViewModel> Databases { get; } = new();

    /// <summary>
    /// Tri-state include for all this server's databases: true = all, false = none, null = mixed.
    /// Setting it (via a two-state checkbox click) includes/excludes every database.
    /// </summary>
    public bool? AllIncluded
    {
        get
        {
            if (Databases.Count == 0)
                return false;
            var included = Databases.Count(d => d.IncludeInRun);
            if (included == 0) return false;
            if (included == Databases.Count) return true;
            return null;
        }
        set
        {
            if (value is not { } include)
                return;
            _suppress = true;
            foreach (var db in Databases)
                db.IncludeInRun = include;
            _suppress = false;
            OnPropertyChanged();
        }
    }

    private void OnDatabasesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (TargetViewModel t in e.OldItems)
                t.PropertyChanged -= OnDatabaseChanged;
        if (e.NewItems != null)
            foreach (TargetViewModel t in e.NewItems)
                t.PropertyChanged += OnDatabaseChanged;
        OnPropertyChanged(nameof(AllIncluded));
    }

    private void OnDatabaseChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TargetViewModel.IncludeInRun) && !_suppress)
            OnPropertyChanged(nameof(AllIncluded));
    }
}
