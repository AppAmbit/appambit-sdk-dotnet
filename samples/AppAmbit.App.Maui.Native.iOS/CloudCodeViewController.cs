using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using AppAmbit;
using AppAmbit.Models.CloudCode;
using AppAmbitTestingApp.Shared;
using CoreGraphics;
using Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UIKit;

namespace AppAmbitTestingiOS;

public sealed class CloudCodeViewController : UIViewController
{
    private readonly Dictionary<string, UIButton> _runButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, UILabel> _resultLabels = new(StringComparer.Ordinal);

    private UIScrollView _scrollView = null!;
    private UIStackView _stack = null!;
    private UILabel _databaseStatus = null!;
    private UILabel _cmsStatus = null!;
    private UIActivityIndicatorView _spinner = null!;
    private UITextField _taskTitle = null!;
    private UITextField _taskId = null!;
    private UITextField _postUuid = null!;
    private UITextField _publishTitle = null!;
    private UITextView _publishBody = null!;

    private bool _databaseAvailable;
    private bool _databaseTablesReady;
    private bool _isRunning;
    private bool _isVerifyingBackend;
    private bool _hasVerifiedBackend;

    private const string Platform = "ios";

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        Title = "Cloud Code";
        View!.BackgroundColor = NativeTheme.Surface;
        BuildUi();
        UpdateButtonStates();
    }

    public override void ViewDidAppear(bool animated)
    {
        base.ViewDidAppear(animated);
        if (!_hasVerifiedBackend)
            _ = VerifyBackendAsync();
    }

    private void BuildUi()
    {
        _scrollView = new UIScrollView { TranslatesAutoresizingMaskIntoConstraints = false };
        _stack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Spacing = 10,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        View.AddSubview(_scrollView);
        _scrollView.AddSubview(_stack);

        var guide = View.SafeAreaLayoutGuide;
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            _scrollView.TopAnchor.ConstraintEqualTo(guide.TopAnchor),
            _scrollView.LeadingAnchor.ConstraintEqualTo(View.LeadingAnchor),
            _scrollView.TrailingAnchor.ConstraintEqualTo(View.TrailingAnchor),
            _scrollView.BottomAnchor.ConstraintEqualTo(guide.BottomAnchor),
            _stack.TopAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.TopAnchor, 16),
            _stack.LeadingAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.LeadingAnchor, 16),
            _stack.TrailingAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.TrailingAnchor, -16),
            _stack.BottomAnchor.ConstraintEqualTo(_scrollView.ContentLayoutGuide.BottomAnchor, -24),
            _stack.WidthAnchor.ConstraintEqualTo(_scrollView.FrameLayoutGuide.WidthAnchor, 1, -32)
        });

        _stack.AddArrangedSubview(Label("Cloud Code", UIFont.BoldSystemFontOfSize(24), UIColor.Label));
        _stack.AddArrangedSubview(Label("HTTP-triggered functions using the ios consumer token.", UIFont.SystemFontOfSize(14), UIColor.SecondaryLabel));
        _stack.AddArrangedSubview(CreateDatabaseCard());
        _stack.AddArrangedSubview(CreateCmsCard());

        _stack.AddArrangedSubview(Label("Database", UIFont.BoldSystemFontOfSize(20), UIColor.Label));
        _taskTitle = CreateTextField("Task title", "Buy coffee");
        _taskId = CreateTextField("Task id for update/delete", string.Empty);
        _taskId.KeyboardType = UIKeyboardType.NumberPad;
        _stack.AddArrangedSubview(_taskTitle);
        _stack.AddArrangedSubview(_taskId);

        string? section = null;
        foreach (var demo in NativeCloudCodeDemoCatalog.Demos)
        {
            if (demo.Id == "setup-database")
                continue;

            if (!string.Equals(section, demo.Section, StringComparison.Ordinal))
            {
                section = demo.Section;
                if (section != "Database")
                    _stack.AddArrangedSubview(Label(section, UIFont.BoldSystemFontOfSize(20), UIColor.Label));

                if (section == "CMS")
                {
                    _postUuid = CreateTextField("CMS post UUID (optional)", string.Empty);
                    _publishTitle = CreateTextField("Sample title", "Cloud Code sample post");
                    _publishBody = new UITextView
                    {
                        Text = "Published through an HTTP Cloud Function.",
                        Font = UIFont.SystemFontOfSize(14),
                        BackgroundColor = UIColor.White,
                        TextColor = UIColor.Label,
                        TranslatesAutoresizingMaskIntoConstraints = false
                    };
                    _publishBody.Layer.CornerRadius = 8;
                    _publishBody.Layer.BorderColor = UIColor.FromRGB(215, 220, 229).CGColor;
                    _publishBody.Layer.BorderWidth = 1;
                    _publishBody.TextContainerInset = new UIEdgeInsets(10, 8, 10, 8);
                    _publishBody.HeightAnchor.ConstraintEqualTo(80).Active = true;
                    _stack.AddArrangedSubview(_postUuid);
                    _stack.AddArrangedSubview(_publishTitle);
                    _stack.AddArrangedSubview(_publishBody);
                }
            }

            _stack.AddArrangedSubview(CreateDemoCard(demo));
        }
    }

    private UIView CreateDatabaseCard()
    {
        var card = CreateCard();
        _databaseStatus = Label("Checking...", UIFont.BoldSystemFontOfSize(14), UIColor.SecondaryLabel);
        _spinner = new UIActivityIndicatorView(UIActivityIndicatorViewStyle.Medium)
        {
            HidesWhenStopped = true,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var content = new UIStackView(new UIView[]
        {
            Label("Database", UIFont.BoldSystemFontOfSize(18), UIColor.Label),
            Label("Create Database first", UIFont.SystemFontOfSize(14), UIColor.FromRGB(40, 100, 210)),
            _databaseStatus,
            _spinner,
            CreateDemoCard(NativeCloudCodeDemoCatalog.Demos[0])
        })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        card.AddSubview(content);
        Pin(content, card);
        return card;
    }

    private UIView CreateCmsCard()
    {
        var card = CreateCard();
        _cmsStatus = Label("Checking...", UIFont.BoldSystemFontOfSize(14), UIColor.SecondaryLabel);
        var content = new UIStackView(new UIView[]
        {
            Label("CMS", UIFont.BoldSystemFontOfSize(18), UIColor.Label),
            Label("Create Content Type first", UIFont.SystemFontOfSize(14), UIColor.FromRGB(125, 75, 180)),
            _cmsStatus
        })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        card.AddSubview(content);
        Pin(content, card);
        return card;
    }

    private UIView CreateDemoCard(NativeCloudCodeDemo demo)
    {
        var card = CreateCard();
        var info = new UIStackView(new UIView[]
        {
            Label(NativeCloudCodeDemoCatalog.Slug(demo.Id, Platform), UIFont.BoldSystemFontOfSize(14), UIColor.Label),
            Label(demo.Detail, UIFont.SystemFontOfSize(12), UIColor.FromRGB(95, 99, 104), 0),
            Label(demo.Prerequisite, UIFont.SystemFontOfSize(11), UIColor.FromRGB(122, 122, 122), 0)
        })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 2,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var button = UIButton.FromType(UIButtonType.System);
        button.SetTitle("▶ Run", UIControlState.Normal);
        button.BackgroundColor = NativeTheme.Primary;
        button.SetTitleColor(UIColor.White, UIControlState.Normal);
        button.Layer.CornerRadius = 8;
        button.HeightAnchor.ConstraintEqualTo(42).Active = true;
        button.WidthAnchor.ConstraintEqualTo(86).Active = true;
        button.TouchUpInside += (_, _) => RunOrConfirm(demo);

        var row = new UIStackView(new UIView[] { info, button })
        {
            Axis = UILayoutConstraintAxis.Horizontal,
            Spacing = 8,
            Alignment = UIStackViewAlignment.Center,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var result = Label(string.Empty, UIFont.SystemFontOfSize(13), UIColor.Label, 0);
        result.BackgroundColor = UIColor.White;
        result.Layer.CornerRadius = 8;
        result.Layer.BorderColor = UIColor.FromRGB(215, 220, 229).CGColor;
        result.Layer.BorderWidth = 1;
        result.Hidden = true;

        var content = new UIStackView(new UIView[] { row, result })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Spacing = 8,
            Alignment = UIStackViewAlignment.Fill,
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        card.AddSubview(content);
        Pin(content, card);

        _runButtons[demo.Id] = button;
        _resultLabels[demo.Id] = result;
        return card;
    }

    private static UIView CreateCard()
    {
        var card = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        card.BackgroundColor = UIColor.FromRGB(248, 248, 248);
        card.Layer.CornerRadius = 10;
        card.Layer.BorderColor = UIColor.FromRGB(215, 220, 229).CGColor;
        card.Layer.BorderWidth = 1;
        return card;
    }

    private static UILabel Label(string text, UIFont font, UIColor color, nint lines = 1) => new()
    {
        Text = text,
        Font = font,
        TextColor = color,
        Lines = lines,
        LineBreakMode = UILineBreakMode.WordWrap,
        TranslatesAutoresizingMaskIntoConstraints = false
    };

    private static UITextField CreateTextField(string placeholder, string value)
    {
        var field = new UITextField
        {
            Placeholder = placeholder,
            Text = value,
            BorderStyle = UITextBorderStyle.RoundedRect,
            Font = UIFont.SystemFontOfSize(14),
            TranslatesAutoresizingMaskIntoConstraints = false
        };
        field.HeightAnchor.ConstraintEqualTo(44).Active = true;
        return field;
    }

    private static void Pin(UIView child, UIView parent)
    {
        NSLayoutConstraint.ActivateConstraints(new[]
        {
            child.TopAnchor.ConstraintEqualTo(parent.TopAnchor, 12),
            child.LeadingAnchor.ConstraintEqualTo(parent.LeadingAnchor, 12),
            child.TrailingAnchor.ConstraintEqualTo(parent.TrailingAnchor, -12),
            child.BottomAnchor.ConstraintEqualTo(parent.BottomAnchor, -12)
        });
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

        var alert = UIAlertController.Create(
            "Confirm Cloud Code action",
            "This calls a real backend operation. Continue only if the required service is configured.",
            UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("Cancel", UIAlertActionStyle.Cancel, null));
        alert.AddAction(UIAlertAction.Create("Run", UIAlertActionStyle.Default, action =>
        {
            _ = RunDemoAsync(demo);
        }));
        PresentViewController(alert, true, null);
    }

    private async Task RunDemoAsync(NativeCloudCodeDemo demo)
    {
        if (_isRunning)
            return;

        _isRunning = true;
        UpdateButtonStates();
        var started = Stopwatch.GetTimestamp();
        SetResult(demo.Id, "Calling...", false);
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
                string.IsNullOrWhiteSpace(_publishBody.Text) ? "Published through an HTTP Cloud Function." : _publishBody.Text.Trim());

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
        InvokeOnMainThread(() =>
        {
            _spinner.StartAnimating();
            _databaseStatus.Text = "Checking...";
            _cmsStatus.Text = "Checking...";
            _databaseStatus.TextColor = UIColor.SecondaryLabel;
            _cmsStatus.TextColor = UIColor.SecondaryLabel;
        });

        try
        {
            var request = NativeCloudCodeDemoCatalog.Configure("dashboard-summary", Platform, "", 1, null, "", "");
            var response = await CloudCode.Call(request.Slug, request.Method, request.Query, request.Body, Headers());
            var summary = ToObject(response.Data);
            _databaseAvailable = summary?["database_available"]?.Value<bool>() == true;
            _databaseTablesReady = summary?["database_tables_ready"]?.Value<bool>() == true;
            var cmsAvailable = summary?["posts"] != null;

            InvokeOnMainThread(() =>
            {
                SetAvailability(_databaseStatus, _databaseAvailable);
                SetAvailability(_cmsStatus, cmsAvailable);
            });
        }
        catch (Exception error)
        {
            _databaseAvailable = false;
            _databaseTablesReady = false;
            InvokeOnMainThread(() =>
            {
                _databaseStatus.Text = "Not available";
                _databaseStatus.TextColor = UIColor.SecondaryLabel;
                _cmsStatus.Text = $"Not available ({error.Message})";
                _cmsStatus.TextColor = UIColor.SecondaryLabel;
            });
        }
        finally
        {
            _isVerifyingBackend = false;
            InvokeOnMainThread(() => _spinner.StopAnimating());
            UpdateButtonStates();
        }
    }

    private static void SetAvailability(UILabel label, bool available)
    {
        label.Text = available ? "Available" : "Not available";
        label.TextColor = available ? UIColor.SystemGreen : UIColor.SecondaryLabel;
    }

    private void UpdateButtonStates()
    {
        InvokeOnMainThread(() =>
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
        InvokeOnMainThread(() =>
        {
            if (!_resultLabels.TryGetValue(id, out var label))
                return;
            label.Hidden = false;
            label.Text = text;
            label.TextColor = isError ? UIColor.SystemRed : UIColor.Label;
        });
    }

    private static IReadOnlyDictionary<string, string> Headers() =>
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

    private static double Elapsed(long started) =>
        (Stopwatch.GetTimestamp() - started) / (double)Stopwatch.Frequency;
}
