using CommunityToolkit.Mvvm.ComponentModel;

namespace QueryPlus.App.ViewModels;

/// <summary>One script in the Scripts list. Checked scripts run, in list order.</summary>
public sealed partial class ScriptTabViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "Untitled.sql";

    [ObservableProperty]
    private string _text = string.Empty;

    /// <summary>Source file path if opened from a .sql file; null otherwise.</summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>Whether this script is included in the next run.</summary>
    [ObservableProperty]
    private bool _includeInRun = true;

    /// <summary>Transient multi-selection state in the Scripts list (for bulk remove).</summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Rows this script produced across all targets in the last run.</summary>
    [ObservableProperty]
    private int _resultRowCount;

    /// <summary>True if this script errored on any target in the last run.</summary>
    [ObservableProperty]
    private bool _hasErrors;

    /// <summary>Targets this script has finished on so far (this run).</summary>
    [ObservableProperty]
    private int _completedTargetCount;

    /// <summary>Total targets this script is running against (this run); the progress bar's max.</summary>
    [ObservableProperty]
    private int _runTargetCount;

    /// <summary>0–100 progress for this script in the current run.</summary>
    public double ScriptProgressValue => RunTargetCount == 0 ? 0 : 100.0 * CompletedTargetCount / RunTargetCount;

    partial void OnCompletedTargetCountChanged(int value) => OnPropertyChanged(nameof(ScriptProgressValue));

    partial void OnRunTargetCountChanged(int value) => OnPropertyChanged(nameof(ScriptProgressValue));
}
