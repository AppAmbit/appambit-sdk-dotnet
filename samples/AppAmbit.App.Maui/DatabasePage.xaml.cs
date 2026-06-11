using AppAmbit;
using AppAmbit.Models.Db;
using AppAmbitTestingApp.Models;

namespace AppAmbitTestingApp;

public partial class DatabasePage : ContentPage
{
    private record DemoItem(string Label, Func<Task> Action);

    private List<DemoItem> _demos = new();

    public DatabasePage()
    {
        InitializeComponent();
        BuildDemos();
        FunctionPicker.ItemsSource = _demos.Select(d => d.Label).ToList();
        FunctionPicker.SelectedIndex = 0;
    }

    private void BuildDemos()
    {
        _demos = new List<DemoItem>
        {
            // Raw SQL
            new("Raw SQL → execute(sql)",                        RunExecute),
            new("Raw SQL → execute(sql, params)",                RunExecuteParams),
            // Batch
            new("Batch → batch()",                               RunBatch),
            new("Batch → batchInTransaction()",                  RunBatchInTransaction),
            // Fluent Builder — SELECT
            new("Fluent SELECT → select+where+orderByDesc+limit", RunFluentSelect),
            new("Fluent SELECT → where(col,val)",                RunWhereEquality),
            new("Fluent SELECT → whereIn()",                     RunWhereIn),
            new("Fluent SELECT → limit+offset",                  RunOffset),
            new("Fluent SELECT → first()",                       RunFirst),
            new("Fluent SELECT → count()",                       RunCount),
            // Fluent Builder — WRITE
            new("Fluent WRITE → insert()",                       RunInsert),
            new("Fluent WRITE → update()",                       RunUpdate),
            new("Fluent WRITE → delete()",                       RunDelete),
            // Typed Model Mapping
            new("Typed Model → from(tasks, TaskModel.class)",    RunTypedModel),
            // Presets
            new("Preset → List tables",                          RunPresetTables),
            new("Preset → SELECT * WHERE priority='high'",       RunPresetHighPriority),
        };
    }

