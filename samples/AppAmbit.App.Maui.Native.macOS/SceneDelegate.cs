// SceneDelegate.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Foundation;
using UIKit;
using AppAmbitMaui;

namespace AppAmbitTestingMacOs;

[Register("SceneDelegate")]
public class SceneDelegate : UIResponder, IUIWindowSceneDelegate
{
    [Export("window")]
    public UIWindow? Window { get; set; }

    static UINavigationController? _crashesNav;
    static UINavigationController? _analyticsNav;

    [Export("scene:willConnectToSession:options:")]
    public void WillConnect(UIScene scene, UISceneSession session, UISceneConnectionOptions connectionOptions)
    {
        if (scene is UIWindowScene windowScene)
        {
            Window ??= new UIWindow(windowScene);

            var crashesViewController = CreateCrashesViewController();
            var analyticsViewController = CreateAnalyticsViewController();
            var remoteConfigViewController = new RemoteConfigViewController();

            _crashesNav = new UINavigationController(crashesViewController);
            _crashesNav.TabBarItem = new UITabBarItem("Crashes", UIImage.GetSystemImage("exclamationmark.triangle"), 0);

            _analyticsNav = new UINavigationController(analyticsViewController);
            _analyticsNav.TabBarItem = new UITabBarItem("Analytics", UIImage.GetSystemImage("chart.bar"), 1);

            var remoteConfigNav = new UINavigationController(remoteConfigViewController);
            remoteConfigNav.TabBarItem = new UITabBarItem("Remote Config", UIImage.GetSystemImage("antenna.radiowaves.left.and.right"), 2);

            var databaseNav = new UINavigationController(new DatabaseViewController());
            databaseNav.TabBarItem = new UITabBarItem("DB", UIImage.GetSystemImage("externaldrive"), 3);

            var tabBarController = new UITabBarController
            {
                ViewControllers = new UIViewController[]
                {
                    _crashesNav,
                    _analyticsNav,
                    remoteConfigNav,
                    databaseNav
                }
            };

            Window.RootViewController = tabBarController;
            Window.MakeKeyAndVisible();
        }
    }

    private static UIButton MakeButton(string title)
    {
        var b = new UIButton(UIButtonType.System);
        b.SetTitle(title, UIControlState.Normal);
        b.TranslatesAutoresizingMaskIntoConstraints = false;
        b.BackgroundColor = UIColor.SystemBlue;
        b.SetTitleColor(UIColor.White, UIControlState.Normal);
        b.Layer.CornerRadius = 8;
        b.HeightAnchor.ConstraintEqualTo(44).Active = true;
        return b;
    }

    private static UITextField MakeTextField(string placeholder, string initialText = "")
    {
        var tf = new UITextField
        {
            Placeholder = placeholder,
            BorderStyle = UITextBorderStyle.RoundedRect,
            TranslatesAutoresizingMaskIntoConstraints = false,
            Text = initialText
        };
        tf.HeightAnchor.ConstraintEqualTo(32).Active = true;
        return tf;
    }

    private static void ShowAlert(UIViewController vc, string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        vc.PresentViewController(alert, true, null);
    }

