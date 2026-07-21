using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using QueryPlus.App.Models;
using QueryPlus.App.Services;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Results;

namespace QueryPlus.App.ViewModels;

/// <summary>Root view model: lists, targets, script tabs, run control and results.</summary>
public sealed partial class MainViewModel : ObservableObject
{
    private readonly IScriptRunner _runner;
    private readonly ConfigStore _configStore;
    private readonly IFileDialogService _fileDialog;
    private readonly IManageTargetsService _manageTargets;

    /// <summary>Cap on rows shown in the grid so it stays responsive; the full set is exported to CSV.</summary>
    private const int MaxGridRows = 100_000;

    private CancellationTokenSource? _cts;
    private DataTable _merged = ResultMerger.CreateMergedTable();
    private int _shownColumnCount;
    private long _totalResultRows;
    private RunReport? _lastReport;

    public MainViewModel(
        IScriptRunner runner,
        ConfigStore configStore,
        IFileDialogService fileDialog,
        IManageTargetsService manageTargets)
    {
        _runner = runner;
        _configStore = configStore;
        _fileDialog = fileDialog;
        _manageTargets = manageTargets;
        ResultsView = _merged.DefaultView;
    }

    public ObservableCollection<DistributionListViewModel> Lists { get; } = new();

    public ObservableCollection<ScriptTabViewModel> ScriptTabs { get; } = new();

    public ObservableCollection<string> Messages { get; } = new();

    public ObservableCollection<string> Errors { get; } = new();

    public const string AllScripts = "(all scripts)";

