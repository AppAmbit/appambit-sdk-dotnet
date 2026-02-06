#if MACCATALYST
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using CoreFoundation;
using Foundation;
using Network;
using UIKit;
using AppAmbit.Services;

namespace AppAmbit;

internal static class AppAmbitMacOs
{
    static readonly List<NSObject> _obs = new();
    static bool _started;
    static bool _ever;
    static int _active;

    static NWPathMonitor _monitor;
    static bool _hasStatus;
    static NWPathStatus _lastStatus;
    static bool _isHandlingNetworkChange;
    static bool _skipFirstResume = true;

    static bool _pagesHooked;
    static UINavigationControllerDelegate? _navDelegate;

    static void TryResume()
    {
        if (_active <= 0) return;

        if (_skipFirstResume)
        {
            _skipFirstResume = false;
            return;
        }

        _ever = true;
        _ = AppAmbitSdk.InternalResume();
    }

    static void TrySleep()
    {
        if (!_ever) return;
        if (_active == 0) AppAmbitSdk.InternalSleep();
    }

    static void StartNetworkMonitoring()
    {
        if (_monitor != null) return;

        _monitor = new NWPathMonitor();
        var queue = new DispatchQueue("AppAmbitMacOs.NWPathMonitor");
        _monitor.SetQueue(queue);
        _monitor.SnapshotHandler = path => OnPathUpdated(path);
        _monitor.Start();
    }

    static void OnPathUpdated(NWPath path)
    {
        var status = path.Status;

        if (_hasStatus && status == _lastStatus) return;

        var previous = _hasStatus ? _lastStatus : status;
        _lastStatus = status;
        _hasStatus = true;

        if (status != NWPathStatus.Satisfied && previous == NWPathStatus.Satisfied)
        {
            BreadcrumbManager.SaveFile(BreadcrumbsConstants.offline);
            return;
        }

        if (status == NWPathStatus.Satisfied && previous != NWPathStatus.Satisfied)
        {
            _ = HandleNetworkChangeAsync();
        }
    }

    static async Task HandleNetworkChangeAsync()
    {
        if (_isHandlingNetworkChange) return;
        _isHandlingNetworkChange = true;

        try
        {
            if (!AppAmbitSdk.InternalTokenIsValid())
                await AppAmbitSdk.InternalEnsureToken(null);

            BreadcrumbManager.LoadBreadcrumbsFromFile();
            await SessionManager.SendEndSessionFromDatabase();
            await SessionManager.SendStartSessionIfExist();
            await Crashes.LoadCrashFileIfExists();
            await BreadcrumbManager.AddAsync(BreadcrumbsConstants.online);
            await AppAmbitSdk.InternalSendPending();
        }
        finally
        {
            _isHandlingNetworkChange = false;
        }
    }

    static void TryHookMacOsNativePages()
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
                Log("[MacOS] Page breadcrumbs enabled (NavDelegate)");
            }
        }
        catch (Exception ex)
        {
            Log("[MacOS] TryHookMacOsNativePages error: " + ex);
        }
    }

    static bool AttachDelegateRecursively(UIViewController vc)
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
        sealed class NavState
        {
            public string Current = "";
            public bool Initialized;
        }

        readonly Dictionary<nint, NavState> _states = new();
        static string? _lastFrom;
        static string? _lastTo;

        public override void DidShowViewController(UINavigationController navigationController, UIViewController viewController, bool animated)
        {
            try
            {
                if (navigationController == null || viewController == null)
                    return;

                var navId = navigationController.Handle;
                if (!_states.TryGetValue(navId, out var state))
                {
                    state = new NavState();
                    _states[navId] = state;
                }

                var newName = viewController.GetType().Name;

                if (!state.Initialized)
                {
                    state.Current = newName;
                    state.Initialized = true;
                    return;
                }

                if (string.Equals(state.Current, newName, StringComparison.Ordinal))
                    return;

                var previous = state.Current;

                if (string.Equals(_lastFrom, previous, StringComparison.Ordinal) &&
                    string.Equals(_lastTo, newName, StringComparison.Ordinal))
                {
                    state.Current = newName;
                    return;
                }

                state.Current = newName;
                _lastFrom = previous;
                _lastTo = newName;

                _ = BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onDisappear}: {previous}");
                _ = BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onAppear}: {newName}");
            }
            catch (Exception ex)
            {
                Log("[MacOS] DidShowViewController error: " + ex);
            }
        }
    }



    static void Log(string msg)
    {
        try
        {
            Debug.WriteLine(msg);
        }
        catch
        {
            Debug.WriteLine(msg);
        }
    }

    public static void Register(string appKey)
    {
        try
        {
            if (_started) return;
            _started = true;

            AppAmbitSdk.InternalStart(appKey);
            StartNetworkMonitoring();

            var c = NSNotificationCenter.DefaultCenter;
            _obs.Add(c.AddObserver(UIScene.DidActivateNotification, _ => UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                _active++;
                TryResume();
                TryHookMacOsNativePages();
            })));
            _obs.Add(c.AddObserver(UIScene.WillDeactivateNotification, _ => UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                if (_active > 0) _active--;
                TrySleep();
            })));
            _obs.Add(c.AddObserver(UIScene.DidEnterBackgroundNotification, _ => UIApplication.SharedApplication.InvokeOnMainThread(() =>
            {
                if (_active > 0) _active--;
                TrySleep();
            })));

            _active = 0;
            foreach (var s in UIApplication.SharedApplication.ConnectedScenes)
            {
                if (s is UIScene sc && sc.ActivationState == UISceneActivationState.ForegroundActive)
                    _active++;
            }

            if (_active > 0)
            {
                TryResume();
                UIApplication.SharedApplication.InvokeOnMainThread(TryHookMacOsNativePages);
            }
        }catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbitMacOs] Exception during Register: {ex}");
        }
    }
}
#endif
