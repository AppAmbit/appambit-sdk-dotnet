using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Text;
using Android.Views;
using Android.Widget;
using AppAmbit;
using AppAmbit.Models.CloudCode;
using AppAmbitTestingApp.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace AppAmbitTestingAppAndroid;

public sealed class CloudCodeView : ScrollView
{
    private readonly Activity _activity;
    private readonly Dictionary<string, Button> _runButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TextView> _resultLabels = new(StringComparer.Ordinal);

    private TextView _databaseStatus = null!;
    private TextView _cmsStatus = null!;
    private ProgressBar _progressBar = null!;
    private EditText _taskTitle = null!;
    private EditText _taskId = null!;
    private EditText _postUuid = null!;
    private EditText _publishTitle = null!;
    private EditText _publishBody = null!;

    private bool _databaseAvailable;
    private bool _databaseTablesReady;
    private bool _isRunning;
    private bool _isVerifyingBackend;
    private bool _hasVerifiedBackend;

    private const string Platform = "android";
    private const int Match = ViewGroup.LayoutParams.MatchParent;
    private const int Wrap = ViewGroup.LayoutParams.WrapContent;

    public CloudCodeView(Activity activity) : base(activity)
    {
        _activity = activity;
        FillViewport = true;
        SetBackgroundColor(Color.White);

        var stack = new LinearLayout(activity) { Orientation = Orientation.Vertical };
        stack.SetPadding(Dp(16), Dp(16), Dp(16), Dp(24));
        AddView(stack, new ViewGroup.LayoutParams(Match, Wrap));

        Add(stack, Label("Cloud Code", 24, Color.Black, true));
        Add(stack, Label("HTTP-triggered functions using the android consumer token.", 14, Color.DarkGray));
        Add(stack, CreateDatabaseCard(), 10);
        Add(stack, CreateCmsCard(), 10);

        Add(stack, Label("Database", 20, Color.Black, true), 8);
        _taskTitle = Input("Task title", "Buy coffee");
        _taskId = Input("Task id for update/delete", string.Empty);
        _taskId.InputType = InputTypes.ClassNumber;
        Add(stack, _taskTitle, 8);
        Add(stack, _taskId, 8);

        string? section = null;
        foreach (var demo in NativeCloudCodeDemoCatalog.Demos)
        {
            if (demo.Id == "setup-database")
                continue;

            if (!string.Equals(section, demo.Section, StringComparison.Ordinal))
            {
                section = demo.Section;
                Add(stack, Label(section, 20, Color.Black, true), 8);

                if (section == "CMS")
                {
                    _postUuid = Input("CMS post UUID (optional)", string.Empty);
                    _publishTitle = Input("Sample title", "Cloud Code sample post");
                    _publishBody = Input("Sample body", "Published through an HTTP Cloud Function.");
                    _publishBody.InputType = InputTypes.ClassText | InputTypes.TextFlagMultiLine;
                    _publishBody.SetMinHeight(Dp(88));
                    Add(stack, _postUuid, 8);
                    Add(stack, _publishTitle, 6);
                    Add(stack, _publishBody, 6);
                }
            }

            Add(stack, CreateDemoCard(demo), 8);
        }

        UpdateButtonStates();
    }

    public void StartVerification()
    {
        if (!_hasVerifiedBackend)
            _ = VerifyBackendAsync();
    }

    private LinearLayout CreateDatabaseCard()
    {
        var card = Card();
        Add(card, Label("Database", 18, Color.Black, true));
        Add(card, Label("Create Database first", 14, Color.Rgb(40, 100, 210)), 4);
        _databaseStatus = Label("Checking...", 14, Color.DarkGray, true);
        Add(card, _databaseStatus, 4);

        _progressBar = new ProgressBar(_activity)
        {
            Indeterminate = true,
            Visibility = ViewStates.Visible
        };
        Add(card, _progressBar, 4);

        var setup = NativeCloudCodeDemoCatalog.Demos[0];
        Add(card, CreateDemoCard(setup), 8);
        return card;
    }

    private LinearLayout CreateCmsCard()
    {
        var card = Card();
        Add(card, Label("CMS", 18, Color.Black, true));
        Add(card, Label("Create Content Type first", 14, Color.Rgb(125, 75, 180)), 4);
        _cmsStatus = Label("Checking...", 14, Color.DarkGray, true);
        Add(card, _cmsStatus, 4);
        return card;
    }

