using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Interfaces;
using AppAmbitMaui;
using Microsoft.Maui.Controls.Shapes;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AppAmbitTestingApp;

public partial class CloudCodePage : ContentPage
{
#if ANDROID
    private const string Platform = "android";
#else
    private const string Platform = "ios";
#endif

    private bool _isRunning;
    private bool _databaseAvailable;
    private bool _databaseTablesReady;
    private bool _isVerifyingBackend;
    private bool _resultExpanded = true;
    private string _fullResultText = "Run a function to see its response here.";
    private readonly Dictionary<string, Border> _functionCards = new(StringComparer.Ordinal);

    private sealed record CloudCodeDemo(
        string Id,
        string Section,
        string Detail,
        string Prerequisite,
        bool RequiresConfirmation = false);

    private static readonly CloudCodeDemo SetupDatabaseDemo = new(
        "setup-database",
        "Database",
        "Create the tables used by the Database examples without destroying data.",
        "Existing linked Database");

    private static readonly CloudCodeDemo[] Demos =
    {
        new("create-task", "Database", "Insert a task for the signed-in consumer.", "cloud_demo_tasks"),
        new("list-tasks", "Database", "Read the current consumer's tasks.", "cloud_demo_tasks"),
        new("complete-task", "Database", "Update one task with consumer ownership.", "Task id"),
        new("delete-task", "Database", "Delete one task owned by the consumer.", "Task id + confirmation", true),
        new("create-order", "Database", "Create an order without duplicate idempotency keys.", "cloud_demo_orders"),
        new("dashboard-summary", "Database", "Combine Database and CMS in one typed response.", "Database + CMS"),
        new("publish-post", "CMS", "Create a published CMS entry.", "Confirmation", true),
        new("read-posts", "CMS", "List published entries using only CMS data.", "cloud_code_demo_posts"),
        new("send-push", "Push", "Send a notification to all consumers.", "Permission + push credentials", true),
        new("http-inspector", "HTTP", "Inspect method, query, body and consumer context.", "HTTP trigger"),
        new("json-values", "HTTP", "Return common JSON value types.", "HTTP trigger"),
        new("null-contract", "HTTP", "Compare raw null and an explicit value.", "HTTP trigger"),
        new("response-shapes", "HTTP", "Demonstrate statuses, body and headers.", "HTTP trigger"),
        new("error-response", "HTTP", "Return a safe client error response.", "HTTP trigger"),
        new("timeout-10s", "HTTP", "Observe the configured function timeout.", "Function timeout = 10 s"),
        new("runtime-context", "HTTP", "Use environment values, secrets and logs safely.", "DEMO_REGION + DEMO_SECRET")
    };

    private static readonly HashSet<string> SharedFunctionNames = new(StringComparer.Ordinal)
    {
        "http-inspector",
        "json-values",
        "null-contract",
        "response-shapes",
        "error-response",
        "timeout-10s",
        "runtime-context"
    };

    public CloudCodePage()
    {
        InitializeComponent();
        PlatformLabel.Text = $"HTTP-triggered functions using the {Platform} consumer token.";
        SetupDatabaseSlugLabel.Text = Slug(SetupDatabaseDemo.Id);
        BuildDemoCards();
        Loaded += (_, _) => _ = VerifyBackendAsync();
    }

    private string Slug(string name) =>
        $"cloud-demo-{name}{(SharedFunctionNames.Contains(name) ? string.Empty : $"-{Platform}")}";

    private Dictionary<string, string> Headers() => new() { ["X-Sample-Client"] = $"dotnet-{Platform}" };

    private void UpdateSetupDatabaseButtonState()
    {
        SetupDatabaseButton.IsEnabled = _databaseAvailable
            && !_databaseTablesReady
            && !_isRunning
            && !_isVerifyingBackend;
    }