    /// <summary>Script-name filter for the Results grid.</summary>
    public ObservableCollection<string> ScriptFilters { get; } = new() { AllScripts };

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCommand))]
    private DistributionListViewModel? _selectedList;

    [ObservableProperty]
    private ScriptTabViewModel? _selectedTab;

    [ObservableProperty]
    private string _scriptFilter = AllScripts;

    [ObservableProperty]
    private int _maxParallel = 8;

    /// <summary>What to do when a script errors on a database.</summary>
    [ObservableProperty]
    private ScriptErrorPolicy _errorPolicy = ScriptErrorPolicy.StopTarget;

    /// <summary>Bottom tab index: 0 = Results, 1 = Messages, 2 = Errors.</summary>
    [ObservableProperty]
    private int _selectedResultTab;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExecuteCommand))]
    [NotifyCanExecuteChangedFor(nameof(StopCommand))]
    private bool _isRunning;

    [ObservableProperty]
    private DataView? _resultsView;

    [ObservableProperty]
    private int _completedTargets;

    [ObservableProperty]
    private int _totalTargets;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _elapsedText = "00:00:00";

    [ObservableProperty]
    private string _statusText = "Ready";

    /// <summary>Non-empty when the grid is showing only the first MaxGridRows of a larger result set.</summary>
    [ObservableProperty]
    private string _resultsTruncatedText = string.Empty;

    private const string SampleScript =
        "-- Runs against every target in the selected distribution list.\r\n" +
        "SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DbName, @@VERSION AS Version;";

    /// <summary>Loads persisted config and ensures there is at least one list and script tab.</summary>
    public void Load()
    {
        var config = _configStore.Load();
        Lists.Clear();
        foreach (var listConfig in config.Lists)
            Lists.Add(DistributionListViewModel.FromConfig(listConfig));

        // First run: seed a starter list so the app is usable immediately (matches the oracle).
        if (Lists.Count == 0)
        {
            var sample = new DistributionListViewModel { Name = "Sample (edit me)" };
            sample.Targets.Add(new TargetViewModel("localhost", "master"));
            Lists.Add(sample);
        }

        SelectedList = Lists.FirstOrDefault();

        if (ScriptTabs.Count == 0)
        {
            ScriptTabs.Add(new ScriptTabViewModel { Title = "Script1.sql", Text = SampleScript });
        }
        SelectedTab = ScriptTabs.FirstOrDefault();
    }

    public void SaveConfig()
    {
        var config = new AppConfig { Lists = Lists.Select(l => l.ToConfig()).ToList() };
        _configStore.Save(config);
    }

    [RelayCommand]
    private void NewTab()
    {
        var tab = new ScriptTabViewModel { Title = $"Script{ScriptTabs.Count + 1}.sql" };
        ScriptTabs.Add(tab);
        SelectedTab = tab;
    }

    /// <summary>Removes all highlighted scripts (or the current one if none are highlighted).</summary>
    [RelayCommand]
    private void RemoveSelectedScripts()
    {
        var toRemove = ScriptTabs.Where(s => s.IsSelected).ToList();
        if (toRemove.Count == 0 && SelectedTab != null)
            toRemove.Add(SelectedTab);
        foreach (var script in toRemove)
            ScriptTabs.Remove(script);
    }

    [RelayCommand]
    private void OpenFile()
    {
        var paths = _fileDialog.OpenSqlFiles();
        if (paths == null)
            return;

        foreach (var path in paths)
        {
            try
            {
                var tab = new ScriptTabViewModel
                {
                    Title = Path.GetFileName(path),
                    Text = File.ReadAllText(path),
                    FilePath = path
                };
                ScriptTabs.Add(tab);
                SelectedTab = tab;
            }
            catch (Exception ex)
            {
                Errors.Add($"Could not open '{path}': {ex.Message}");
            }
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        var folder = _fileDialog.PickFolder();
        if (folder == null)
            return;

        try
        {
            ScriptTabViewModel? last = null;
            foreach (var path in Directory.EnumerateFiles(folder, "*.sql").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var tab = new ScriptTabViewModel
                {
                    Title = Path.GetFileName(path),
                    Text = File.ReadAllText(path),
                    FilePath = path
                };
                ScriptTabs.Add(tab);
                last = tab;
            }

            if (last != null)
                SelectedTab = last;
            else
                Errors.Add($"No .sql files found in '{folder}'.");
        }
        catch (Exception ex)
        {
            Errors.Add($"Could not open folder '{folder}': {ex.Message}");
        }
    }

    [RelayCommand]
    private void CheckAll()
    {
        foreach (var script in ScriptTabs)
            script.IncludeInRun = true;
    }

    [RelayCommand]
    private void UncheckAll()
    {
        foreach (var script in ScriptTabs)
            script.IncludeInRun = false;
    }

    [RelayCommand]
    private void CheckAllTargets() => SetAllTargets(true);

    [RelayCommand]
    private void UncheckAllTargets() => SetAllTargets(false);

    private void SetAllTargets(bool include)
    {
        if (SelectedList == null)
            return;
        foreach (var target in SelectedList.Targets)
            target.IncludeInRun = include;
    }

    [RelayCommand]
    private void ManageTargets()
    {
        _manageTargets.Show(this);
        SelectedList?.RebuildGroups();
        SaveConfig();
    }

    private bool CanExecute() => !IsRunning && SelectedList != null;

    [RelayCommand(CanExecute = nameof(CanExecute))]
    private async Task ExecuteAsync()
    {
        var listVm = SelectedList;
        if (listVm == null)
            return;

        var included = ScriptTabs.Where(t => t.IncludeInRun).ToList();
        if (included.Count == 0)
        {
            StatusText = "No scripts checked to run.";
            return;
        }

        var list = listVm.ToRuntime(); // only checked targets
        if (list.Targets.Count == 0)
        {
            StatusText = "No targets checked to run.";
            return;
        }

        Messages.Clear();
        Errors.Clear();
        _merged = ResultMerger.CreateMergedTable();
        ResultsView = _merged.DefaultView;
        _shownColumnCount = _merged.Columns.Count;
        _totalResultRows = 0;
        _lastReport = null;
        ResultsTruncatedText = string.Empty;
        ExportCsvCommand.NotifyCanExecuteChanged();
        ExportExcelCommand.NotifyCanExecuteChanged();

        // Reset per-script run state and rebuild the Results script filter.
        var totalTargets = list.Targets.Count;
        foreach (var script in ScriptTabs)
        {
            script.ResultRowCount = 0;
            script.HasErrors = false;
            script.CompletedTargetCount = 0;
            script.RunTargetCount = script.IncludeInRun ? totalTargets : 0;
        }
        ScriptFilters.Clear();
        ScriptFilters.Add(AllScripts);
        foreach (var name in included.Select(s => s.Title).Distinct())
            ScriptFilters.Add(name);
        ScriptFilter = AllScripts;

        var scripts = included.Select(t => new ScriptItem(t.Title, t.Text)).ToList();

        _cts = new CancellationTokenSource();
        IsRunning = true;
        CompletedTargets = 0;
        TotalTargets = list.Targets.Count;
        ProgressValue = 0;
        ElapsedText = "00:00:00";
        StatusText = "Running…";

        var stopwatch = Stopwatch.StartNew();
        var progress = new Progress<RunProgress>(OnProgress);
        var options = new RunOptions
        {
            MaxParallel = MaxParallel,
            ErrorPolicy = ErrorPolicy,
            ScriptProgress = new Progress<ScriptProgress>(OnScriptProgress)
        };
        try
        {
            var report = await _runner.RunAsync(list, scripts, options, progress, _cts.Token);
            _lastReport = report;
            StatusText = $"Completed {report.Targets.Count} target(s) — {_totalResultRows:N0} row(s), {Errors.Count} error(s)";
            ResultsTruncatedText = _totalResultRows > _merged.Rows.Count
                ? $"Showing the first {_merged.Rows.Count:N0} of {_totalResultRows:N0} rows. Use “Export CSV” for the full set."
                : string.Empty;
            ExportCsvCommand.NotifyCanExecuteChanged();
            ExportExcelCommand.NotifyCanExecuteChanged();
            // Show Results whenever there is any result data; only jump to Errors when there is
            // nothing to show but there were errors.
            SelectedResultTab = _totalResultRows == 0 && Errors.Count > 0 ? 2 : 0;
        }
        catch (OperationCanceledException)
        {
            StatusText = "Stopped";
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            StatusText = "Failed";
        }
        finally
        {
            stopwatch.Stop();
            ElapsedText = FormatElapsed(stopwatch.Elapsed);
            IsRunning = false;
            _cts.Dispose();
            _cts = null;
        }
    }

    private bool CanStop() => IsRunning;

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop()
    {
        _cts?.Cancel();
        StatusText = "Stopping…";
    }

    private bool CanExportCsv() => _lastReport != null && _totalResultRows > 0;

    /// <summary>Streams the full result set (all rows, beyond the grid cap) to a CSV file.</summary>
    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportCsvAsync()
    {
        var report = _lastReport;
        if (report == null)
            return;
        var path = _fileDialog.SaveCsvFile();
        if (path == null)
            return;

        StatusText = $"Exporting {_totalResultRows:N0} row(s)…";
        try
        {
            await Task.Run(() =>
            {
                using var writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(true));
                ResultMerger.WriteCsv(report, writer);
            });
            StatusText = $"Exported {_totalResultRows:N0} row(s) to {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Errors.Add($"Export failed: {ex.Message}");
            StatusText = "Export failed";
        }
    }

    /// <summary>Streams the full result set to an .xlsx workbook (row-by-row, off the UI thread).</summary>
    [RelayCommand(CanExecute = nameof(CanExportCsv))]
    private async Task ExportExcelAsync()
    {
        var report = _lastReport;
        if (report == null)
            return;
        var path = _fileDialog.SaveExcelFile();
        if (path == null)
            return;

        StatusText = $"Exporting {_totalResultRows:N0} row(s) to Excel…";
        try
        {
            var truncated = await Task.Run(() => ExcelExporter.Write(report, path));
            StatusText = truncated
                ? $"Exported to {Path.GetFileName(path)} (truncated to Excel's 1,048,575-row limit)."
                : $"Exported {_totalResultRows:N0} row(s) to {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            Errors.Add($"Excel export failed: {ex.Message}");
            StatusText = "Excel export failed";
        }
    }

    private void OnProgress(RunProgress progress)
    {
        // Runs on the UI thread (Progress<T> captures the synchronization context).
        CompletedTargets = progress.CompletedTargets;
        TotalTargets = progress.TotalTargets;
        ProgressValue = progress.TotalTargets == 0
            ? 0
            : (double)progress.CompletedTargets / progress.TotalTargets * 100.0;
        StatusText = $"{CompletedTargets}/{TotalTargets} targets";

        if (progress.CompletedTarget is { } target)
        {
            _totalResultRows += target.ResultSets.Sum(r => (long)r.Table.Rows.Count);

            // Append to the grid, capped for responsiveness. BeginLoadData suppresses per-row
            // change notifications so the bound grid doesn't churn on bulk inserts.
            if (_merged.Rows.Count < MaxGridRows)
            {
                _merged.BeginLoadData();
                try
                {
                    ResultMerger.AppendTarget(_merged, target, MaxGridRows);
                }
                finally
                {
                    _merged.EndLoadData();
                }
                RefreshResultsViewIfSchemaGrew();
            }

            foreach (var rs in target.ResultSets)
            {
                var script = ScriptTabs.FirstOrDefault(s => s.Title == rs.ScriptName);
                if (script != null)
                    script.ResultRowCount += rs.Table.Rows.Count;
            }
            foreach (var message in target.Messages)
                Messages.Add($"[{target.Server}\\{target.Database}] {message}");
            foreach (var error in target.Errors)
                Errors.Add($"[{target.Server}\\{target.Database}] {error}");
        }
    }

    private void OnScriptProgress(ScriptProgress sp)
    {
        // UI thread (Progress<T> captures the sync context). Advances that script's progress bar.
        var script = ScriptTabs.FirstOrDefault(s => s.Title == sp.ScriptName);
        if (script == null)
            return;
        script.CompletedTargetCount++;
        if (sp.Outcome == ScriptOutcome.Faulted)
            script.HasErrors = true;
    }

    partial void OnScriptFilterChanged(string value) => ApplyScriptFilter();

    private void ApplyScriptFilter()
    {
        if (ResultsView == null)
            return;
        ResultsView.RowFilter = string.IsNullOrEmpty(ScriptFilter) || ScriptFilter == AllScripts
            ? string.Empty
            : $"[Script] = '{ScriptFilter.Replace("'", "''")}'";
    }

    /// <summary>
    /// The WPF DataGrid auto-generates columns only when its source is (re)assigned — it does
    /// not pick up columns added to the bound DataTable afterward. As result sets with new
    /// columns arrive, re-assign a fresh DataView so the new columns actually render. Rows keep
    /// streaming live because a DataView reflects the table's rows without re-assignment.
    /// </summary>
    private void RefreshResultsViewIfSchemaGrew()
    {
        if (_merged.Columns.Count == _shownColumnCount)
            return;
        _shownColumnCount = _merged.Columns.Count;
        ResultsView = new DataView(_merged);
        ApplyScriptFilter(); // re-assigning the view drops the row filter; restore it
    }

    private static string FormatElapsed(TimeSpan elapsed) =>
        elapsed.ToString(@"hh\:mm\:ss\.fff");

    /// <summary>Populates the grid with sample data (design-time / self-test aid; no DB).</summary>
    public void LoadSampleResults()
    {
        var target = new TargetResult("SAMPLE\\INSTANCE", "DemoDb");
        var table = new DataTable();
        table.Columns.Add("Id", typeof(int));
        table.Columns.Add("Name", typeof(string));
        table.Rows.Add(1, "alpha");
        table.Rows.Add(2, DBNull.Value);
        target.ResultSets.Add(new ResultSet("demo.sql", table));
        target.Messages.Add("rows returned: 2");

        _merged = ResultMerger.CreateMergedTable();
        ResultMerger.AppendTarget(_merged, target);
        ResultsView = _merged.DefaultView;
        Messages.Add("[SAMPLE\\INSTANCE\\DemoDb] rows returned: 2");
        TotalTargets = 1;
        CompletedTargets = 1;
        ProgressValue = 100;
        StatusText = "Sample data";
    }
}
