using Microsoft.Win32;

namespace QueryPlus.App.Services;

public sealed class FileDialogService : IFileDialogService
{
    public string[]? OpenSqlFiles()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open SQL script(s)",
            Filter = "SQL scripts (*.sql)|*.sql|All files (*.*)|*.*",
            Multiselect = true
        };
        return dialog.ShowDialog() == true ? dialog.FileNames : null;
    }

    public string? PickFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Open all .sql files in a folder" };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? SaveCsvFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export results to CSV",
            Filter = "CSV (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"QueryPlusResults_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            AddExtension = true,
            DefaultExt = ".csv"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SaveExcelFile()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export results to Excel",
            Filter = "Excel workbook (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            FileName = $"QueryPlusResults_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx",
            AddExtension = true,
            DefaultExt = ".xlsx"
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
