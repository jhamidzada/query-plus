using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Input;
using QueryPlus.App.ViewModels;

namespace QueryPlus.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Title = $"QueryPlus  —  build {BuildStamp()}";

        // Replace the DataGrid's built-in copy (which serializes selected cells into several
        // clipboard formats, incl. HTML, on the UI thread — hangs on large grids) with a fast
        // single-format tab-separated copy driven straight from the underlying DataView.
        ResultsGrid.CommandBindings.Add(
            new CommandBinding(ApplicationCommands.Copy, OnResultsCopy, OnCanResultsCopy));
    }

    /// <summary>The running exe's timestamp, so it's obvious which build is in use.</summary>
    private static string BuildStamp()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                return File.GetLastWriteTime(exe).ToString("yyyy-MM-dd HH:mm");
        }
        catch
        {
            // fall through
        }
        return "unknown";
    }

    private void OnCanResultsCopy(object sender, CanExecuteRoutedEventArgs e)
    {
        e.CanExecute = ResultsGrid.Items.Count > 0;
        e.Handled = true;
    }

    private void OnResultsCopy(object sender, ExecutedRoutedEventArgs e)
    {
        e.Handled = true;
        if (ResultsGrid.ItemsSource is not DataView { Table: { } table })
            return;

        // Copy the selected rows, or the whole (filtered) view when everything/nothing is selected.
        var selected = ResultsGrid.SelectedItems;
        var rows = selected.Count > 0 && selected.Count < ResultsGrid.Items.Count
            ? selected.Cast<DataRowView>()
            : ResultsGrid.Items.Cast<DataRowView>();

        var columns = table.Columns.Cast<DataColumn>().ToArray();
        var sb = new StringBuilder();
        sb.AppendLine(string.Join("\t", columns.Select(c => Clean(c.ColumnName))));
        foreach (var drv in rows)
        {
            // Merged-table values are strings (or DBNull); "as string" yields null for DBNull.
            var cells = drv.Row.ItemArray;
            sb.AppendLine(string.Join("\t", cells.Select(v => Clean(v as string))));
        }

        try
        {
            Clipboard.SetText(sb.ToString());
        }
        catch
        {
            // clipboard can be transiently locked by another app; ignore
        }
    }

    private static string Clean(string? value) =>
        (value ?? string.Empty).Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ');

    protected override void OnClosing(CancelEventArgs e)
    {
        base.OnClosing(e);
        // Persist lists + targets on exit. Matches the oracle.
        (DataContext as MainViewModel)?.SaveConfig();
    }
}
