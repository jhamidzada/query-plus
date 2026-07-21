using QueryPlus.App.ViewModels;

namespace QueryPlus.App.Services;

/// <summary>Picks .sql files / a folder to open as scripts.</summary>
public interface IFileDialogService
{
    string[]? OpenSqlFiles();

    /// <summary>Pick a folder (to bulk-open its .sql files). Null if cancelled.</summary>
    string? PickFolder();

    /// <summary>Pick a destination path for a CSV export. Null if cancelled.</summary>
    string? SaveCsvFile();

    /// <summary>Pick a destination path for an Excel (.xlsx) export. Null if cancelled.</summary>
    string? SaveExcelFile();
}

/// <summary>Shows the Manage Targets editor for the current configuration.</summary>
public interface IManageTargetsService
{
    void Show(MainViewModel main);
}
