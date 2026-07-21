using System.Windows;
using QueryPlus.App.ViewModels;
using QueryPlus.App.Views;
using QueryPlus.Core.Execution;

namespace QueryPlus.App.Services;

public sealed class ManageTargetsService : IManageTargetsService
{
    private readonly IDatabaseEnumerator _enumerator;

    public ManageTargetsService(IDatabaseEnumerator enumerator) => _enumerator = enumerator;

    public void Show(MainViewModel main)
    {
        var window = new ManageTargetsWindow
        {
            DataContext = new ManageTargetsViewModel(main, _enumerator),
            Owner = Application.Current?.MainWindow
        };
        window.ShowDialog();
    }
}
