#if ANDROID
using System;
using System.Threading;
using System.Diagnostics;
using Android.Content;
using Android.Content.Res;
using Android.Net;
using Android.Views;

using AApp = Android.App.Application;
using AActivity = Android.App.Activity;
using ABundle = Android.OS.Bundle;
using AHandler = Android.OS.Handler;
using ALooper = Android.OS.Looper;
using AContext = Android.Content.Context;
using CManager = Android.Net.ConnectivityManager;
using NRequest = Android.Net.NetworkRequest;
using NCapability = Android.Net.NetCapability;

namespace AppAmbit;

internal static partial class NativePlatforms
{
    private static volatile bool _androidInitialized;
    private static int _startedActivities;
    private static int _resumedActivities;
    private static volatile bool _inForeground;
    private static volatile bool _waitingPause;

    private static readonly long _activityDelayMs = 700;
    private static readonly AHandler _handler = new(ALooper.MainLooper);
    private static readonly Java.Lang.IRunnable _pauseRunnable = new Java.Lang.Runnable(() =>
    {
        if (_resumedActivities == 0 && _inForeground && _waitingPause)
        {
            _inForeground = false;
            AppAmbitSdk.InternalSleep();
        }
        _waitingPause = false;
    });
    private static CManager.NetworkCallback? _netCallback;
    private static volatile bool _firstConnectivityEvent = true;

    private static readonly object _pageLock = new();
    private static string? _lastPageClassFqcn;

    static partial void PlatformRegister(string appKey)
    {
        if (_androidInitialized) return;
        var app = AApp.Context as AApp;
        if (app is null) return;
        app.RegisterActivityLifecycleCallbacks(new LifecycleCallbacks());
        TryRegisterNetworkCallback(AApp.Context);
        _androidInitialized = true;
    }

    static partial void PlatformEnablePageBreadcrumbs() { }

    private static void TrackPageChange(AActivity activity)
    {
        if (IsDialogLike(activity)) return;
        var fqcn = activity.Class?.Name ?? activity.GetType().FullName ?? "";
        string? prev;
        lock (_pageLock)
        {
            if (_lastPageClassFqcn == null)
            {
                _lastPageClassFqcn = fqcn;
                return;
            }
            if (string.Equals(_lastPageClassFqcn, fqcn, StringComparison.Ordinal))
                return;
            prev = _lastPageClassFqcn;
            _lastPageClassFqcn = fqcn;
        }
        var previousDisplay = SimpleNameOf(prev);
        var currentDisplay = activity.Class?.SimpleName ?? activity.GetType().Name;
        _ = BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onDisappear}: {previousDisplay}");
        _ = BreadcrumbManager.AddAsync($"{BreadcrumbsConstants.onAppear}: {currentDisplay}");
    }

    private static bool IsDialogLike(AActivity activity)
    {
        try
        {
            int[] attrs = new int[] { Android.Resource.Attribute.WindowIsTranslucent, Android.Resource.Attribute.WindowIsFloating };
            using var ta = activity.Theme?.ObtainStyledAttributes(attrs);
            bool translucent = ta?.GetBoolean(0, false) ?? false;
            bool floating = ta?.GetBoolean(1, false) ?? false;
            return translucent || floating;
        }
        catch
        {
            return false;
        }
    }

    private static string SimpleNameOf(string? fqcn)
    {
        if (string.IsNullOrWhiteSpace(fqcn)) return "";
        var i = fqcn.LastIndexOf('.');
        return i >= 0 ? fqcn.Substring(i + 1) : fqcn;
    }

    [Android.Runtime.Register("appambit/internal/LifecycleCallbacks")]
    internal sealed class LifecycleCallbacks : Java.Lang.Object, AApp.IActivityLifecycleCallbacks
    {
        public LifecycleCallbacks() { }
        public LifecycleCallbacks(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

        public void OnActivityCreated(AActivity activity, ABundle? savedInstanceState) { }

        public void OnActivityStarted(AActivity activity)
        {
            Interlocked.Increment(ref _startedActivities);
        }

        public void OnActivityResumed(AActivity activity)
        {
            Interlocked.Increment(ref _resumedActivities);

            if (_waitingPause)
            {
                _handler.RemoveCallbacks(_pauseRunnable);
                _waitingPause = false;
            }

            if (!_inForeground)
            {
                _inForeground = true;
                _ = AppAmbitSdk.InternalResume();
            }

            TrackPageChange(activity);
        }

        public void OnActivityPaused(AActivity activity)
        {
            InterlockedExtensions.DecrementToZero(ref _resumedActivities);
            if (_resumedActivities == 0)
            {
                _waitingPause = true;
                _handler.PostDelayed(_pauseRunnable, _activityDelayMs);
            }
        }

        public void OnActivityStopped(AActivity activity)
        {
            InterlockedExtensions.DecrementToZero(ref _startedActivities);
            if (_startedActivities == 0 && !activity.IsChangingConfigurations)
                AppAmbitSdk.InternalEnd();
        }

        public void OnActivitySaveInstanceState(AActivity activity, ABundle outState) { }

        public void OnActivityDestroyed(AActivity activity)
        {
            if (_startedActivities == 0 && _resumedActivities == 0 && !activity.IsChangingConfigurations)
                AppAmbitSdk.InternalEnd();
        }
    }

    private static void TryRegisterNetworkCallback(AContext context)
    {
        var cm = (CManager?)context.GetSystemService(AContext.ConnectivityService);
        if (cm is null) return;
        try
        {
            var request = new NRequest.Builder().AddCapability(NCapability.Internet).Build();
            _netCallback ??= new NetCb();
            cm.RegisterNetworkCallback(request, _netCallback);
        }
        catch (Java.Lang.SecurityException)
        {
            _netCallback = null;
        }
        catch
        {
            Debug.WriteLine("Error in TryRegisterNetworkCallback");
        }
    }

    [Android.Runtime.Register("appambit/internal/NetCb")]
    internal sealed class NetCb : CManager.NetworkCallback
    {
        public NetCb() { }
        public NetCb(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

        public override void OnAvailable(Network network)
        {
            base.OnAvailable(network);
            if (_firstConnectivityEvent)
            {
                _firstConnectivityEvent = false;
                return;
            }
            _handler.PostDelayed(new Java.Lang.Runnable(async () =>
            {
                await BreadcrumbManager.AddAsync(BreadcrumbsConstants.online);
                _ = OnConnectivityAvailableAsync();
            }), 3000);
        }

        public override void OnLost(Network network)
        {
            base.OnLost(network);
            BreadcrumbManager.SaveFile(BreadcrumbsConstants.offline);
        }
    }

    private static class InterlockedExtensions
    {
        public static void DecrementToZero(ref int target)
        {
            int initial, computed;
            do
            {
                initial = Volatile.Read(ref target);
                computed = Math.Max(0, initial - 1);
            } while (Interlocked.CompareExchange(ref target, computed, initial) != initial);
        }
    }
}
#endif
