using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AppAmbit;
using Foundation;
using UIKit;

namespace AppAmbitTestingiOS;

public class AnalyticsViewController : UIViewController
{
    UIColor Gray6Compat()
    {
        if (OperatingSystem.IsIOSVersionAtLeast(13)) return UIColor.SystemGray6;
        return UIColor.FromRGB(242, 242, 247);
    }

    UIButton MakeButton(string title)
    {
        var b = new UIButton(UIButtonType.System);
        b.SetTitle(title, UIControlState.Normal);
        b.BackgroundColor = UIColor.SystemBlue;
        b.SetTitleColor(UIColor.White, UIControlState.Normal);
        b.Layer.CornerRadius = 8;
        if (!OperatingSystem.IsIOSVersionAtLeast(15))
            b.ContentEdgeInsets = new UIEdgeInsets(12, 12, 12, 12);
        return b;
    }

    UIButton MakeDisabledGray(string title)
    {
        var b = new UIButton(UIButtonType.System);
        b.SetTitle(title, UIControlState.Normal);
        b.BackgroundColor = UIColor.FromRGB(96, 120, 141);
        b.SetTitleColor(Gray6Compat(), UIControlState.Normal);
        b.Layer.CornerRadius = 8;
        if (!OperatingSystem.IsIOSVersionAtLeast(15))
            b.ContentEdgeInsets = new UIEdgeInsets(12, 12, 12, 12);
        b.Enabled = false;
        return b;
    }

    void ShowAlert(string title, string message)
    {
        var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
        alert.AddAction(UIAlertAction.Create("OK", UIAlertActionStyle.Default, null));
        PresentViewController(alert, true, null);
    }