    private async void OnRun(object sender, EventArgs e)
    {
        if (FunctionPicker.SelectedIndex < 0) return;
        var demo = _demos[FunctionPicker.SelectedIndex];
        SetLoading(true);
        ShowStatus($"Running: {demo.Label}", false);
        try
        {
            await demo.Action();
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", true);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private void SetLoading(bool loading)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            LoadingIndicator.IsVisible = loading;
            LoadingIndicator.IsRunning = loading;
        });
    }

    // ── Raw Execute ───────────────────────────────────────────────────────────

    private async Task RunExecute()
    {
        var sql = EditSql.Text?.Trim();
        if (string.IsNullOrWhiteSpace(sql))
        {
            sql = "SELECT * FROM tasks LIMIT 10";
            EditSql.Text = sql;
        }
        var result = await AppAmbitDb.Execute(sql);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"execute(sql) — rows_read={result.RowsRead}  rows_written={result.RowsWritten}", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    private async Task RunExecuteParams()
    {
        var result = await AppAmbitDb.Execute(
            "SELECT * FROM tasks WHERE is_completed = ? LIMIT ?", 0, 10);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"execute(sql, 0, 10) — rows_read={result.RowsRead}", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    // ── Batch ─────────────────────────────────────────────────────────────────

    private async Task RunBatch()
    {
        var results = await AppAmbitDb.Batch(
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Buy coffee", 0, "low", "2026-06-10"),
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Review PR", 0, "high", "2026-06-05"),
            DbStatement.Of("SELECT COUNT(*) AS total FROM tasks"));

        int written = results.Sum(r => r.RowsWritten);
        var cols = new List<string> { "statement", "rows_written", "rows_read" };
        var maps = results.Select((r, i) => new Dictionary<string, object?>
        {
            { "statement", i + 1 },
            { "rows_written", r.RowsWritten },
            { "rows_read", r.RowsRead }
        }).ToList();
        ShowStatus($"batch() — {written} row(s) written, {results.Count} statements, no transaction", false);
        ShowRows(cols, maps);
    }

    private async Task RunBatchInTransaction()
    {
        var results = await AppAmbitDb.BatchInTransaction(
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Team meeting", 0, "high", "2026-06-06"),
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Prepare agenda", 0, "medium", "2026-06-06"));

        int written = results.Sum(r => r.RowsWritten);
        ShowStatus($"batchInTransaction() — {written} row(s) written, rolled back on any failure", false);
        var cols = new List<string> { "statement", "rows_written" };
        var maps = results.Select((r, i) => new Dictionary<string, object?>
        {
            { "statement", i + 1 },
            { "rows_written", r.RowsWritten }
        }).ToList();
        ShowRows(cols, maps);
    }

    // ── Fluent SELECT ─────────────────────────────────────────────────────────

    private async Task RunFluentSelect()
    {
        var maps = await AppAmbitDb.From("tasks")
            .Select("id", "title", "priority", "due_date")
            .Where("is_completed", "=", 0)
            .OrderByDesc("due_date")
            .Limit(5)
            .Get();

        ShowStatus(maps.Count == 0
            ? "No pending tasks"
            : $"from().select().where().orderByDesc().limit(5) — {maps.Count} row(s)", false);
        if (maps.Count > 0) ShowRows(maps[0].Keys.ToList(), maps);
    }

    private async Task RunWhereEquality()
    {
        var maps = await AppAmbitDb.From("tasks").Where("is_completed", 0).Get();
        ShowStatus(maps.Count == 0
            ? "No pending tasks"
            : $"where(is_completed, 0) — {maps.Count} row(s)", false);
        if (maps.Count > 0) ShowRows(maps[0].Keys.ToList(), maps);
        else ShowRows(new List<string>(), new List<Dictionary<string, object?>>());
    }

    private async Task RunWhereIn()
    {
        var maps = await AppAmbitDb.From("tasks")
            .WhereIn("priority", new object?[] { "high", "medium" })
            .OrderBy("due_date")
            .Get();
        ShowStatus(maps.Count == 0
            ? "No high/medium tasks"
            : $"whereIn(priority, [high,medium]) — {maps.Count} row(s)", false);
        if (maps.Count > 0) ShowRows(maps[0].Keys.ToList(), maps);
        else ShowRows(new List<string>(), new List<Dictionary<string, object?>>());
    }

    private async Task RunOffset()
    {
        var maps = await AppAmbitDb.From("tasks")
            .OrderBy("due_date")
            .Limit(5)
            .Offset(0)
            .Get();
        ShowStatus(maps.Count == 0
            ? "No tasks"
            : $"limit(5).offset(0) — page 1, {maps.Count} row(s)", false);
        if (maps.Count > 0) ShowRows(maps[0].Keys.ToList(), maps);
        else ShowRows(new List<string>(), new List<Dictionary<string, object?>>());
    }

    private async Task RunFirst()
    {
        var item = await AppAmbitDb.From("tasks")
            .Where("is_completed", "=", 0)
            .OrderBy("due_date")
            .First();
        if (item == null) { ShowStatus("first() — no pending tasks", false); ShowRows(new(), new()); return; }
        ShowStatus("first() — next task to expire", false);
        ShowRows(item.Keys.ToList(), new List<Dictionary<string, object?>> { item });
    }

    private async Task RunCount()
    {
        var count = await AppAmbitDb.From("tasks").Where("is_completed", 0).Count();
        var row = new Dictionary<string, object?> { { "pending_tasks", count } };
        ShowStatus($"count() — {count} pending task(s)", false);
        ShowRows(new List<string> { "pending_tasks" }, new List<Dictionary<string, object?>> { row });
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    private async Task RunInsert()
    {
        var result = await AppAmbitDb.From("tasks").Insert(new Dictionary<string, object?>
        {
            { "title", "New task" },
            { "is_completed", 0 },
            { "priority", "medium" },
            { "due_date", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd") }
        });
        ShowStatus($"insert() — rows_written={result.RowsWritten}", false);
        ShowRows(new List<string> { "rows_written" },
            new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    private async Task RunUpdate()
    {
        var result = await AppAmbitDb.From("tasks")
            .Where("title", "New task")
            .Update(new Dictionary<string, object?> { { "is_completed", 1 } });
        ShowStatus($"update() — rows_written={result.RowsWritten}  (run insert first)", false);
        ShowRows(new List<string> { "rows_written" },
            new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    private async Task RunDelete()
    {
        var result = await AppAmbitDb.From("tasks").Where("is_completed", 1).Delete();
        ShowStatus($"delete() — rows_written={result.RowsWritten}  (run update first)", false);
        ShowRows(new List<string> { "rows_written" },
            new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    // ── Typed model ───────────────────────────────────────────────────────────

    private async Task RunTypedModel()
    {
        var tasks = await AppAmbitDb.From<TaskModel>("tasks")
            .Select("id", "title", "is_completed", "priority", "due_date")
            .Limit(5)
            .Get();

        var cols = new List<string> { "id", "title", "isCompleted", "priority", "dueDate" };
        var maps = tasks.Select(t => new Dictionary<string, object?>
        {
            { "id", t.Id },
            { "title", t.Title },
            { "isCompleted", t.IsCompleted },
            { "priority", t.Priority },
            { "dueDate", t.DueDate }
        }).ToList();

        ShowStatus($"from<TaskModel>() — {tasks.Count} typed row(s)", false);
        ShowRows(cols, maps);
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    private async Task RunPresetTables()
    {
        const string q = "SELECT name FROM sqlite_master WHERE type = 'table'";
        EditSql.Text = q;
        var result = await AppAmbitDb.Execute(q);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"sqlite_master tables — {result.RowsRead} row(s)", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    private async Task RunPresetHighPriority()
    {
        const string q = "SELECT * FROM tasks WHERE priority = 'high'";
        EditSql.Text = q;
        var result = await AppAmbitDb.Execute(q);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"tasks WHERE priority='high' — {result.RowsRead} row(s)", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    // ── UI helpers ────────────────────────────────────────────────────────────

    private void ShowStatus(string message, bool isError)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusFrame.IsVisible = true;
            StatusFrame.BackgroundColor = isError ? Color.FromArgb("#FFEBEE") : Color.FromArgb("#E8F5E9");
            TxtStatus.TextColor = isError ? Color.FromArgb("#C62828") : Color.FromArgb("#1B5E20");
            TxtStatus.Text = message;
        });
    }

    private void ShowRows(List<string> columns, List<Dictionary<string, object?>> rows)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (rows.Count == 0)
            {
                HeaderScroll.IsVisible = false;
                ResultsView.ItemsSource = new List<string> { "(no rows)" };
                return;
            }

            // Each row → one monospace line: "col1: val  |  col2: val  |  ..."
            var lines = rows.Select(row =>
                string.Join("   |   ", columns.Select(c =>
                {
                    var val = row.TryGetValue(c, out var v) ? v?.ToString() ?? "null" : "null";
                    return $"{c}: {val}";
                }))).ToList();

            HeaderScroll.IsVisible = false;
            ResultsView.ItemsSource = lines;
        });
    }
}
