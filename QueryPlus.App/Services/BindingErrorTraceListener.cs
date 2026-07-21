using System.Diagnostics;
using System.IO;
using System.Windows;

namespace QueryPlus.App.Services;

/// <summary>
/// Captures WPF data-binding failures (PresentationTraceSources.DataBindingSource at
/// SourceLevels.Error) into an in-memory list and a log file, so binding problems are
/// visible even though no one is watching the rendered window.
/// </summary>
public sealed class BindingErrorTraceListener : TraceListener
{
    private readonly object _gate = new();
    private readonly List<string> _errors = new();
    private readonly string? _logPath;
    private System.Text.StringBuilder _pending = new();

    private BindingErrorTraceListener(string? logPath) => _logPath = logPath;

    public IReadOnlyList<string> Errors
    {
        get { lock (_gate) return _errors.ToList(); }
    }

    /// <summary>Installs the listener and returns it. Call once at startup.</summary>
    public static BindingErrorTraceListener Install(string? logPath = null)
    {
        var listener = new BindingErrorTraceListener(logPath);
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Error;
        return listener;
    }

    public override void Write(string? message)
    {
        if (message != null) _pending.Append(message);
    }

    public override void WriteLine(string? message)
    {
        if (message != null) _pending.Append(message);
        var line = _pending.ToString();
        _pending = new System.Text.StringBuilder();
        if (string.IsNullOrWhiteSpace(line)) return;

        lock (_gate)
        {
            _errors.Add(line);
            if (_logPath != null)
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_logPath)!);
                    File.AppendAllText(_logPath, line + Environment.NewLine);
                }
                catch
                {
                    // Logging must never throw into the UI.
                }
            }
        }
    }
}
