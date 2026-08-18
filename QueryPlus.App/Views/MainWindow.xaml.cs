using System.ComponentModel;
using System.Data;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QueryPlus.App.ViewModels;

namespace QueryPlus.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Replace the DataGrid's built-in copy (which serializes selected cells into several
        // clipboard formats, incl. HTML, on the UI thread — hangs on large grids) with a fast
        // single-format tab-separated copy driven straight from the underlying DataView.
        ResultsGrid.CommandBindings.Add(
            new CommandBinding(ApplicationCommands.Copy, OnResultsCopy, OnCanResultsCopy));
    }

    // ---------------------------------------------------------------------
    // Drag-and-drop reordering of the Scripts list.
    //
    // Scripts execute top-to-bottom, so their order is meaningful. The list is
    // reordered live while dragging (the item follows the cursor between rows),
    // which gives immediate feedback without needing a drop-indicator adorner.
    // ---------------------------------------------------------------------

    private Point _dragStart;
    private ScriptTabViewModel? _dragItem;

    private void ScriptsList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't hijack clicks on the include-in-run checkbox.
        if (FindAncestor<CheckBox>(e.OriginalSource as DependencyObject) != null)
        {
            _dragItem = null;
            return;
        }

        _dragStart = e.GetPosition(null);
        _dragItem = ItemAt(e.GetPosition(ScriptsList));
    }

    private void ScriptsList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragItem == null)
            return;

        var delta = e.GetPosition(null) - _dragStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var item = _dragItem;
        _dragItem = null;
        DragDrop.DoDragDrop(ScriptsList, item, DragDropEffects.Move);
    }

    private void ScriptsList_DragOver(object sender, DragEventArgs e)
    {
        var dragged = e.Data.GetData(typeof(ScriptTabViewModel)) as ScriptTabViewModel;
        var scripts = (DataContext as MainViewModel)?.ScriptTabs;
        if (dragged == null || scripts == null)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        var point = e.GetPosition(ScriptsList);
        var over = ItemAt(point);

        // Hitting empty space below the rows means "move to the end".
        var to = over != null ? scripts.IndexOf(over) : (IsBelowLastItem(point) ? scripts.Count - 1 : -1);
        var from = scripts.IndexOf(dragged);
        if (from >= 0 && to >= 0 && from != to)
            scripts.Move(from, to);
    }

    private void ScriptsList_Drop(object sender, DragEventArgs e)
    {
        // The list was already reordered during DragOver; just keep the moved
        // script selected so the editor still shows it.
        if (e.Data.GetData(typeof(ScriptTabViewModel)) is ScriptTabViewModel dropped &&
            DataContext is MainViewModel vm)
        {
            vm.SelectedTab = dropped;
        }
        e.Handled = true;
    }

    /// <summary>True when the point sits in the empty area past the last row.</summary>
    private bool IsBelowLastItem(Point point)
    {
        if (ScriptsList.Items.Count == 0)
            return false;
        if (ScriptsList.ItemContainerGenerator.ContainerFromIndex(ScriptsList.Items.Count - 1)
            is not ListBoxItem last)
        {
            return false; // virtualized away: it is off-screen, so we are not past it
        }

        var bottom = last.TransformToAncestor(ScriptsList)
            .Transform(new Point(0, last.ActualHeight)).Y;
        return point.Y > bottom;
    }

    /// <summary>The script whose row is under <paramref name="point"/> (list coordinates), if any.</summary>
    private ScriptTabViewModel? ItemAt(Point point)
    {
        var hit = ScriptsList.InputHitTest(point) as DependencyObject;
        return FindAncestor<ListBoxItem>(hit)?.DataContext as ScriptTabViewModel;
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
                return match;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }
        return null;
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
