using System.ComponentModel;
using System.Windows;
using QueryPlus.App.ViewModels;

namespace QueryPlus.App.Views;

public partial class ManageTargetsWindow : Window
{
    private ManageTargetsViewModel? _vm;
    private bool _syncing;

    public ManageTargetsWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        PasswordBox.PasswordChanged += OnPasswordChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm != null)
            _vm.PropertyChanged -= OnVmPropertyChanged;

        _vm = e.NewValue as ManageTargetsViewModel;
        if (_vm != null)
            _vm.PropertyChanged += OnVmPropertyChanged;

        UpdatePasswordBox();
    }

    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ManageTargetsViewModel.SelectedList))
            UpdatePasswordBox();
    }

    // PasswordBox.Password cannot be data-bound (security), so keep it in sync by hand.
    private void UpdatePasswordBox()
    {
        _syncing = true;
        try
        {
            PasswordBox.Password = _vm?.SelectedList?.SqlPassword ?? string.Empty;
        }
        finally
        {
            _syncing = false;
        }
    }

    private void OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_syncing)
            return;
        if (_vm?.SelectedList != null)
            _vm.SelectedList.SqlPassword = PasswordBox.Password;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
