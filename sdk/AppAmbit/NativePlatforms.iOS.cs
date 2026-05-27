#if IOS
using System;
using System.Net;
using System.Diagnostics;
using CoreFoundation;
using Foundation;
using SystemConfiguration;
using UIKit;

namespace AppAmbit;

internal static partial class NativePlatforms
{
    private static NSObject? _obsWillEnterForeground;
    private static NSObject? _obsDidEnterBackground;
    private static NSObject? _obsWillTerminate;

    private static NetworkReachability? _reachability;
    private static NetworkReachabilityFlags? _lastFlags;

    private static bool _pagesHooked;
    private static UINavigationControllerDelegate? _navDelegate;
    private static bool _wasBackgrounded;

    static partial void PlatformRegister(string appKey)
    {
        if (_obsWillEnterForeground != null) return;

        var nc = NSNotificationCenter.DefaultCenter;
        _obsDidEnterBackground  = nc.AddObserver(UIApplication.DidEnterBackgroundNotification, OnDidEnterBackground);
        _obsWillEnterForeground = nc.AddObserver(UIApplication.WillEnterForegroundNotification, OnWillEnterForeground);
        _obsWillTerminate       = nc.AddObserver(UIApplication.WillTerminateNotification, OnWillTerminate);

        StartReachability();
        Log("[iOS] PlatformRegister");

        DispatchQueue.MainQueue.DispatchAsync(TryHookIOSNativePages);
    }

    static partial void PlatformEnablePageBreadcrumbs()
    {
        DispatchQueue.MainQueue.DispatchAsync(TryHookIOSNativePages);
    }

    private static void OnDidEnterBackground(NSNotification n)
    {
        Log("[iOS] DidEnterBackground");
        try
        {
            _wasBackgrounded = true;
            AppAmbitSdk.InternalSleep();
        }
        catch (Exception ex)
        {
            Log("[iOS] InternalSleep error: " + ex);
        }
    }

    private static void OnWillEnterForeground(NSNotification n)
    {
        Log("[iOS] WillEnterForeground");
        try
        {
            if (_reachability == null) StartReachability();
            if (_wasBackgrounded)
            {
                _wasBackgrounded = false;
                _ = AppAmbitSdk.InternalResume();
            }

            DispatchQueue.MainQueue.DispatchAsync(TryHookIOSNativePages);
        }
        catch (Exception ex)
        {
            Log("[iOS] InternalResume error: " + ex);
        }
    }

    private static void OnWillTerminate(NSNotification n)
    {
        Log("[iOS] WillTerminate");
        try
        {
            AppAmbitSdk.InternalEnd();
        }
        catch (Exception ex)
        {
            Log("[iOS] InternalEnd error: " + ex);
        }
    }

    private static void StartReachability()
    {
        try
        {
            _reachability?.Unschedule(CFRunLoop.Main, CFRunLoop.ModeDefault);
            _reachability = new NetworkReachability(new IPAddress(0));
            _reachability.SetNotification(ReachabilityFlagsChanged);
            _reachability.Schedule(CFRunLoop.Main, CFRunLoop.ModeDefault);

            if (_reachability.TryGetFlags(out var flags))
            {
                _lastFlags = flags;
                Log("[iOS] Initial reachability flags: " + flags);
            }
        }
        catch (Exception ex)
        {
            Log("[iOS] StartReachability error: " + ex);
        }
    }

    private static void ReachabilityFlagsChanged(NetworkReachabilityFlags flags)
    {
        try
        {
            if (_lastFlags.HasValue && flags == _lastFlags.Value)
                return;

            Log("[iOS] ReachabilityFlagsChanged: " + flags);

            var reachableNow = IsReachable(flags);
            var reachablePrev = _lastFlags.HasValue && IsReachable(_lastFlags.Value);

            _lastFlags = flags;

            if (reachableNow == reachablePrev)
                return;

            if (reachableNow)
            {
                _ = AddCrumb(BreadcrumbsConstants.online);
                _ = OnConnectivityAvailableAsync();
            }
            else
            {
                AddCrumbFile(BreadcrumbsConstants.offline);
            }
        }
        catch (Exception ex)
        {
            Log("[iOS] ReachabilityFlagsChanged error: " + ex);
        }
    }

