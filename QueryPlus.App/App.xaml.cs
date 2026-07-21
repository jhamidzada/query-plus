using System.IO;
using System.Windows;
using System.Windows.Threading;
using QueryPlus.App.Services;
using QueryPlus.App.ViewModels;
using QueryPlus.App.Views;
using QueryPlus.Core.Engine;
using QueryPlus.Core.Execution;

namespace QueryPlus.App;

public partial class App : Application
{
    private BindingErrorTraceListener? _bindingErrors;
    private SqlDatabaseEnumerator? _databaseEnumerator;

    private static string DataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "QueryPlus");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var bindingLog = Path.Combine(DataDir, "binding-errors.log");
        TryDelete(bindingLog);
        _bindingErrors = BindingErrorTraceListener.Install(bindingLog);

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            LogCrash(args.ExceptionObject as Exception, "AppDomain");

        var selfTest = e.Args.Contains("--selftest");
        var configStore = new ConfigStore();
        IScriptRunner runner = selfTest ? new SelfTestRunner() : new ScriptRunner(new SqlBatchExecutor());
        _databaseEnumerator = new SqlDatabaseEnumerator();
        var viewModel = new MainViewModel(
            runner, configStore, new FileDialogService(), new ManageTargetsService(_databaseEnumerator));

        var window = new MainWindow { DataContext = viewModel };
        MainWindow = window;
        window.Show();

        viewModel.Load();

        if (selfTest)
            RunSelfTest(window, viewModel);
    }

    /// <summary>
    /// Writes a report to .xlsx and re-reads it to confirm a valid workbook plus correct type
    /// inference: numbers stay numeric, dates get a date style, and IDs/text stay strings.
    /// </summary>
    private static bool VerifyExcelExport()
    {
        var path = Path.Combine(Path.GetTempPath(), "msp-selftest-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            // Columns after Server/Database/Script are: Name(D), Amount(E), When(F), Code(G).
            var table = new System.Data.DataTable();
            table.Columns.Add("Name", typeof(string));
            table.Columns.Add("Amount", typeof(string));
            table.Columns.Add("When", typeof(string));
            table.Columns.Add("Code", typeof(string));
            table.Rows.Add("alpha", "123.45", "11/21/2023 00:00:00", "007");
            var target = new QueryPlus.Core.Results.TargetResult("S1", "DB1");
            target.ResultSets.Add(new QueryPlus.Core.Results.ResultSet("a.sql", table));
            var report = new QueryPlus.Core.Results.RunReport(new[] { target });

            QueryPlus.App.Services.ExcelExporter.Write(report, path);

            using var doc = DocumentFormat.OpenXml.Packaging.SpreadsheetDocument.Open(path, false);
            var wbPart = doc.WorkbookPart!;
            var sheet = wbPart.Workbook.Descendants<DocumentFormat.OpenXml.Spreadsheet.Sheet>()
                .FirstOrDefault(s => s.Name == "Results");
            if (sheet?.Id?.Value == null)
                return false;
            var wsPart = (DocumentFormat.OpenXml.Packaging.WorksheetPart)wbPart.GetPartById(sheet.Id!.Value!);
            var cells = wsPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Cell>().ToList();

            DocumentFormat.OpenXml.Spreadsheet.Cell? Cell(string reference) =>
                cells.FirstOrDefault(c => c.CellReference?.Value == reference);

            var rowCount = wsPart.Worksheet.Descendants<DocumentFormat.OpenXml.Spreadsheet.Row>().Count();
            var amount = Cell("E2");   // "123.45" → numeric (no InlineString type), value preserved
            var when = Cell("F2");     // date → carries a style index
            var code = Cell("G2");     // "007" → text (leading zero)

            var amountNumeric = amount != null
                && amount.DataType?.Value != DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString
                && amount.CellValue?.Text == "123.45";
            var whenDate = when?.StyleIndex?.Value is 1 or 2;
            var codeText = code?.DataType?.Value == DocumentFormat.OpenXml.Spreadsheet.CellValues.InlineString;

            return rowCount == 2 && amountNumeric && whenDate && codeText;
        }
        catch
        {
            return false;
        }
        finally
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }
    }

    /// <summary>
    /// Round-trips a list with a password through an isolated ConfigStore to confirm the password
    /// survives save/load (DPAPI) and that the on-disk JSON never contains the plaintext.
    /// </summary>
    private static (bool roundTrip, bool noPlaintext) VerifyPasswordPersistence()
    {
        const string secret = "selftest-secret-Pw!9";
        var tempDir = Path.Combine(Path.GetTempPath(), "msp-selftest-" + Guid.NewGuid().ToString("N"));
        try
        {
            var store = new ConfigStore(tempDir);
            var list = new QueryPlus.App.ViewModels.DistributionListViewModel
            {
                Name = "rt", Auth = QueryPlus.Core.Domain.AuthMode.Sql, SqlUser = "u",
                SqlPassword = secret, RememberPassword = true
            };
            store.Save(new QueryPlus.App.Models.AppConfig { Lists = { list.ToConfig() } });

            var json = File.ReadAllText(store.ConfigPath);
            var reloaded = QueryPlus.App.ViewModels.DistributionListViewModel.FromConfig(store.Load().Lists[0]);

            return (reloaded.SqlPassword == secret, !json.Contains(secret));
        }
        catch
        {
            return (false, false);
        }
        finally
        {
            try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { /* ignore */ }
        }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogCrash(e.Exception, "Dispatcher");
        MessageBox.Show(e.Exception.Message, "QueryPlus error", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void LogCrash(Exception? ex, string source)
    {
        if (ex == null) return;
        try
        {
            Directory.CreateDirectory(DataDir);
            File.AppendAllText(Path.Combine(DataDir, "crash.log"),
                $"[{DateTime.Now:O}] ({source}) {ex}{Environment.NewLine}");
        }
        catch
        {
            // never throw from the crash logger
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { /* ignore */ }
    }

    /// <summary>
    /// Headless verification: load sample data, also open the Manage Targets window so its
    /// bindings evaluate, let everything render, then write the captured binding errors to a
    /// result file and exit (0 = no binding errors, 2 = binding errors found).
    /// </summary>
    private void RunSelfTest(MainWindow window, MainViewModel viewModel)
    {
        var (passwordRoundTrip, noPlaintext) = VerifyPasswordPersistence();
        var excelOk = VerifyExcelExport();

        if (viewModel.Lists.Count == 0)
        {
            var sample = new DistributionListViewModel { Name = "Sample (self-test)" };
            viewModel.Lists.Add(sample);
            viewModel.SelectedList = sample;
        }

        // Ensure the selected list has targets so the run produces results.
        var listVm = viewModel.SelectedList!;
        if (listVm.Targets.Count == 0)
        {
            listVm.Targets.Add(new TargetViewModel("localhost", "master"));
            listVm.Targets.Add(new TargetViewModel("server2", "AppDb"));
        }

        var manage = new ManageTargetsWindow
        {
            DataContext = new ManageTargetsViewModel(viewModel, _databaseEnumerator!),
            Owner = window
        };
        manage.Show();

        window.ContentRendered += async (_, _) =>
        {
            // Drive a real execute through the fake runner: exercises the results-grid path,
            // proving the merged result columns actually surface (the "no results" fix).
            try
            {
                await viewModel.ExecuteCommand.ExecuteAsync(null);
            }
            catch
            {
                // captured below
            }

            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1500) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var errors = _bindingErrors?.Errors ?? Array.Empty<string>();
                var resultColumns = viewModel.ResultsView?.Table?.Columns.Count ?? 0;
                var resultRows = viewModel.ResultsView?.Count ?? 0;
                try
                {
                    Directory.CreateDirectory(DataDir);
                    File.WriteAllText(Path.Combine(DataDir, "selftest-result.txt"),
                        $"BindingErrors={errors.Count}{Environment.NewLine}" +
                        $"ResultColumns={resultColumns}{Environment.NewLine}" +
                        $"ResultRows={resultRows}{Environment.NewLine}" +
                        $"PasswordRoundTrip={passwordRoundTrip}{Environment.NewLine}" +
                        $"NoPlaintext={noPlaintext}{Environment.NewLine}" +
                        $"ExcelExport={excelOk}{Environment.NewLine}" +
                        string.Join(Environment.NewLine, errors));
                }
                catch
                {
                    // ignore
                }

                manage.Close();
                var ok = errors.Count == 0 && resultColumns > 3 && resultRows > 0 && passwordRoundTrip && noPlaintext && excelOk;
                Shutdown(ok ? 0 : 2);
            };
            timer.Start();
        };
    }
}
