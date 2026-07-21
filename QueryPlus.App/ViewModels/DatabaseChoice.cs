using CommunityToolkit.Mvvm.ComponentModel;

namespace QueryPlus.App.ViewModels;

/// <summary>A database returned by Connect, with a checkbox for selection.</summary>
public sealed partial class DatabaseChoice : ObservableObject
{
    public DatabaseChoice(string name) => Name = name;

    public string Name { get; }

    [ObservableProperty]
    private bool _isSelected;
}
