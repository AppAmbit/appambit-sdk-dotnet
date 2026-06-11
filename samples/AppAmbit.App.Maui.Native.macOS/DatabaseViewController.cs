using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AppAmbit;
using AppAmbit.Attributes;
using AppAmbit.Models.Db;
using Foundation;
using UIKit;

namespace AppAmbitTestingMacOs;

public class DatabaseViewController : UIViewController
{
    private UITextView _editSql = null!;
    private UIButton _btnFunction = null!;
    private UIButton _btnRun = null!;
    private UILabel _lblStatus = null!;
    private UIActivityIndicatorView _spinner = null!;
    private UITableView _tableView = null!;

    private DbMacTableViewSource _source = null!;

    private List<(string Label, Func<Task> Action)> _demos = new();
    private int _selectedDemoIndex = 0;

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "Database";
        View!.BackgroundColor = UIColor.SystemBackground;
        BuildDemos();
        SetupUI();
    }

    private void BuildDemos()
    {
        _demos = new List<(string, Func<Task>)>
        {
            ("Raw SQL → execute(sql)",                         RunExecute),
            ("Raw SQL → execute(sql, params)",                 RunExecuteParams),
            ("Batch → batch()",                                RunBatch),
            ("Batch → batchInTransaction()",                   RunBatchInTransaction),
            ("Fluent SELECT → select+where+orderByDesc+limit", RunFluentSelect),
            ("Fluent SELECT → where(col,val)",                 RunWhereEquality),
            ("Fluent SELECT → whereIn()",                      RunWhereIn),
            ("Fluent SELECT → limit+offset",                   RunOffset),
            ("Fluent SELECT → first()",                        RunFirst),
            ("Fluent SELECT → count()",                        RunCount),
            ("Fluent WRITE → insert()",                        RunInsert),
            ("Fluent WRITE → update()",                        RunUpdate),
            ("Fluent WRITE → delete()",                        RunDelete),
            ("Typed Model → from(tasks, TaskModel.class)",     RunTypedModel),
            ("Preset → List tables",                           RunPresetTables),
            ("Preset → SELECT * WHERE priority='high'",        RunPresetHighPriority),
        };
    }

    private void SetupUI()
    {
        _editSql = new UITextView
        {
            Text = "SELECT * FROM tasks LIMIT 10",
            Font = UIFont.FromName("Courier", 13) ?? UIFont.SystemFontOfSize(13),
            BackgroundColor = UIColor.SecondarySystemBackground,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _editSql.Layer.CornerRadius = 8;
        _editSql.Layer.BorderColor = UIColor.SystemGray4.CGColor;
        _editSql.Layer.BorderWidth = 1;
        _editSql.TextContainerInset = new UIEdgeInsets(8, 8, 8, 8);

        _btnFunction = UIButton.FromType(UIButtonType.System);
        _btnFunction.SetTitle(_demos[0].Label, UIControlState.Normal);
        _btnFunction.HorizontalAlignment = UIControlContentHorizontalAlignment.Left;
        _btnFunction.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnFunction.Layer.BorderColor = UIColor.SystemGray4.CGColor;
        _btnFunction.Layer.BorderWidth = 1;
        _btnFunction.Layer.CornerRadius = 8;
        _btnFunction.ContentEdgeInsets = new UIEdgeInsets(0, 12, 0, 12);

        if (OperatingSystem.IsIOSVersionAtLeast(14))
        {
            var actions = _demos.Select((d, idx) => UIAction.Create(d.Label, null, null, _ =>
            {
                _selectedDemoIndex = idx;
                _btnFunction.SetTitle(d.Label, UIControlState.Normal);
            })).ToArray();
            _btnFunction.Menu = UIMenu.Create(string.Empty, actions);
            _btnFunction.ShowsMenuAsPrimaryAction = true;
        }

        _btnRun = UIButton.FromType(UIButtonType.System);
        _btnRun.SetTitle("▶ Run", UIControlState.Normal);
        _btnRun.BackgroundColor = UIColor.SystemBlue;
        _btnRun.SetTitleColor(UIColor.White, UIControlState.Normal);
        _btnRun.Layer.CornerRadius = 8;
        _btnRun.TranslatesAutoresizingMaskIntoConstraints = false;
        _btnRun.TouchUpInside += async (s, e) => await OnRun();

        _lblStatus = new UILabel
        {
            Text = "",
            Font = UIFont.SystemFontOfSize(12),
            Lines = 0,
            Hidden = true,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        _lblStatus.Layer.CornerRadius = 6;
        _lblStatus.Layer.MasksToBounds = true;

        _spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            HidesWhenStopped = true,
            Color = UIColor.SystemBlue
        };

        _tableView = new UITableView
        {
            TranslatesAutoresizingMaskIntoConstraints = false,
            RowHeight = UITableView.AutomaticDimension,
            EstimatedRowHeight = 44f,
            SeparatorStyle = UITableViewCellSeparatorStyle.SingleLine
        };
        _tableView.RegisterClassForCellReuse(typeof(DbMacRowCell), DbMacRowCell.Key);
        _source = new DbMacTableViewSource();
        _tableView.Source = _source;

        var funcRow = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        funcRow.AddSubviews(_btnFunction, _btnRun);

        View!.AddSubviews(_editSql, funcRow, _lblStatus, _spinner, _tableView);

        var g = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _editSql.TopAnchor.ConstraintEqualTo(g.TopAnchor, 12),
            _editSql.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _editSql.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            _editSql.HeightAnchor.ConstraintEqualTo(70),

            funcRow.TopAnchor.ConstraintEqualTo(_editSql.BottomAnchor, 10),
            funcRow.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            funcRow.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),
            funcRow.HeightAnchor.ConstraintEqualTo(44),

            _btnFunction.TopAnchor.ConstraintEqualTo(funcRow.TopAnchor),
            _btnFunction.LeadingAnchor.ConstraintEqualTo(funcRow.LeadingAnchor),
            _btnFunction.TrailingAnchor.ConstraintEqualTo(_btnRun.LeadingAnchor, -8),
            _btnFunction.BottomAnchor.ConstraintEqualTo(funcRow.BottomAnchor),

            _btnRun.TopAnchor.ConstraintEqualTo(funcRow.TopAnchor),
            _btnRun.TrailingAnchor.ConstraintEqualTo(funcRow.TrailingAnchor),
            _btnRun.WidthAnchor.ConstraintEqualTo(80),
            _btnRun.BottomAnchor.ConstraintEqualTo(funcRow.BottomAnchor),

            _lblStatus.TopAnchor.ConstraintEqualTo(funcRow.BottomAnchor, 8),
            _lblStatus.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor, 16),
            _lblStatus.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor, -16),

            _spinner.TopAnchor.ConstraintEqualTo(_lblStatus.BottomAnchor, 8),
            _spinner.CenterXAnchor.ConstraintEqualTo(g.CenterXAnchor),

            _tableView.TopAnchor.ConstraintEqualTo(_spinner.BottomAnchor, 4),
            _tableView.LeadingAnchor.ConstraintEqualTo(g.LeadingAnchor),
            _tableView.TrailingAnchor.ConstraintEqualTo(g.TrailingAnchor),
            _tableView.BottomAnchor.ConstraintEqualTo(g.BottomAnchor),
        });
    }

    private async Task OnRun()
    {
        SetLoading(true);
        var demo = _demos[_selectedDemoIndex];
        ShowStatus($"Running: {demo.Label}", false);
        try { await demo.Action(); }
        catch (Exception ex) { ShowStatus($"Error: {ex.Message}", true); }
        finally { SetLoading(false); }
    }

    private void SetLoading(bool loading)
    {
        InvokeOnMainThread(() =>
        {
            _btnRun.Enabled = !loading;
            if (loading) _spinner.StartAnimating(); else _spinner.StopAnimating();
        });
    }

    private void ShowStatus(string message, bool isError)
    {
        InvokeOnMainThread(() =>
        {
            _lblStatus.Hidden = false;
            _lblStatus.Text = "  " + message + "  ";
            _lblStatus.BackgroundColor = isError ? UIColor.FromRGB(255, 235, 238) : UIColor.FromRGB(232, 245, 233);
            _lblStatus.TextColor = isError ? UIColor.FromRGB(198, 40, 40) : UIColor.FromRGB(27, 94, 32);
        });
    }

    private void ShowRows(List<string> columns, List<Dictionary<string, object?>> rows)
    {
        InvokeOnMainThread(() => { _source.UpdateData(columns, rows); _tableView.ReloadData(); });
    }

    // ── Raw Execute ───────────────────────────────────────────────────────────

    private async Task RunExecute()
    {
        var sql = _editSql.Text?.Trim();
        if (string.IsNullOrWhiteSpace(sql)) { sql = "SELECT * FROM tasks LIMIT 10"; InvokeOnMainThread(() => _editSql.Text = sql); }
        var result = await AppAmbitDb.Execute(sql);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"execute(sql) — rows_read={result.RowsRead}  rows_written={result.RowsWritten}", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    private async Task RunExecuteParams()
    {
        var result = await AppAmbitDb.Execute("SELECT * FROM tasks WHERE is_completed = ? LIMIT ?", 0, 10);
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
        var maps = results.Select((r, i) => new Dictionary<string, object?> { { "statement", i + 1 }, { "rows_written", r.RowsWritten }, { "rows_read", r.RowsRead } }).ToList();
        ShowStatus($"batch() — {written} row(s) written, {results.Count} statements, no transaction", false);
        ShowRows(cols, maps);
    }

    private async Task RunBatchInTransaction()
    {
        var results = await AppAmbitDb.BatchInTransaction(
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Team meeting", 0, "high", "2026-06-06"),
            DbStatement.Of("INSERT INTO tasks (title, is_completed, priority, due_date) VALUES (?, ?, ?, ?)", "Prepare agenda", 0, "medium", "2026-06-06"));
        int written = results.Sum(r => r.RowsWritten);
        var cols = new List<string> { "statement", "rows_written" };
        var maps = results.Select((r, i) => new Dictionary<string, object?> { { "statement", i + 1 }, { "rows_written", r.RowsWritten } }).ToList();
        ShowStatus($"batchInTransaction() — {written} row(s) written, rolled back on any failure", false);
        ShowRows(cols, maps);
    }

    // ── Fluent SELECT ─────────────────────────────────────────────────────────

    private async Task RunFluentSelect()
    {
        var maps = await AppAmbitDb.From("tasks").Select("id", "title", "priority", "due_date").Where("is_completed", "=", 0).OrderByDesc("due_date").Limit(5).Get();
        ShowStatus(maps.Count == 0 ? "No pending tasks" : $"from().select().where().orderByDesc().limit(5) — {maps.Count} row(s)", false);
        if (maps.Count > 0) ShowRows(maps[0].Keys.ToList(), maps);
    }

    private async Task RunWhereEquality()
    {
        var maps = await AppAmbitDb.From("tasks").Where("is_completed", 0).Get();
        ShowStatus(maps.Count == 0 ? "No pending tasks" : $"where(is_completed, 0) — {maps.Count} row(s)", false);
        ShowRows(maps.Count > 0 ? maps[0].Keys.ToList() : new List<string>(), maps);
    }

    private async Task RunWhereIn()
    {
        var maps = await AppAmbitDb.From("tasks").WhereIn("priority", new object?[] { "high", "medium" }).OrderBy("due_date").Get();
        ShowStatus(maps.Count == 0 ? "No high/medium tasks" : $"whereIn(priority, [high,medium]) — {maps.Count} row(s)", false);
        ShowRows(maps.Count > 0 ? maps[0].Keys.ToList() : new List<string>(), maps);
    }

    private async Task RunOffset()
    {
        var maps = await AppAmbitDb.From("tasks").OrderBy("due_date").Limit(5).Offset(0).Get();
        ShowStatus(maps.Count == 0 ? "No tasks" : $"limit(5).offset(0) — page 1, {maps.Count} row(s)", false);
        ShowRows(maps.Count > 0 ? maps[0].Keys.ToList() : new List<string>(), maps);
    }

    private async Task RunFirst()
    {
        var item = await AppAmbitDb.From("tasks").Where("is_completed", "=", 0).OrderBy("due_date").First();
        if (item == null) { ShowStatus("first() — no pending tasks", false); ShowRows(new List<string>(), new List<Dictionary<string, object?>>()); return; }
        ShowStatus("first() — next task to expire", false);
        ShowRows(item.Keys.ToList(), new List<Dictionary<string, object?>> { item });
    }

    private async Task RunCount()
    {
        var count = await AppAmbitDb.From("tasks").Where("is_completed", 0).Count();
        ShowStatus($"count() — {count} pending task(s)", false);
        ShowRows(new List<string> { "pending_tasks" }, new List<Dictionary<string, object?>> { new() { { "pending_tasks", count } } });
    }

    // ── Mutations ─────────────────────────────────────────────────────────────

    private async Task RunInsert()
    {
        var result = await AppAmbitDb.From("tasks").Insert(new Dictionary<string, object?> { { "title", "New task" }, { "is_completed", 0 }, { "priority", "medium" }, { "due_date", DateTime.UtcNow.AddDays(7).ToString("yyyy-MM-dd") } });
        ShowStatus($"insert() — rows_written={result.RowsWritten}", false);
        ShowRows(new List<string> { "rows_written" }, new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    private async Task RunUpdate()
    {
        var result = await AppAmbitDb.From("tasks").Where("title", "New task").Update(new Dictionary<string, object?> { { "is_completed", 1 } });
        ShowStatus($"update() — rows_written={result.RowsWritten}  (run insert first)", false);
        ShowRows(new List<string> { "rows_written" }, new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    private async Task RunDelete()
    {
        var result = await AppAmbitDb.From("tasks").Where("is_completed", 1).Delete();
        ShowStatus($"delete() — rows_written={result.RowsWritten}  (run update first)", false);
        ShowRows(new List<string> { "rows_written" }, new List<Dictionary<string, object?>> { new() { { "rows_written", result.RowsWritten } } });
    }

    // ── Typed model ───────────────────────────────────────────────────────────

    private async Task RunTypedModel()
    {
        var tasks = await AppAmbitDb.From<MacTaskModel>("tasks").Select("id", "title", "is_completed", "priority", "due_date").Limit(5).Get();
        var cols = new List<string> { "id", "title", "isCompleted", "priority", "dueDate" };
        var maps = tasks.Select(t => new Dictionary<string, object?> { { "id", t.Id }, { "title", t.Title }, { "isCompleted", t.IsCompleted }, { "priority", t.Priority }, { "dueDate", t.DueDate } }).ToList();
        ShowStatus($"from<TaskModel>() — {tasks.Count} typed row(s)", false);
        ShowRows(cols, maps);
    }

    // ── Presets ───────────────────────────────────────────────────────────────

    private async Task RunPresetTables()
    {
        const string q = "SELECT name FROM sqlite_master WHERE type = 'table'";
        InvokeOnMainThread(() => _editSql.Text = q);
        var result = await AppAmbitDb.Execute(q);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"sqlite_master tables — {result.RowsRead} row(s)", false);
        ShowRows(result.Columns, result.ToMaps());
    }

    private async Task RunPresetHighPriority()
    {
        const string q = "SELECT * FROM tasks WHERE priority = 'high'";
        InvokeOnMainThread(() => _editSql.Text = q);
        var result = await AppAmbitDb.Execute(q);
        if (result.HasError) { ShowStatus($"Error: {result.Error}", true); return; }
        ShowStatus($"tasks WHERE priority='high' — {result.RowsRead} row(s)", false);
        ShowRows(result.Columns, result.ToMaps());
    }
}

// ── TaskModel ─────────────────────────────────────────────────────────────────

public class MacTaskModel
{
    public int Id { get; set; }
    public string? Title { get; set; }
    [DbColumn("is_completed")] public int IsCompleted { get; set; }
    public string? Priority { get; set; }
    [DbColumn("due_date")] public string? DueDate { get; set; }
}

// ── Table view data source ────────────────────────────────────────────────────

public class DbMacTableViewSource : UITableViewSource
{
    private List<string> _columns = new();
    private List<Dictionary<string, object?>> _rows = new();

    public void UpdateData(List<string> columns, List<Dictionary<string, object?>> rows)
    {
        _columns = columns;
        _rows = rows;
    }

    public override nint RowsInSection(UITableView tableview, nint section) => _rows.Count;

    public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
    {
        var cell = (DbMacRowCell)tableView.DequeueReusableCell(DbMacRowCell.Key, indexPath);
        cell.Bind(_columns, _rows[indexPath.Row]);
        return cell;
    }
}

// ── Table view cell ───────────────────────────────────────────────────────────

public class DbMacRowCell : UITableViewCell
{
    public static readonly NSString Key = new NSString(nameof(DbMacRowCell));

    private UIScrollView _scroll = null!;
    private UIStackView _stack = null!;

    [Export("initWithStyle:reuseIdentifier:")]
    public DbMacRowCell(UITableViewCellStyle style, NSString reuseIdentifier) : base(style, reuseIdentifier)
    {
        SelectionStyle = UITableViewCellSelectionStyle.None;

        _scroll = new UIScrollView { TranslatesAutoresizingMaskIntoConstraints = false, ShowsVerticalScrollIndicator = false };
        _stack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Spacing = 16,
            Alignment = UIStackViewAlignment.Top,
            Distribution = UIStackViewDistribution.EqualSpacing,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        _scroll.AddSubview(_stack);
        ContentView.AddSubview(_scroll);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _scroll.TopAnchor.ConstraintEqualTo(ContentView.TopAnchor, 4),
            _scroll.LeadingAnchor.ConstraintEqualTo(ContentView.LeadingAnchor, 8),
            _scroll.TrailingAnchor.ConstraintEqualTo(ContentView.TrailingAnchor, -8),
            _scroll.BottomAnchor.ConstraintEqualTo(ContentView.BottomAnchor, -4),
            _scroll.HeightAnchor.ConstraintEqualTo(52),

            _stack.TopAnchor.ConstraintEqualTo(_scroll.TopAnchor),
            _stack.LeadingAnchor.ConstraintEqualTo(_scroll.LeadingAnchor),
            _stack.BottomAnchor.ConstraintEqualTo(_scroll.BottomAnchor),
            _stack.HeightAnchor.ConstraintEqualTo(_scroll.HeightAnchor),
        });
    }

    public void Bind(List<string> columns, Dictionary<string, object?> row)
    {
        foreach (var sub in _stack.ArrangedSubviews) sub.RemoveFromSuperview();
        foreach (var col in columns)
        {
            var val = row.TryGetValue(col, out var v) ? v?.ToString() ?? "null" : "null";
            var colView = new UIStackView { Axis = UILayoutConstraintAxis.Vertical, Spacing = 2, TranslatesAutoresizingMaskIntoConstraints = false };
            colView.AddArrangedSubview(new UILabel { Text = col, Font = UIFont.BoldSystemFontOfSize(10), TextColor = UIColor.SystemGray, TranslatesAutoresizingMaskIntoConstraints = false });
            colView.AddArrangedSubview(new UILabel { Text = val, Font = UIFont.FromName("Courier", 13) ?? UIFont.SystemFontOfSize(13), TextColor = UIColor.Label, TranslatesAutoresizingMaskIntoConstraints = false });
            _stack.AddArrangedSubview(colView);
        }
    }
}