    private LinearLayout CreateDemoCard(NativeCloudCodeDemo demo)
    {
        var card = Card();
        var row = new LinearLayout(_activity) { Orientation = Orientation.Horizontal };
        var info = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
        Add(info, Label(NativeCloudCodeDemoCatalog.Slug(demo.Id, Platform), 14, Color.Black, true));
        Add(info, Label(demo.Detail, 12, Color.Rgb(95, 99, 104)), 2);
        Add(info, Label(demo.Prerequisite, 11, Color.Rgb(122, 122, 122)), 2);
        row.AddView(info, new LinearLayout.LayoutParams(0, Wrap, 1f));

        var button = new Button(_activity)
        {
            Text = "▶ Run"
        };
        button.SetAllCaps(false);
        button.SetTextSize(Android.Util.ComplexUnitType.Sp, 13);
        button.SetTextColor(Color.White);
        button.SetMinHeight(Dp(44));
        button.SetPadding(Dp(12), 0, Dp(12), 0);
        button.Background = RoundedBackground(Color.Rgb(81, 43, 212), 8, Color.Transparent, 0);
        button.Click += (_, _) => RunOrConfirm(demo);
        row.AddView(button, new LinearLayout.LayoutParams(Wrap, Dp(44)) { LeftMargin = Dp(8) });
        Add(card, row);

        var result = Label(string.Empty, 13, Color.Black);
        result.SetPadding(Dp(10), Dp(10), Dp(10), Dp(10));
        result.Background = RoundedBackground(Color.Rgb(248, 248, 248), 8, Color.Rgb(215, 220, 229), 1);
        result.Visibility = ViewStates.Gone;
        Add(card, result, 8);

        _runButtons[demo.Id] = button;
        _resultLabels[demo.Id] = result;
        return card;
    }

    private LinearLayout Card()
    {
        var card = new LinearLayout(_activity) { Orientation = Orientation.Vertical };
        card.SetPadding(Dp(12), Dp(10), Dp(12), Dp(10));
        card.Background = RoundedBackground(Color.Rgb(248, 248, 248), 10, Color.Rgb(215, 220, 229), 1);
        return card;
    }

    private EditText Input(string hint, string value)
    {
        var input = new EditText(_activity)
        {
            Hint = hint,
            Text = value
        };
        input.SetSingleLine(true);
        input.SetTextSize(Android.Util.ComplexUnitType.Sp, 14);
        input.SetTextColor(Color.Black);
        input.SetHintTextColor(Color.Rgb(117, 117, 117));
        input.SetPadding(Dp(12), 0, Dp(12), 0);
        input.Background = RoundedBackground(Color.White, 8, Color.Rgb(215, 220, 229), 1);
        input.SetMinHeight(Dp(44));
        return input;
    }

    private TextView Label(string text, float size, Color color, bool bold = false)
    {
        var label = new TextView(_activity) { Text = text };
        label.SetTextSize(Android.Util.ComplexUnitType.Sp, size);
        label.SetTextColor(color);
        label.SetIncludeFontPadding(true);
        if (bold)
            label.SetTypeface(Android.Graphics.Typeface.Default, TypefaceStyle.Bold);
        return label;
    }

    private void Add(LinearLayout parent, View child, int topMarginDp = 0)
    {
        var parameters = new LinearLayout.LayoutParams(Match, Wrap)
        {
            TopMargin = Dp(topMarginDp)
        };
        parent.AddView(child, parameters);
    }

    private void RunOrConfirm(NativeCloudCodeDemo demo)
    {
        if (_isRunning || _isVerifyingBackend)
            return;
        if (demo.Id == "setup-database" && (!_databaseAvailable || _databaseTablesReady))
            return;

        if (!demo.RequiresConfirmation)
        {
            _ = RunDemoAsync(demo);
            return;
        }

        new AlertDialog.Builder(_activity)
            .SetTitle("Confirm Cloud Code action")
            .SetMessage("This calls a real backend operation. Continue only if the required service is configured.")
            .SetNegativeButton("Cancel", (s, e) => { })
            .SetPositiveButton("Run", (s, e) => _ = RunDemoAsync(demo))
            .Show();
    }

