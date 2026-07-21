using System.Diagnostics;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Execution;
using QueryPlus.Core.Results;
using QueryPlus.Core.Scripting;

namespace QueryPlus.Core.Engine;

/// <summary>
/// Orchestrates a run: fans out across targets in parallel, runs each target's scripts
/// sequentially over one connection, isolates errors per target/script, honors
/// cancellation, and reports progress. All database access goes through
/// <see cref="IBatchExecutor"/> so this class is fully testable without a live SQL Server.
/// </summary>
public sealed class ScriptRunner : IScriptRunner
{
    private readonly IBatchExecutor _executor;

    public ScriptRunner(IBatchExecutor executor)
    {
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public async Task<RunReport> RunAsync(
        DistributionList list,
        IReadOnlyList<ScriptItem> scripts,
        RunOptions options,
        IProgress<RunProgress>? progress,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(list);
        ArgumentNullException.ThrowIfNull(scripts);
        ArgumentNullException.ThrowIfNull(options);

        var targets = list.Targets;
        var total = targets.Count;
        var results = new TargetResult[total];
        var completed = 0;

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = Math.Max(1, options.MaxParallel),
            CancellationToken = ct
        };

        var indexed = targets.Select((target, index) => (target, index));

        // Manual stop signal for ScriptErrorPolicy.StopRun (checked, not thrown, so the run still
        // returns a partial report rather than surfacing a cancellation).
        using var stopRun = new CancellationTokenSource();

        await Parallel.ForEachAsync(indexed, parallelOptions, async (item, loopCt) =>
        {
            var result = await RunTargetAsync(
                list, item.target, scripts, options.ScriptProgress, options.ErrorPolicy, stopRun, loopCt).ConfigureAwait(false);
            results[item.index] = result;

            var done = Interlocked.Increment(ref completed);
            progress?.Report(new RunProgress
            {
                CompletedTargets = done,
                TotalTargets = total,
                CompletedTarget = result
            });
        }).ConfigureAwait(false);

        return new RunReport(results);
    }

    private async Task<TargetResult> RunTargetAsync(
        DistributionList list,
        Target target,
        IReadOnlyList<ScriptItem> scripts,
        IProgress<ScriptProgress>? scriptProgress,
        ScriptErrorPolicy errorPolicy,
        CancellationTokenSource stopRun,
        CancellationToken ct)
    {
        var result = new TargetResult(target.Server, target.Database);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Another target already triggered StopRun — skip this whole target.
            if (stopRun.IsCancellationRequested)
            {
                ReportSkipped(scripts, 0, scriptProgress);
                return result;
            }

            await using var connection = await _executor.OpenAsync(list, target, ct).ConfigureAwait(false);

            for (var i = 0; i < scripts.Count; i++)
            {
                ct.ThrowIfCancellationRequested();
                if (stopRun.IsCancellationRequested)
                {
                    ReportSkipped(scripts, i, scriptProgress);
                    break;
                }

                var stopTarget = await RunScriptAsync(
                    connection, scripts[i], result, scriptProgress, errorPolicy, stopRun, ct).ConfigureAwait(false);
                if (stopTarget)
                {
                    ReportSkipped(scripts, i + 1, scriptProgress); // skip the rest on this target
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Connection failure (or any non-script error) fails only this target; no script ran.
            result.Errors.Add(ex.Message);
            ReportSkipped(scripts, 0, scriptProgress);
        }
        finally
        {
            stopwatch.Stop();
            result.Elapsed = stopwatch.Elapsed;
        }

        return result;
    }

    private static void ReportSkipped(IReadOnlyList<ScriptItem> scripts, int startIndex, IProgress<ScriptProgress>? scriptProgress)
    {
        if (scriptProgress == null)
            return;
        for (var i = startIndex; i < scripts.Count; i++)
            scriptProgress.Report(new ScriptProgress(scripts[i].Name, ScriptOutcome.Skipped));
    }

    /// <summary>
    /// Runs one script's batches. A batch error is recorded but — like SSMS — the remaining
    /// batches still run when the policy is <see cref="ScriptErrorPolicy.Continue"/> (so a
    /// script that keeps going after a per-database error, then SELECTs the results in a later
    /// batch, still returns those results). Returns true if the target should stop.
    /// </summary>
    private static async Task<bool> RunScriptAsync(
        ITargetConnection connection,
        ScriptItem script,
        TargetResult result,
        IProgress<ScriptProgress>? scriptProgress,
        ScriptErrorPolicy errorPolicy,
        CancellationTokenSource stopRun,
        CancellationToken ct)
    {
        var batches = SqlBatchSplitter.Split(script.Text);
        var faulted = false;
        var stopTarget = false;

        for (var b = 0; b < batches.Count; b++)
        {
            var batch = batches[b];
            var label = $"batch {b + 1}/{batches.Count}";

            for (var run = 0; run < batch.Count; run++)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var batchResult = await connection.ExecuteBatchAsync(batch.Text, ct).ConfigureAwait(false);

                    foreach (var table in batchResult.Tables)
                        result.ResultSets.Add(new ResultSet(script.Name, table));

                    foreach (var message in batchResult.InfoMessages)
                        result.Messages.Add(message);

                    if (batchResult.Tables.Count == 0)
                        result.Messages.Add($"{label} completed");
                    else
                        result.Messages.Add($"{label} returned {batchResult.Tables.Count} result set(s)");
                }
                catch (OperationCanceledException)
                {
                    throw; // cancellation must abort, not be recorded as an error
                }
                catch (Exception ex)
                {
                    result.Errors.Add($"[{script.Name}] {label}: {ex.Message}");
                    faulted = true;
                    if (errorPolicy != ScriptErrorPolicy.Continue)
                    {
                        if (errorPolicy == ScriptErrorPolicy.StopRun)
                            stopRun.Cancel();
                        stopTarget = true;
                    }
                }

                if (stopTarget)
                    break;
            }

            if (stopTarget)
                break;
        }

        scriptProgress?.Report(new ScriptProgress(script.Name, faulted ? ScriptOutcome.Faulted : ScriptOutcome.Completed));
        return stopTarget;
    }
}
