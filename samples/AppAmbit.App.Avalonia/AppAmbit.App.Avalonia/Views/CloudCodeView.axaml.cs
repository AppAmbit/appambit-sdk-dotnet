using AppAmbit.Models.CloudCode;
using AppAmbit.Services.Interfaces;
using AppAmbitAvalonia;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Diagnostics;

namespace AppAmbitTestingAppAvalonia.Views;

public partial class CloudCodeView : UserControl
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

    public CloudCodeView()
    {
        InitializeComponent();
        PlatformLabel.Text = $"HTTP-triggered functions using the {Platform} consumer token.";
        SetupDatabaseSlugLabel.Text = Slug(SetupDatabaseDemo.Id);
        BuildDemoCards();
        _ = VerifyBackendAsync();
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
        _functionCards[SetupDatabaseDemo.Id] = SetupFunctionCard;

        foreach (var demo in Demos)
        {
            var card = CreateDemoCard(demo);
            DemoPanel(demo.Section).Children.Add(card);
            _functionCards[demo.Id] = card;
        }
    }

    private Border CreateDemoCard(CloudCodeDemo demo)
    {
        var info = new StackPanel { Spacing = 3 };
        info.Children.Add(new TextBlock
        {
            Text = Slug(demo.Id),
            FontSize = 14,
            FontWeight = FontWeight.Bold
        });
        info.Children.Add(new TextBlock
        {
            Text = demo.Detail,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.Parse("#5F6368")),
            TextWrapping = TextWrapping.Wrap
        });
        info.Children.Add(new TextBlock
        {
            Text = demo.Prerequisite,
            FontSize = 11,
            Foreground = new SolidColorBrush(Color.Parse("#7A7A7A")),
            TextWrapping = TextWrapping.Wrap
        });

        var runButton = new Avalonia.Controls.Button
        {
            Content = "▶ Run",
            MinHeight = 44
        };
        runButton.Click += async (_, _) => await RunOrConfirmAsync(demo);

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            },
            ColumnSpacing = 8
        };
        grid.Children.Add(info);
        Grid.SetColumn(runButton, 1);
        grid.Children.Add(runButton);

        return new Border
        {
            Background = new SolidColorBrush(Color.Parse("#F8F8F8")),
            BorderBrush = new SolidColorBrush(Color.Parse("#D7DCE5")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Child = grid
        };
    }

    private StackPanel DemoPanel(string section) => section switch
    {
        "Database" => DatabaseDemosPanel,
        "CMS" => CmsDemosPanel,
        "Push" => PushDemosPanel,
        "HTTP" => HttpDemosPanel,
        _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
    };

    private async void OnRunButtonClicked(object? sender, RoutedEventArgs e)
    {
        if (sender is Avalonia.Controls.Button { Tag: string action })
        {
            if (_functionCards.TryGetValue(action, out _))
                await RunOrConfirmAsync(DemoFor(action));
        }
    }

    private CloudCodeDemo DemoFor(string id) => id == SetupDatabaseDemo.Id
        ? SetupDatabaseDemo
        : Demos.First(demo => demo.Id == id);

    private async Task RunOrConfirmAsync(CloudCodeDemo demo)
    {
        if (demo.Id == SetupDatabaseDemo.Id && (!_databaseAvailable || _databaseTablesReady))
            return;

        if (demo.RequiresConfirmation)
        {
            var confirmed = await AlertWindow.ShowConfirmation(
                "This calls a real backend operation. Continue only if the required service is configured.");
            if (!confirmed)
                return;
        }

        await RunAsync(demo.Id);
    }

    private async Task VerifyBackendAsync()
    {
        if (_isVerifyingBackend) return;

        _isVerifyingBackend = true;
        DatabaseStatusLabel.Text = "Checking...";
        DatabaseStatusLabel.Foreground = Brushes.Gray;
        CmsStatusLabel.Text = "Checking...";
        CmsStatusLabel.Foreground = Brushes.Gray;
        UpdateSetupDatabaseButtonState();
        try
        {
            var result = await CloudCode.Call<DashboardSummary>(Slug("dashboard-summary"), HttpMethodEnum.Get, headers: Headers());
            var summary = result.Data;
            _databaseAvailable = summary?.DatabaseAvailable == true;
            _databaseTablesReady = summary?.DatabaseTablesReady == true;
            DatabaseStatusLabel.Text = _databaseAvailable ? "Available" : "Not available";
            DatabaseStatusLabel.Foreground = _databaseAvailable ? Brushes.Green : Brushes.Gray;
            var cmsAvailable = summary?.Posts != null;
            CmsStatusLabel.Text = cmsAvailable ? "Available" : "Not available";
            CmsStatusLabel.Foreground = cmsAvailable ? Brushes.Green : Brushes.Gray;
        }
        catch (CloudCodeError error)
        {
            _databaseAvailable = false;
            _databaseTablesReady = false;
            DatabaseStatusLabel.Text = "Not available";
            DatabaseStatusLabel.Foreground = Brushes.Gray;
            CmsStatusLabel.Text = "Not available";
            CmsStatusLabel.Foreground = Brushes.Gray;
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

    private async Task RunAsync(string action)
    {
        if (_isRunning) return;
        if ((action is "complete-task" or "delete-task") && !int.TryParse(TaskIdTextBox.Text?.Trim(), out _))
        {
            ResultTitleLabel.Text = "Input required";
            ResultTextBox.Text = "Enter a numeric task id first.";
            return;
        }

        _isRunning = true;
        UpdateSetupDatabaseButtonState();
        var configuration = Configure(action);
        var started = Stopwatch.GetTimestamp();
        PrepareResult(action, configuration.Slug);
        ResultTitleLabel.Text = $"Result · {configuration.Slug}";
        SetResultText($"Calling {configuration.Slug}...");
        try
        {
            if (action == "dashboard-summary")
            {
                var result = await CloudCode.Call<DashboardSummary>(configuration.Slug, configuration.Method, configuration.Query, configuration.Body, Headers());
                SetResultText($"HTTP {result.StatusCode}\nDuration: {Elapsed(started):0.00} s\nrequestId: {result.RequestId ?? "none"}\nBody: {JsonText(result.Data)}");
            }
            else
            {
                var response = await CloudCode.Call(configuration.Slug, configuration.Method, configuration.Query, configuration.Body, Headers());
                SetResultText(FormatResponse(response, Elapsed(started)));
            }

            if (action == "setup-database")
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

    private (string Slug, HttpMethodEnum Method, IReadOnlyDictionary<string, string>? Query, object? Body) Configure(string action)
    {
        var taskId = int.TryParse(TaskIdTextBox.Text?.Trim(), out var parsedTaskId) ? parsedTaskId : 0;
        return action switch
        {
            "setup-database" => (Slug("setup-database"), HttpMethodEnum.Post, null, null),
            "create-task" => (Slug("create-task"), HttpMethodEnum.Post, null, new { title = TaskTitleTextBox.Text ?? string.Empty }),
            "list-tasks" => (Slug("list-tasks"), HttpMethodEnum.Get, new Dictionary<string, string> { ["limit"] = "20" }, null),
            "complete-task" => (Slug("complete-task"), HttpMethodEnum.Patch, null, new { task_id = taskId }),
            "delete-task" => (Slug("delete-task"), HttpMethodEnum.Delete, null, new { task_id = taskId }),
            "create-order" => (Slug("create-order"), HttpMethodEnum.Post, null, new { idempotency_key = Guid.NewGuid().ToString(), amount = 100 }),
            "dashboard-summary" => (Slug("dashboard-summary"), HttpMethodEnum.Get, null, null),
            "publish-post" => (Slug("publish-post"), HttpMethodEnum.Post, null, new { title = PublishTitleTextBox.Text ?? string.Empty, body = PublishBodyTextBox.Text ?? string.Empty }),
            "read-posts" => (Slug("read-posts"), HttpMethodEnum.Get, string.IsNullOrWhiteSpace(PostUuidTextBox.Text) ? null : new Dictionary<string, string> { ["uuid"] = PostUuidTextBox.Text.Trim() }, null),
            "send-push" => (Slug("send-push"), HttpMethodEnum.Post, null, new { title = $"Cloud Code {Platform} demo", body = $"Push from the .NET {Platform} sample." }),
            "http-inspector" => ("cloud-demo-http-inspector", HttpMethodEnum.Post, new Dictionary<string, string> { ["source"] = $"dotnet-{Platform}" }, new { message = "hello", count = 2 }),
            "json-values" => ("cloud-demo-json-values", HttpMethodEnum.Post, null, null),
            "null-contract" => ("cloud-demo-null-contract", HttpMethodEnum.Get, null, null),
            "response-shapes" => ("cloud-demo-response-shapes", HttpMethodEnum.Post, null, null),
            "error-response" => ("cloud-demo-error-response", HttpMethodEnum.Post, null, new { invalid = true }),
            "timeout-10s" => ("cloud-demo-timeout-10s", HttpMethodEnum.Get, null, null),
            "runtime-context" => ("cloud-demo-runtime-context", HttpMethodEnum.Get, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };
    }

    private void PrepareResult(string demoId, string slug)
    {
        if (ResultBorder.Parent is Panel currentParent)
        {
            currentParent.Children.Remove(ResultBorder);
        }

        if (_functionCards.TryGetValue(demoId, out var card) && card.Parent is Panel parent)
        {
            var index = parent.Children.IndexOf(card);
            parent.Children.Insert(index + 1, ResultBorder);
        }
        else
        {
            RootPanel.Children.Add(ResultBorder);
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

    private void OnToggleResultClicked(object? sender, RoutedEventArgs e)
    {
        _resultExpanded = !_resultExpanded;
        UpdateResultDisplay();
    }

    private void UpdateResultDisplay()
    {
        if (ResultTextBox == null || ResultToggleButton == null) return;
        ResultTextBox.Text = _resultExpanded
            ? _fullResultText
            : string.Join(Environment.NewLine, _fullResultText.Split('\n').Take(2));
        ResultToggleButton.Content = _resultExpanded ? "⌃" : "⌄";
        ToolTip.SetTip(ResultToggleButton, _resultExpanded ? "Collapse response" : "Expand response");
    }

    private static string FormatResponse(CloudCodeResponse response, double elapsed) =>
        $"HTTP {response.StatusCode}\nDuration: {elapsed:0.00} s\nrequestId: {response.RequestId ?? "none"}\nBody: {JsonText(response.Data)}";

    private static string FormatError(CloudCodeError error, double elapsed) =>
        $"Duration: {elapsed:0.00} s\nrequestId: {error.RequestId ?? "none"}\nHTTP error body: {JsonText(error.Body ?? error.RawBody)}\nError: {error.Message}";

    private static string JsonText(object? value)
    {
        if (value == null) return "null";
        if (value is JValue jValue && jValue.Type == JTokenType.Null) return "null";
        return value is JToken token ? token.ToString(Formatting.Indented) : JsonConvert.SerializeObject(value, Formatting.Indented);
    }

    private static double Elapsed(long started) => (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;

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