    private void BuildDemoCards()
    {
        _functionCards[SetupDatabaseDemo.Id] = SetupDatabaseCard;

        foreach (var demo in Demos)
        {
            var card = CreateDemoCard(demo);
            DemoLayout(demo.Section).Children.Add(card);
            _functionCards[demo.Id] = card;
        }
    }

    private Border CreateDemoCard(CloudCodeDemo demo)
    {
        var info = new VerticalStackLayout { Spacing = 2 };
        info.Children.Add(new Label
        {
            Text = Slug(demo.Id),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold
        });
        info.Children.Add(new Label
        {
            Text = demo.Detail,
            FontSize = 12,
            TextColor = Color.FromArgb("#5F6368")
        });
        info.Children.Add(new Label
        {
            Text = demo.Prerequisite,
            FontSize = 11,
            TextColor = Color.FromArgb("#7A7A7A")
        });

        var runButton = new Button
        {
            Text = "▶ Run",
            MinimumHeightRequest = 44
        };
        runButton.Clicked += async (_, _) => await RunOrConfirmAsync(demo);

        var grid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        grid.Add(info);
        grid.Add(runButton, 1);

        return new Border
        {
            Stroke = Color.FromArgb("#D7DCE5"),
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(10) },
            Background = Color.FromArgb("#F8F8F8"),
            Padding = new Thickness(12, 10),
            Content = grid
        };
    }

    private VerticalStackLayout DemoLayout(string section) => section switch
    {
        "Database" => DatabaseDemosLayout,
        "CMS" => CmsDemosLayout,
        "Push" => PushDemosLayout,
        "HTTP" => HttpDemosLayout,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
    };

    private async Task VerifyBackendAsync()
    {
        if (_isRunning || _isVerifyingBackend) return;

        _isVerifyingBackend = true;
        DatabaseStatusLabel.Text = "Checking...";
        DatabaseStatusLabel.TextColor = Colors.Gray;
        CmsStatusLabel.Text = "Checking...";
        CmsStatusLabel.TextColor = Colors.Gray;
        UpdateSetupDatabaseButtonState();
        try
        {
            var result = await CloudCode.Call<DashboardSummary>(
                Slug("dashboard-summary"),
                HttpMethodEnum.Get,
                headers: Headers());
            var summary = result.Data;
            _databaseAvailable = summary?.DatabaseAvailable == true;
            _databaseTablesReady = summary?.DatabaseTablesReady == true;
            DatabaseStatusLabel.Text = _databaseAvailable ? "Available" : "Not available";
            DatabaseStatusLabel.TextColor = _databaseAvailable ? Colors.Green : Colors.Gray;
            var cmsAvailable = summary?.Posts != null;
            CmsStatusLabel.Text = cmsAvailable ? "Available" : "Not available";
            CmsStatusLabel.TextColor = cmsAvailable ? Colors.Green : Colors.Gray;
        }
        catch (CloudCodeError error)
        {
            _databaseAvailable = false;
            _databaseTablesReady = false;
            DatabaseStatusLabel.Text = "Not available";
            DatabaseStatusLabel.TextColor = Colors.Gray;
            CmsStatusLabel.Text = "Not available";
            CmsStatusLabel.TextColor = Colors.Gray;
            ResultTitleLabel.Text = "Backend verification failed";
            ResultBorder.IsVisible = true;
            SetResultText(FormatError(error, 0));
        }
        finally
        {
            _isVerifyingBackend = false;
            UpdateSetupDatabaseButtonState();
        }
    }

    private async void OnSetupDatabaseClicked(object? sender, EventArgs e) => await RunOrConfirmAsync(SetupDatabaseDemo);

    private async Task RunOrConfirmAsync(CloudCodeDemo demo)
    {
        if (demo.Id == SetupDatabaseDemo.Id && (!_databaseAvailable || _databaseTablesReady))
            return;

        if (demo.RequiresConfirmation && !await DisplayAlertAsync(
                "Confirm Cloud Code action",
                "This calls a real backend operation. Continue only if the required service is configured.",
                "Run",
                "Cancel"))
        {
            return;
        }

        await RunDemoAsync(demo);
    }

    private Task RunDemoAsync(CloudCodeDemo demo) => demo.Id switch
    {
        "setup-database" => RunAsync(demo.Id, HttpMethodEnum.Post),
        "create-task" => RunAsync(demo.Id, HttpMethodEnum.Post, body: new { title = TaskTitleEntry.Text ?? string.Empty }),
        "list-tasks" => RunAsync(demo.Id, HttpMethodEnum.Get, query: new Dictionary<string, string> { ["limit"] = "20" }),
        "complete-task" => RunTaskMutationAsync(demo.Id, HttpMethodEnum.Patch),
        "delete-task" => RunTaskMutationAsync(demo.Id, HttpMethodEnum.Delete),
        "create-order" => RunAsync(demo.Id, HttpMethodEnum.Post, body: new { idempotency_key = Guid.NewGuid().ToString(), amount = 100 }),
        "dashboard-summary" => RunTypedAsync<DashboardSummary>(demo.Id, HttpMethodEnum.Get),
        "publish-post" => RunAsync(demo.Id, HttpMethodEnum.Post, body: new { title = PublishTitleEntry.Text ?? string.Empty, body = PublishBodyEditor.Text ?? string.Empty }),
        "read-posts" => RunAsync(demo.Id, HttpMethodEnum.Get, query: ReadPostsQuery()),
        "send-push" => RunAsync(demo.Id, HttpMethodEnum.Post, body: new { title = $"Cloud Code {Platform} demo", body = $"Push from the .NET {Platform} sample." }),
        "http-inspector" => RunAsync(demo.Id, HttpMethodEnum.Post, new Dictionary<string, string> { ["source"] = $"dotnet-{Platform}" }, new { message = "hello", count = 2 }),
        "json-values" => RunAsync(demo.Id, HttpMethodEnum.Post),
        "null-contract" => RunAsync(demo.Id, HttpMethodEnum.Get),
        "response-shapes" => RunAsync(demo.Id, HttpMethodEnum.Post),
        "error-response" => RunAsync(demo.Id, HttpMethodEnum.Post, body: new { invalid = true }),
        "timeout-10s" => RunAsync(demo.Id, HttpMethodEnum.Get),
        "runtime-context" => RunAsync(demo.Id, HttpMethodEnum.Get),
        _ => throw new ArgumentOutOfRangeException(nameof(demo), demo.Id, null)
    };

    private Dictionary<string, string>? ReadPostsQuery()
    {
        var uuid = PostUuidEntry.Text?.Trim();
        return string.IsNullOrWhiteSpace(uuid) ? null : new Dictionary<string, string> { ["uuid"] = uuid };
    }

    private async Task RunTaskMutationAsync(string name, HttpMethodEnum method)
    {
        if (!int.TryParse(TaskIdEntry.Text?.Trim(), out var taskId))
        {
            ResultTitleLabel.Text = "Input required";
            ResultBorder.IsVisible = true;
            SetResultText("Enter a numeric task id first.");
            return;
        }
        await RunAsync(name, method, body: new { task_id = taskId });
    }

    private async Task RunAsync(
        string name,
        HttpMethodEnum method,
        IReadOnlyDictionary<string, string>? query = null,
        object? body = null)
    {
        if (_isRunning) return;
        _isRunning = true;
        UpdateSetupDatabaseButtonState();
        var slug = name.Contains("-") && name.StartsWith("cloud-demo-", StringComparison.Ordinal)
            ? name
            : Slug(name);
        var started = Stopwatch.GetTimestamp();
        PrepareResult(name, slug);
        ResultTitleLabel.Text = $"Result · {slug}";
        SetResultText($"Calling {slug}...");
        try
        {
            var response = await CloudCode.Call(slug, method, query, body, Headers());
            SetResultText(FormatResponse(response, Elapsed(started)));
            if (name == "setup-database")
            {
                _isRunning = false;
                await VerifyBackendAsync();
            }
        }
        catch (CloudCodeError error)
        {
            SetResultText(FormatError(error, Elapsed(started)));
        }
        finally
        {
            _isRunning = false;
            UpdateSetupDatabaseButtonState();
        }
    }

    private async Task RunTypedAsync<T>(string name, HttpMethodEnum method)
    {
        if (_isRunning) return;
        _isRunning = true;
        var slug = Slug(name);
        var started = Stopwatch.GetTimestamp();
        PrepareResult(name, slug);
        ResultTitleLabel.Text = $"Result · {slug}";
        SetResultText($"Calling {slug}...");
        try
        {
            var result = await CloudCode.Call<T>(slug, method, headers: Headers());
            SetResultText($"HTTP {result.StatusCode}\nDuration: {Elapsed(started):0.00} s\nrequestId: {result.RequestId ?? "none"}\nBody: {JsonText(result.Data)}");
        }
        catch (CloudCodeError error)
        {
            SetResultText(FormatError(error, Elapsed(started)));
        }
        finally
        {
            _isRunning = false;
        }
    }

    private static string FormatResponse(CloudCodeResponse response, double elapsed) =>
        $"HTTP {response.StatusCode}\nDuration: {elapsed:0.00} s\nrequestId: {response.RequestId ?? "none"}\nBody: {JsonText(response.Data)}";

    private static string FormatError(CloudCodeError error, double elapsed) =>
        $"Duration: {elapsed:0.00} s\nrequestId: {error.RequestId ?? "none"}\nHTTP error body: {JsonText(error.Body ?? error.RawBody)}\nError: {error.Message}";

    private void PrepareResult(string demoId, string slug)
    {
        if (ResultBorder.Parent is Layout currentParent)
        {
            currentParent.Children.Remove(ResultBorder);
        }

        if (_functionCards.TryGetValue(demoId, out var card) && card.Parent is Layout parent)
        {
            var index = parent.Children.IndexOf(card);
            parent.Children.Insert(index + 1, ResultBorder);
        }
        else
        {
            RootLayout.Children.Add(ResultBorder);
        }

        ResultBorder.IsVisible = true;
        ResultTitleLabel.Text = $"Result · {slug}";
        _resultExpanded = true;
        UpdateResultDisplay();
    }

    private void SetResultText(string text)
    {
        _fullResultText = text;
        UpdateResultDisplay();
    }

    private void OnToggleResultClicked(object? sender, EventArgs e)
    {
        _resultExpanded = !_resultExpanded;
        UpdateResultDisplay();
    }

    private void UpdateResultDisplay()
    {
        if (ResultEditor == null || ResultToggleButton == null) return;
        ResultEditor.Text = _resultExpanded
            ? _fullResultText
            : string.Join(Environment.NewLine, _fullResultText.Split('\n').Take(2));
        ResultToggleButton.Text = _resultExpanded ? "⌃" : "⌄";
        SemanticProperties.SetDescription(ResultToggleButton, _resultExpanded ? "Collapse response" : "Expand response");
    }

    private static string JsonText(object? value)
    {
        if (value == null) return "null";
        if (value is JValue jValue && jValue.Type == JTokenType.Null) return "null";
        return value is JToken token
            ? token.ToString(Formatting.Indented)
            : JsonConvert.SerializeObject(value, Formatting.Indented);
    }

    private static double Elapsed(long started) =>
        (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;

    private sealed class DashboardSummary
    {
        [JsonProperty("task_count")]
        public int? TaskCount { get; set; }

        [JsonProperty("database_available")]
        public bool DatabaseAvailable { get; set; }

        [JsonProperty("database_tables_ready")]
        public bool DatabaseTablesReady { get; set; }

        [JsonProperty("posts")]
        public List<object?>? Posts { get; set; }

        [JsonProperty("platform")]
        public string? Platform { get; set; }
    }
}