    private UIViewController CreateCrashesViewController()
    {
        var vc = new UIViewController();
        vc.View!.BackgroundColor = UIColor.SystemBackground;

        var userIdField = MakeTextField("User id", Guid.NewGuid().ToString());
        var userEmailField = MakeTextField("User email", "test@gmail.com");
        var logMessageField = MakeTextField("Type log message", "Test Log Message");

        string logMessage = logMessageField.Text ?? "Test Log Message";

        logMessageField.EditingChanged += (s, e) =>
        {
            logMessage = logMessageField.Text ?? "";
        };

        var btnDidCrash = MakeButton("Did the app crash during your last session?");
        btnDidCrash.TouchUpInside += async (s, e) =>
        {
            var did = await Crashes.DidCrashInLastSession();
            ShowAlert(vc, "Info", did ? "Application crashed in the last session" : "Application did not crash in the last session");
        };

        var btnChangeUserId = MakeButton("Change user id");
        btnChangeUserId.TouchUpInside += (s, e) =>
        {
            Analytics.SetUserId(userIdField.Text ?? "");
            ShowAlert(vc, "Info", "User id changed");
        };

        var btnChangeUserEmail = MakeButton("Change user email");
        btnChangeUserEmail.TouchUpInside += (s, e) =>
        {
            Analytics.SetUserEmail(userEmailField.Text ?? "");
            ShowAlert(vc, "Info", "User email changed");
        };

        var btnCustomLogError = MakeButton("Send Custom LogError");
        btnCustomLogError.TouchUpInside += async (s, e) =>
        {
            await Crashes.LogError(logMessage);
            ShowAlert(vc, "Info", "LogError Sent");
        };

        var btnDefaultLogError = MakeButton("Send Default LogError");
        btnDefaultLogError.TouchUpInside += async (s, e) =>
        {
            await Crashes.LogError("Test Log Error", new Dictionary<string, string> { { "user_id", "1" } });
            ShowAlert(vc, "Info", "LogError Sent");
        };

        var btnSendExceptionLogError = MakeButton("Send Exception LogError");
        btnSendExceptionLogError.TouchUpInside += async (s, e) =>
        {
            try
            {
                throw new NullReferenceException();
            }
            catch (Exception exception)
            {
                await Crashes.LogError(exception, new Dictionary<string, string> { { "user_id", "1" } });
                ShowAlert(vc, "Info", "LogError Sent");
            }
        };

        var btnClassFqnLogError = MakeButton("Send ClassInfo LogError");
        btnClassFqnLogError.TouchUpInside += async (s, e) =>
        {
            await Crashes.LogError(
                "Test Log Error",
                new Dictionary<string, string> { { "user_id", "1" } },
                GetType().FullName
            );
            ShowAlert(vc, "Info", "LogError Sent");
        };

        var btnThrowCrash = MakeButton("Throw new Crash");
        btnThrowCrash.TouchUpInside += (s, e) =>
        {
            var arr = new int[0];
            _ = arr[10];
        };

        var btnGenerateTestCrash = MakeButton("Generate Test Crash");
        btnGenerateTestCrash.TouchUpInside += (s, e) =>
        {
            _ = Crashes.GenerateTestCrash();
            ShowAlert(vc, "Info", "LogError Sent");
        };

        var stack = new UIStackView(new UIView[]
        {
            btnDidCrash,
            userIdField,
            btnChangeUserId,
            userEmailField,
            btnChangeUserEmail,
            logMessageField,
            btnCustomLogError,
            btnDefaultLogError,
            btnSendExceptionLogError,
            btnClassFqnLogError,
            btnThrowCrash,
            btnGenerateTestCrash
        })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.Fill,
            Spacing = 12,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        vc.View!.AddSubview(stack);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stack.TopAnchor.ConstraintEqualTo(vc.View.SafeAreaLayoutGuide.TopAnchor, 24),
            stack.CenterXAnchor.ConstraintEqualTo(vc.View.CenterXAnchor),
            stack.WidthAnchor.ConstraintEqualTo(vc.View.WidthAnchor, 0.4f)
        });

        return vc;
    }

    private UIViewController CreateAnalyticsViewController()
    {
        var vc = new UIViewController();
        vc.View!.BackgroundColor = UIColor.SystemBackground;

        var btnInvalidateToken = MakeButton("Invalidate Token");
        btnInvalidateToken.TouchUpInside += (s, e) =>
        {
            Analytics.ClearToken();
            ShowAlert(vc, "Info", "Token cleared");
        };

        var btnTokenRefreshTest = MakeButton("Token refresh test");
        btnTokenRefreshTest.TouchUpInside += async (s, e) =>
        {
            Analytics.ClearToken();

            var logsTask = Enumerable.Range(0, 5).Select(
                _ => Task.Run(() =>
                {
                    Crashes.LogError("Sending 5 errors after an invalid token");
                }));

            var eventsTask = Enumerable.Range(0, 5).Select(
                _ => Task.Run(() =>
                {
                    Analytics.TrackEvent(
                        "Sending 5 events after an invalid token",
                        new Dictionary<string, string>
                        {
                            { "Test Token", "5 events sent" }
                        });
                }));

            await Task.WhenAll(logsTask);
            Analytics.ClearToken();
            await Task.WhenAll(eventsTask);

            ShowAlert(vc, "Info", "5 events and errors sent");
        };

        var btnStartSession = MakeButton("Start Session");
        btnStartSession.TouchUpInside += async (s, e) =>
        {
            await Analytics.StartSession();
            ShowAlert(vc, "Info", "Session started");
        };

        var btnEndSession = MakeButton("End Session");
        btnEndSession.TouchUpInside += async (s, e) =>
        {
            await Analytics.EndSession();
            ShowAlert(vc, "Info", "Session ended");
        };

        var btnClickedEvent = MakeButton("Send \"Button Clicked\" Event w/ property");
        btnClickedEvent.TouchUpInside += async (s, e) =>
        {
            await Analytics.TrackEvent("ButtonClicked", new Dictionary<string, string> { { "Count", "41" } });
            ShowAlert(vc, "Info", "Event sent");
        };

        var btnDefaultEvent = MakeButton("Send Default Event w/ property");
        btnDefaultEvent.TouchUpInside += async (s, e) =>
        {
            await Analytics.GenerateTestEvent();
            ShowAlert(vc, "Info", "Default event sent");
        };

        var btnMax300Event = MakeButton("Send Max-300-Length Event");
        btnMax300Event.TouchUpInside += async (s, e) =>
        {
            var _300Characters = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            var _300Characters2 = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678902";

            var properties = new Dictionary<string, string>
            {
                { _300Characters, _300Characters },
                { _300Characters2, _300Characters2 }
            };

            await Analytics.TrackEvent(_300Characters, properties);
            ShowAlert(vc, "Info", "Max-length event sent");
        };

        var btnMax20PropsEvent = MakeButton("Send Max-20-Properties Event");
        btnMax20PropsEvent.TouchUpInside += async (s, e) =>
        {
            var properties = new Dictionary<string, string>
            {
                { "01", "01"},
                { "02", "02"},
                { "03", "03"},
                { "04", "04"},
                { "05", "05"},
                { "06", "06"},
                { "07", "07"},
                { "08", "08"},
                { "09", "09"},
                { "10", "10"},
                { "11", "11"},
                { "12", "12"},
                { "13", "13"},
                { "14", "14"},
                { "15", "15"},
                { "16", "16"},
                { "17", "17"},
                { "18", "18"},
                { "19", "19"},
                { "20", "20"},
                { "21", "21"},
                { "22", "22"},
                { "23", "23"},
                { "24", "24"},
                { "25", "25"},
            };

            await Analytics.TrackEvent("TestMaxProperties", properties);
            ShowAlert(vc, "Info", "Max-properties event sent");
        };

        var btnBatchEvents = MakeButton("Send Batch of 220 Events");
        btnBatchEvents.TouchUpInside += async (s, e) =>
        {
            ShowAlert(vc, "Info", "Turn off internet");
            foreach (var index in Enumerable.Range(1, 220))
            {
                await Analytics.TrackEvent("Test Batch TrackEvent", new Dictionary<string, string> { { "test1", "test1" } });
            }
            ShowAlert(vc, "Info", "Events generated");
            ShowAlert(vc, "Info", "Turn on internet to send the events");
        };

        var btnGoSecond = MakeButton("Change to Second Page");
        btnGoSecond.TouchUpInside += OnGoToSecondPage;

        var stack = new UIStackView(new UIView[]
        {
            btnInvalidateToken,
            btnTokenRefreshTest,
            btnStartSession,
            btnEndSession,
            btnClickedEvent,
            btnDefaultEvent,
            btnMax300Event,
            btnMax20PropsEvent,
            btnBatchEvents,
            btnGoSecond
        })
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.Fill,
            Spacing = 12,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        vc.View!.AddSubview(stack);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            stack.TopAnchor.ConstraintEqualTo(vc.View.SafeAreaLayoutGuide.TopAnchor, 24),
            stack.CenterXAnchor.ConstraintEqualTo(vc.View.CenterXAnchor),
            stack.WidthAnchor.ConstraintEqualTo(vc.View.WidthAnchor, 0.4f)
        });

        return vc;
    }


    private async void OnGoToSecondPage(object? sender, EventArgs e)
    {
        if (_analyticsNav == null) return;

        var second = new SecondViewController();
        _analyticsNav.PushViewController(second, true);

        await Task.CompletedTask;
    }

    [Export("sceneDidDisconnect:")]
    public void DidDisconnect(UIScene scene)
    {
    }

    [Export("sceneDidBecomeActive:")]
    public void DidBecomeActive(UIScene scene)
    {
    }

    [Export("sceneWillResignActive:")]
    public void WillResignActive(UIScene scene)
    {
    }

    [Export("sceneWillEnterForeground:")]
    public void WillEnterForeground(UIScene scene)
    {
    }

    [Export("sceneDidEnterBackground:")]
    public void DidEnterBackground(UIScene scene)
    {
    }
}