    private static bool IsReachable(NetworkReachabilityFlags flags)
    {
        var reachable = flags.HasFlag(NetworkReachabilityFlags.Reachable);
        var noConnReq = !flags.HasFlag(NetworkReachabilityFlags.ConnectionRequired);
        var autoConn  = flags.HasFlag(NetworkReachabilityFlags.ConnectionOnDemand) || flags.HasFlag(NetworkReachabilityFlags.ConnectionOnTraffic);
        var noUser    = !flags.HasFlag(NetworkReachabilityFlags.InterventionRequired);
        return reachable && (noConnReq || (autoConn && noUser));
    }

    private static void TryHookIOSNativePages()
    {
        try
        {
            var app = UIApplication.SharedApplication;
            if (app?.ConnectedScenes == null) return;

            _navDelegate ??= new NavDelegate();
            var attachedAny = false;

            foreach (var scene in app.ConnectedScenes)
            {
                if (scene is not UIWindowScene ws) continue;

                foreach (var window in ws.Windows)
                {
                    var root = window.RootViewController;
                    if (root == null) continue;

                    attachedAny |= AttachDelegateRecursively(root);
                }
            }

            if (attachedAny && !_pagesHooked)
            {
                _pagesHooked = true;
                Log("[iOS] Page breadcrumbs enabled (NavDelegate)");
            }
        }
        catch (Exception ex)
        {
            Log("[iOS] TryHookIOSNativePages error: " + ex);
        }
    }

    private static bool AttachDelegateRecursively(UIViewController vc)
    {
        var attached = false;

        if (vc is UINavigationController nav)
        {
            if (nav.Delegate != _navDelegate)
            {
                nav.Delegate = _navDelegate;
                attached = true;
            }
        }

        if (vc.PresentedViewController != null)
            attached |= AttachDelegateRecursively(vc.PresentedViewController);

        if (vc is UITabBarController tab && tab.ViewControllers != null)
        {
            foreach (var child in tab.ViewControllers)
            {
                if (child != null)
                    attached |= AttachDelegateRecursively(child);
            }
        }

        foreach (var child in vc.ChildViewControllers)
            attached |= AttachDelegateRecursively(child);

        return attached;
    }

    private sealed class NavDelegate : UINavigationControllerDelegate
    {
        private string? _currentPageFqcn;
        private bool _hasShownAny;

        public override void WillShowViewController(UINavigationController navigationController, UIViewController viewController, bool animated)
        {
            try
            {
                if (_hasShownAny && !string.IsNullOrEmpty(_currentPageFqcn))
                {
                    var prev = SimpleNameOf(_currentPageFqcn);
                    _ = AddCrumb($"{BreadcrumbsConstants.onDisappear}: {prev}");
                }
            }
            catch (Exception ex)
            {
                Log("[iOS] WillShowViewController error: " + ex);
            }
        }

        public override void DidShowViewController(UINavigationController navigationController, UIViewController viewController, bool animated)
        {
            try
            {
                var fqcn = viewController?.GetType()?.FullName ?? "UnknownVC";
                if (!_hasShownAny)
                {
                    _currentPageFqcn = fqcn;
                    _hasShownAny = true;
                    return;
                }
                if (!string.Equals(_currentPageFqcn, fqcn, StringComparison.Ordinal))
                {
                    _currentPageFqcn = fqcn;
                    var display = !string.IsNullOrWhiteSpace(viewController?.Title)
                        ? viewController!.Title!
                        : viewController?.GetType()?.Name ?? "UnknownVC";
                    _ = AddCrumb($"{BreadcrumbsConstants.onAppear}: {display}");
                }
            }
            catch (Exception ex)
            {
                Log("[iOS] DidShowViewController error: " + ex);
            }
        }

        private static string SimpleNameOf(string? fqcn)
        {
            if (string.IsNullOrWhiteSpace(fqcn)) return "";
            var i = fqcn.LastIndexOf('.');
            return i >= 0 ? fqcn[(i + 1)..] : fqcn;
        }
    }

    private static void Log(string msg) => Debug.WriteLine(msg);
}
#endif