    private async Task RunDemoAsync(NativeCloudCodeDemo demo)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        UpdateButtonStates();
        SetResult(demo.Id, "Calling...", false);
        var started = Stopwatch.GetTimestamp();
        try
        {
            var taskId = int.TryParse(_taskId.Text?.Trim(), out var parsedTaskId) ? parsedTaskId : 1;
            var request = NativeCloudCodeDemoCatalog.Configure(
                demo.Id,
                Platform,
                string.IsNullOrWhiteSpace(_taskTitle.Text) ? "Buy coffee" : _taskTitle.Text!.Trim(),
                taskId,
                _postUuid.Text,
                string.IsNullOrWhiteSpace(_publishTitle.Text) ? "Cloud Code sample post" : _publishTitle.Text!.Trim(),
                string.IsNullOrWhiteSpace(_publishBody.Text) ? "Published through an HTTP Cloud Function." : _publishBody.Text!.Trim());

            var response = await CloudCode.Call(request.Slug, request.Method, request.Query, request.Body, Headers());
            SetResult(demo.Id, FormatResponse(response, Elapsed(started)), false);
            if (demo.Id == "setup-database")
                await VerifyBackendAsync();
        }
        catch (CloudCodeError error)
        {
            SetResult(demo.Id, FormatError(error, Elapsed(started)), true);
        }
        catch (Exception error)
        {
            SetResult(demo.Id, $"Duration: {Elapsed(started):0.00} s\nError: {error.Message}", true);
        }
        finally
        {
            _isRunning = false;
            UpdateButtonStates();
        }
    }

    private async Task VerifyBackendAsync()
    {
        if (_isVerifyingBackend)
            return;

        _isVerifyingBackend = true;
        _hasVerifiedBackend = true;
        UpdateButtonStates();
        RunOnUi(() =>
        {
            _progressBar.Visibility = ViewStates.Visible;
            _databaseStatus.Text = "Checking...";
            _cmsStatus.Text = "Checking...";
            _databaseStatus.SetTextColor(Color.DarkGray);
            _cmsStatus.SetTextColor(Color.DarkGray);
        });

        try
        {
            var request = NativeCloudCodeDemoCatalog.Configure("dashboard-summary", Platform, "", 1, null, "", "");
            var response = await CloudCode.Call(request.Slug, request.Method, request.Query, request.Body, Headers());
            var summary = ToObject(response.Data);
            _databaseAvailable = summary?[(object)"database_available"]?.Value<bool>() == true;
            _databaseTablesReady = summary?[(object)"database_tables_ready"]?.Value<bool>() == true;
            var cmsAvailable = summary?[(object)"posts"] != null;

            RunOnUi(() =>
            {
                SetAvailability(_databaseStatus, _databaseAvailable);
                SetAvailability(_cmsStatus, cmsAvailable);
            });
        }
        catch (Exception error)
        {
            _databaseAvailable = false;
            _databaseTablesReady = false;
            RunOnUi(() =>
            {
                _databaseStatus.Text = "Not available";
                _databaseStatus.SetTextColor(Color.DarkGray);
                _cmsStatus.Text = $"Not available ({error.Message})";
                _cmsStatus.SetTextColor(Color.DarkGray);
            });
        }
        finally
        {
            _isVerifyingBackend = false;
            RunOnUi(() => _progressBar.Visibility = ViewStates.Gone);
            UpdateButtonStates();
        }
    }

    private static void SetAvailability(TextView label, bool available)
    {
        label.Text = available ? "Available" : "Not available";
        label.SetTextColor(available ? Color.Rgb(46, 125, 50) : Color.DarkGray);
    }

    private void UpdateButtonStates()
    {
        RunOnUi(() =>
        {
            foreach (var pair in _runButtons)
            {
                var enabled = !_isRunning && !_isVerifyingBackend;
                if (pair.Key == "setup-database")
                    enabled = enabled && _databaseAvailable && !_databaseTablesReady;
                pair.Value.Enabled = enabled;
                pair.Value.Alpha = enabled ? 1f : 0.45f;
            }
        });
    }

    private void SetResult(string id, string text, bool isError)
    {
        RunOnUi(() =>
        {
            if (!_resultLabels.TryGetValue(id, out var label))
                return;
            label.Visibility = ViewStates.Visible;
            label.Text = text;
            label.SetTextColor(isError ? Color.Rgb(198, 40, 40) : Color.Black);
        });
    }

    private IReadOnlyDictionary<string, string> Headers() =>
        new Dictionary<string, string> { ["X-Sample-Client"] = $"dotnet-{Platform}" };

    private static JObject? ToObject(object? value)
    {
        if (value is JObject objectValue)
            return objectValue;
        if (value is JToken token && token.Type == JTokenType.Object)
            return (JObject)token;
        return value == null ? null : JObject.FromObject(value);
    }

    private static string FormatResponse(CloudCodeResponse response, double elapsed) =>
        $"HTTP {response.StatusCode}\nDuration: {elapsed:0.00} s\nrequestId: {response.RequestId ?? "none"}\nBody: {JsonText(response.Data)}";

    private static string FormatError(CloudCodeError error, double elapsed) =>
        $"Duration: {elapsed:0.00} s\nrequestId: {error.RequestId ?? "none"}\nHTTP error body: {JsonText(error.Body ?? error.RawBody)}\nError: {error.Message}";

    private static string JsonText(object? value)
    {
        if (value == null)
            return "null";
        if (value is JValue jValue && jValue.Type == JTokenType.Null)
            return "null";
        return value is JToken token
            ? token.ToString(Formatting.Indented)
            : JsonConvert.SerializeObject(value, Formatting.Indented);
    }

    private void RunOnUi(Action action)
    {
        if (!_activity.IsFinishing)
            _activity.RunOnUiThread(action);
    }

    private int Dp(int value) => (int)(value * Resources!.DisplayMetrics!.Density + 0.5f);

    private static double Elapsed(long started) =>
        (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;

    private GradientDrawable RoundedBackground(Color fill, float radiusDp, Color stroke, int strokeDp)
    {
        var background = new GradientDrawable();
        background.SetColor(fill);
        background.SetCornerRadius(Dp((int)radiusDp));
        if (stroke != Color.Transparent && strokeDp > 0)
            background.SetStroke(Dp(strokeDp), stroke);
        return background;
    }
}