    public override void ViewDidLoad()
    {
        base.ViewDidLoad();
        View.BackgroundColor = UIColor.SystemBackground;
        Title = "AnalyticsView";

        var scroll = new UIScrollView { TranslatesAutoresizingMaskIntoConstraints = false };
        var stack = new UIStackView
        {
            Axis = UILayoutConstraintAxis.Vertical,
            Alignment = UIStackViewAlignment.Fill,
            Distribution = UIStackViewDistribution.Fill,
            Spacing = 25,
            TranslatesAutoresizingMaskIntoConstraints = false
        };

        var btnInvalidate = MakeButton("Invalidate Token");
        btnInvalidate.TouchUpInside += (s, e) =>
        {
            Analytics.ClearToken();
        };

        var btnTokenRefresh = MakeButton("Token refresh test");
        btnTokenRefresh.TouchUpInside += async (s, e) =>
        {
            var props = new Dictionary<string, string> { { "user_id", "1" } };

            var logsTasks = Enumerable.Range(1, 5).Select(async i =>
            {
                await Crashes.LogError("Sending logs 5 after invalid token", props, "AnalyticsView");
                Debug.WriteLine($"Log {i} recorded successfully");
            });
            await Task.WhenAll(logsTasks);

            var serial = new SemaphoreSlim(1, 1);
            for (int i = 1; i <= 5; i++)
            {
                await serial.WaitAsync();
                await Analytics.TrackEvent(
                    "Sending event 5 after invalid token",
                    new Dictionary<string, string> { { "Test Token", "5 events sent" } }
                );
                Debug.WriteLine($"Event {i} tracked successfully");
                serial.Release();
            }

            ShowAlert("Info", "5 events and 5 errors sent");
        };

        var btnStartSession = MakeButton("Start Session");
        btnStartSession.TouchUpInside += async (s, e) =>
        {
            await Analytics.StartSession();
            Debug.WriteLine("Successful Start Session");
        };

        var btnEndSession = MakeButton("End Session");
        btnEndSession.TouchUpInside += async (s, e) =>
        {
            await Analytics.EndSession();
            Debug.WriteLine("Successful End Session");
        };

        var btnEventWithProp = MakeButton("Send 'Button Clicked' Event w/ property");
        btnEventWithProp.TouchUpInside += async (s, e) =>
        {
            await Analytics.TrackEvent("ButtonClicked", new Dictionary<string, string> { { "Count", "41" } });
            Debug.WriteLine("Event sent successfully");
        };

        var btnDefaultEvent = MakeButton("Send Default Event w/ property");
        btnDefaultEvent.TouchUpInside += async (s, e) =>
        {
            await Analytics.GenerateTestEvent();
            Debug.WriteLine("Event sent successfully");
        };

        var btn300 = MakeButton("Send Max-300-Length Event");
        btn300.TouchUpInside += async (s, e) =>
        {
            var _300Characters = "123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890";
            var _300Characters2 = "1234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678902";

            var properties = new Dictionary<string, string>
            {
                { _300Characters, _300Characters },
                { _300Characters2, _300Characters2 }
            };

            await Analytics.TrackEvent(_300Characters, properties);
            Debug.WriteLine("Event sent successfully");
        };

        var btnMaxProps = MakeButton("Send Max-20-Properties Event");
        btnMaxProps.TouchUpInside += async (s, e) =>
        {
            var props = Enumerable.Range(1, 25).ToDictionary(i => i.ToString("00"), i => i.ToString("00"));
            await Analytics.TrackEvent("TestMaxProperties", props);
            Debug.WriteLine("Event sent successfully");
        };

        var btnGoSecond = MakeButton("Change to Second Activity");
        btnGoSecond.TouchUpInside += (s, e) =>
        {
            var second = new SecondView();

            var top = GetTopViewController();
            if (top == null)
                return;

            var nav = top.NavigationController;
            if (nav != null)
            {
                nav.PushViewController(second, true);
            }
            else if (top is UINavigationController nav2)
            {
                nav2.PushViewController(second, true);
            }
            else
            {
                second.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                top.PresentViewController(second, true, null);
            }
        };

        var container = new UIView { TranslatesAutoresizingMaskIntoConstraints = false };
        var buttons = new[]
        {
            btnInvalidate,
            btnTokenRefresh,
            btnStartSession,
            btnEndSession,
            btnEventWithProp,
            btnDefaultEvent,
            btn300,
            btnMaxProps,
            btnGoSecond
        };

        foreach (var b in buttons) stack.AddArrangedSubview(b);

        View.AddSubview(scroll);
        scroll.AddSubview(container);
        container.AddSubview(stack);

        NSLayoutConstraint.ActivateConstraints(new[]
        {
            scroll.TopAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TopAnchor),
            scroll.LeadingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.LeadingAnchor),
            scroll.TrailingAnchor.ConstraintEqualTo(View.SafeAreaLayoutGuide.TrailingAnchor),
            scroll.BottomAnchor.ConstraintEqualTo(View.BottomAnchor),

            container.TopAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TopAnchor),
            container.LeadingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.LeadingAnchor),
            container.TrailingAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.TrailingAnchor),
            container.BottomAnchor.ConstraintEqualTo(scroll.ContentLayoutGuide.BottomAnchor),
            container.WidthAnchor.ConstraintEqualTo(scroll.FrameLayoutGuide.WidthAnchor),

            stack.TopAnchor.ConstraintEqualTo(container.TopAnchor, 16),
            stack.LeadingAnchor.ConstraintEqualTo(container.LeadingAnchor, 16),
            stack.TrailingAnchor.ConstraintEqualTo(container.TrailingAnchor, -16),
            stack.BottomAnchor.ConstraintEqualTo(container.BottomAnchor, -16),
        });
    }

    UIViewController? GetTopViewController()
    {
        var app = UIApplication.SharedApplication;
        if (app?.ConnectedScenes == null) return null;

        foreach (var scene in app.ConnectedScenes)
        {
            if (scene is UIWindowScene ws)
            {
                foreach (var window in ws.Windows)
                {
                    if (!window.IsKeyWindow) continue;
                    var root = window.RootViewController;
                    if (root == null) continue;
                    return TopViewController(root);
                }
            }
        }

        return null;
    }

    UIViewController TopViewController(UIViewController root)
    {
        if (root.PresentedViewController != null)
            return TopViewController(root.PresentedViewController);

        if (root is UINavigationController nav && nav.VisibleViewController != null)
            return TopViewController(nav.VisibleViewController);

        if (root is UITabBarController tab && tab.SelectedViewController != null)
            return TopViewController(tab.SelectedViewController);

        return root;
    }
}
