using FluentAssertions;
using QueryPlus.Core.Domain;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Execution;
using QueryPlus.Core.Results;
using QueryPlus.Core.Tests.Fakes;

namespace QueryPlus.Core.Tests;

public class ScriptRunnerTests
{
    private static IReadOnlyList<ScriptItem> Scripts(params (string name, string text)[] scripts) =>
        scripts.Select(s => new ScriptItem(s.name, s.text)).ToList();

    private static RunOptions Options(int parallel = 4) => new() { MaxParallel = parallel };

    /// <summary>A capturing IProgress that records synchronously (the runner reports inline).</summary>
    private sealed class CapturingProgress<T> : IProgress<T>
    {
        private readonly object _gate = new();
        public List<T> Reports { get; } = new();

        public void Report(T value)
        {
            lock (_gate)
                Reports.Add(value);
        }
    }

    [Fact]
    public async Task Captures_result_sets_in_order_with_script_name()
    {
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = (text, _) => Task.FromResult(
                TestData.Result(TestData.Table(new[] { "B" }, new object?[] { text })))
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "SELECT 1\nGO\nSELECT 2")),
            Options(), null, CancellationToken.None);

        var target = report.Targets.Single();
        target.ResultSets.Should().HaveCount(2);
        target.ResultSets.Should().OnlyContain(r => r.ScriptName == "a.sql");
        target.ResultSets[0].Table.Rows[0]["B"].Should().Be("SELECT 1");
        target.ResultSets[1].Table.Rows[0]["B"].Should().Be("SELECT 2");
        target.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Records_batch_completed_when_no_result_set()
    {
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "UPDATE t SET x = 1")),
            Options(), null, CancellationToken.None);

        report.Targets.Single().Messages.Should().ContainSingle().Which.Should().Be("batch 1/1 completed");
    }

    [Fact]
    public async Task Captures_info_messages()
    {
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = (_, _) => Task.FromResult(
                TestData.ResultWithMessages(new[] { "hello", "world" },
                    TestData.Table(new[] { "B" }, new object?[] { "1" })))
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "PRINT 'hello'")),
            Options(), null, CancellationToken.None);

        report.Targets.Single().Messages.Should().Equal("hello", "world", "batch 1/1 returned 1 result set(s)");
    }

    [Fact]
    public async Task Repeat_count_executes_batch_multiple_times()
    {
        FakeTargetConnection? captured = null;
        var executor = new FakeBatchExecutor(_ =>
        {
            captured = new FakeTargetConnection
            {
                OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
            };
            return captured;
        });
        var runner = new ScriptRunner(executor);

        await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "INSERT INTO t VALUES (1)\nGO 3")),
            Options(), null, CancellationToken.None);

        captured!.ExecutedBatches.Should().HaveCount(3);
        captured.ExecutedBatches.Should().OnlyContain(b => b == "INSERT INTO t VALUES (1)");
    }

    [Fact]
    public async Task Runs_scripts_sequentially_within_a_target()
    {
        FakeTargetConnection? captured = null;
        var executor = new FakeBatchExecutor(_ =>
        {
            captured = new FakeTargetConnection
            {
                OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
            };
            return captured;
        });
        var runner = new ScriptRunner(executor);

        await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("first.sql", "A"), ("second.sql", "B")),
            Options(), null, CancellationToken.None);

        captured!.ExecutedBatches.Should().Equal("A", "B");
    }

    [Fact]
    public async Task Disposes_connection_per_target()
    {
        FakeTargetConnection? captured = null;
        var executor = new FakeBatchExecutor(_ => captured = new FakeTargetConnection());
        var runner = new ScriptRunner(executor);

        await runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "SELECT 1")),
            Options(), null, CancellationToken.None);

        captured!.DisposeCount.Should().Be(1);
    }

    [Fact]
    public async Task Error_on_one_target_does_not_abort_others()
    {
        var executor = new FakeBatchExecutor(target =>
        {
            if (target.Server == "BadServer")
            {
                return new FakeTargetConnection
                {
                    OnExecute = (_, _) => throw new InvalidOperationException("boom")
                };
            }

            return new FakeTargetConnection
            {
                OnExecute = (_, _) => Task.FromResult(
                    TestData.Result(TestData.Table(new[] { "B" }, new object?[] { "ok" })))
            };
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(
                TestData.Target("GoodServer1", "DB1"),
                TestData.Target("BadServer", "DB2"),
                TestData.Target("GoodServer2", "DB3")),
            Scripts(("a.sql", "SELECT 1")),
            Options(), null, CancellationToken.None);

        report.Targets.Should().HaveCount(3);

        var bad = report.Targets.Single(t => t.Server == "BadServer");
        bad.Errors.Should().ContainSingle().Which.Should().Contain("boom");
        bad.ResultSets.Should().BeEmpty();

        var good = report.Targets.Where(t => t.Server != "BadServer").ToList();
        good.Should().OnlyContain(t => t.Errors.Count == 0);
        good.Should().OnlyContain(t => t.ResultSets.Count == 1);
    }

    [Fact]
    public async Task Connection_failure_fails_only_its_target()
    {
        var executor = new FakeBatchExecutor(target => new FakeTargetConnection
        {
            OpenFailure = target.Server == "BadServer"
                ? () => throw new TimeoutException("login failed")
                : null,
            OnExecute = (_, _) => Task.FromResult(
                TestData.Result(TestData.Table(new[] { "B" }, new object?[] { "ok" })))
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(
                TestData.Target("GoodServer", "DB1"),
                TestData.Target("BadServer", "DB2")),
            Scripts(("a.sql", "SELECT 1")),
            Options(), null, CancellationToken.None);

        report.Targets.Single(t => t.Server == "BadServer").Errors
            .Should().ContainSingle().Which.Should().Contain("login failed");
        report.Targets.Single(t => t.Server == "GoodServer").Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task Reports_progress_once_per_target_reaching_total()
    {
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
        });
        var runner = new ScriptRunner(executor);
        var progress = new CapturingProgress<RunProgress>();

        var targets = Enumerable.Range(1, 5)
            .Select(i => TestData.Target($"S{i}", "DB")).ToArray();

        await runner.RunAsync(
            TestData.List(targets),
            Scripts(("a.sql", "SELECT 1")),
            Options(parallel: 3), progress, CancellationToken.None);

        progress.Reports.Should().HaveCount(5);
        progress.Reports.Should().OnlyContain(r => r.TotalTargets == 5);
        progress.Reports.Select(r => r.CompletedTargets).OrderBy(x => x)
            .Should().Equal(1, 2, 3, 4, 5);
        progress.Reports.Should().OnlyContain(r => r.CompletedTarget != null);
    }

    [Fact]
    public async Task Reports_script_progress_once_per_script_per_target()
    {
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
        });
        var runner = new ScriptRunner(executor);
        var scriptProgress = new CapturingProgress<ScriptProgress>();

        await runner.RunAsync(
            TestData.List(
                TestData.Target("S1", "DB1"),
                TestData.Target("S2", "DB2"),
                TestData.Target("S3", "DB3")),
            Scripts(("a.sql", "A"), ("b.sql", "B")),
            new RunOptions { MaxParallel = 2, ScriptProgress = scriptProgress },
            null, CancellationToken.None);

        scriptProgress.Reports.Should().HaveCount(6); // 2 scripts x 3 targets
        scriptProgress.Reports.Count(r => r.ScriptName == "a.sql").Should().Be(3);
        scriptProgress.Reports.Count(r => r.ScriptName == "b.sql").Should().Be(3);
        scriptProgress.Reports.Should().OnlyContain(r => r.Outcome == ScriptOutcome.Completed);
    }

    [Fact]
    public async Task Reports_skipped_scripts_when_connection_fails()
    {
        var executor = new FakeBatchExecutor(target => new FakeTargetConnection
        {
            OpenFailure = target.Server == "Bad" ? () => throw new TimeoutException("nope") : null,
            OnExecute = (_, _) => Task.FromResult(new BatchExecutionResult())
        });
        var runner = new ScriptRunner(executor);
        var scriptProgress = new CapturingProgress<ScriptProgress>();

        await runner.RunAsync(
            TestData.List(TestData.Target("Bad", "DB1")),
            Scripts(("a.sql", "A"), ("b.sql", "B")),
            new RunOptions { MaxParallel = 1, ScriptProgress = scriptProgress },
            null, CancellationToken.None);

        // Connection never opened — each script is reported skipped so progress still completes.
        scriptProgress.Reports.Should().HaveCount(2);
        scriptProgress.Reports.Should().OnlyContain(r => r.Outcome == ScriptOutcome.Skipped);
    }

    [Fact]
    public async Task StopTarget_skips_remaining_scripts_on_the_failing_target_only()
    {
        FakeTargetConnection? badConnection = null;
        var executor = new FakeBatchExecutor(target =>
        {
            var connection = new FakeTargetConnection
            {
                OnExecute = (text, _) => target.Server == "Bad" && text == "B"
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(new BatchExecutionResult())
            };
            if (target.Server == "Bad")
                badConnection = connection;
            return connection;
        });
        var runner = new ScriptRunner(executor);
        var scriptProgress = new CapturingProgress<ScriptProgress>();

        var report = await runner.RunAsync(
            TestData.List(TestData.Target("Bad", "DB1"), TestData.Target("Good", "DB2")),
            Scripts(("a.sql", "A"), ("b.sql", "B"), ("c.sql", "C")),
            new RunOptions { MaxParallel = 2, ErrorPolicy = ScriptErrorPolicy.StopTarget, ScriptProgress = scriptProgress },
            null, CancellationToken.None);

        // On the failing target, c.sql never executed (B threw, so the target stopped).
        badConnection!.ExecutedBatches.Should().Equal("A", "B");
        scriptProgress.Reports.Count(r => r.ScriptName == "c.sql" && r.Outcome == ScriptOutcome.Skipped).Should().Be(1);
        scriptProgress.Reports.Count(r => r.ScriptName == "c.sql" && r.Outcome == ScriptOutcome.Completed).Should().Be(1);

        // The other target completed all three scripts and recorded no error.
        report.Targets.Single(t => t.Server == "Good").Errors.Should().BeEmpty();
        report.Targets.Single(t => t.Server == "Bad").Errors.Should().ContainSingle().Which.Should().Contain("boom");
    }

    [Fact]
    public async Task Continue_policy_runs_remaining_batches_after_a_batch_error()
    {
        FakeTargetConnection? captured = null;
        var executor = new FakeBatchExecutor(_ =>
        {
            captured = new FakeTargetConnection
            {
                OnExecute = (text, _) => text == "B"
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(TestData.Result(TestData.Table(new[] { "X" }, new object?[] { text })))
            };
            return captured;
        });
        var runner = new ScriptRunner(executor);

        var report = await runner.RunAsync(
            TestData.List(TestData.Target("S", "DB")),
            Scripts(("m.sql", "A\nGO\nB\nGO\nC")),
            new RunOptions { MaxParallel = 1, ErrorPolicy = ScriptErrorPolicy.Continue },
            null, CancellationToken.None);

        // B errored, but A and C still ran (SSMS-like continue past a batch error).
        captured!.ExecutedBatches.Should().Equal("A", "B", "C");
        var target = report.Targets.Single();
        target.Errors.Should().ContainSingle().Which.Should().Contain("boom");
        target.ResultSets.Select(r => (string)r.Table.Rows[0]["X"]).Should().BeEquivalentTo("A", "C");
    }

    [Fact]
    public async Task StopTarget_stops_remaining_batches_after_a_batch_error()
    {
        FakeTargetConnection? captured = null;
        var executor = new FakeBatchExecutor(_ =>
        {
            captured = new FakeTargetConnection
            {
                OnExecute = (text, _) => text == "B"
                    ? throw new InvalidOperationException("boom")
                    : Task.FromResult(new BatchExecutionResult())
            };
            return captured;
        });
        var runner = new ScriptRunner(executor);

        await runner.RunAsync(
            TestData.List(TestData.Target("S", "DB")),
            Scripts(("m.sql", "A\nGO\nB\nGO\nC")),
            new RunOptions { MaxParallel = 1, ErrorPolicy = ScriptErrorPolicy.StopTarget },
            null, CancellationToken.None);

        // B errored and stopped the target — C never ran.
        captured!.ExecutedBatches.Should().Equal("A", "B");
    }

    [Fact]
    public async Task Cancellation_stops_a_long_running_batch_promptly()
    {
        var started = new TaskCompletionSource();
        var executor = new FakeBatchExecutor(_ => new FakeTargetConnection
        {
            OnExecute = async (_, ct) =>
            {
                started.TrySetResult();
                await Task.Delay(Timeout.Infinite, ct);
                return new BatchExecutionResult();
            }
        });
        var runner = new ScriptRunner(executor);
        using var cts = new CancellationTokenSource();

        var run = runner.RunAsync(
            TestData.List(TestData.Target("S1", "DB1")),
            Scripts(("a.sql", "WAITFOR DELAY '01:00:00'")),
            Options(parallel: 1), null, cts.Token);

        await started.Task;
        cts.Cancel();

        var act = async () => await run;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
