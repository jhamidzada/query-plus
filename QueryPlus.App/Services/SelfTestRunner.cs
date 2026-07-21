using System.Data;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Results;

namespace QueryPlus.App.Services;

/// <summary>
/// A no-database <see cref="IScriptRunner"/> used only by <c>--selftest</c> to exercise the
/// execute → progress → results-grid path (each target returns a small result set).
/// </summary>
internal sealed class SelfTestRunner : IScriptRunner
{
    public async Task<RunReport> RunAsync(
        DistributionList list,
        IReadOnlyList<ScriptItem> scripts,
        RunOptions options,
        IProgress<RunProgress>? progress,
        CancellationToken ct)
    {
        var results = new List<TargetResult>();
        var total = list.Targets.Count;
        var done = 0;

        foreach (var t in list.Targets)
        {
            var tr = new TargetResult(t.Server, t.Database);
            foreach (var s in scripts)
            {
                var table = new DataTable();
                table.Columns.Add("Id", typeof(int));
                table.Columns.Add("Name", typeof(string));
                table.Rows.Add(1, "alpha");
                table.Rows.Add(2, DBNull.Value);
                tr.ResultSets.Add(new ResultSet(s.Name, table));
                options.ScriptProgress?.Report(new ScriptProgress(s.Name, ScriptOutcome.Completed));
            }
            tr.Messages.Add("self-test row");
            results.Add(tr);

            done++;
            progress?.Report(new RunProgress { CompletedTargets = done, TotalTargets = total, CompletedTarget = tr });
            await Task.Yield();
        }

        return new RunReport(results);
    }
}
