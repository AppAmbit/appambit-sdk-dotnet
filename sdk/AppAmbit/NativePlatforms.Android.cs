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
    internal static volatile bool _androidInitialized;
    internal static int _startedActivities;
    internal static int _resumedActivities;
    internal static volatile bool _inForeground;
    internal static volatile bool _waitingPause;

    internal static readonly long _activityDelayMs = 700;
    internal static readonly Android.OS.Handler _handler = new(Android.OS.Looper.MainLooper!);
    internal static readonly Java.Lang.IRunnable _pauseRunnable = new Java.Lang.Runnable(() =>
    {
        if (_resumedActivities == 0 && _inForeground && _waitingPause)
        {
            _inForeground = false;
            AppAmbitSdk.InternalSleep();
        }
        _waitingPause = false;
    });
    internal static Android.Net.ConnectivityManager.NetworkCallback? _netCallback;
    internal static volatile bool _firstConnectivityEvent = true;

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

    internal static void TrackPageChange(Android.App.Activity activity)
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



    internal static class InterlockedExtensions
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

public sealed class LifecycleCallbacks : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    public LifecycleCallbacks() { }
    public LifecycleCallbacks(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public void OnActivityCreated(Android.App.Activity activity, Android.OS.Bundle? savedInstanceState) { }

    public void OnActivityStarted(Android.App.Activity activity)
    {
        System.Threading.Interlocked.Increment(ref AppAmbit.NativePlatforms._startedActivities);
    }

    public void OnActivityResumed(Android.App.Activity activity)
    {
        System.Threading.Interlocked.Increment(ref AppAmbit.NativePlatforms._resumedActivities);

        if (AppAmbit.NativePlatforms._waitingPause)
        {
            AppAmbit.NativePlatforms._handler.RemoveCallbacks(AppAmbit.NativePlatforms._pauseRunnable);
            AppAmbit.NativePlatforms._waitingPause = false;
        }

        if (!AppAmbit.NativePlatforms._inForeground)
        {
            AppAmbit.NativePlatforms._inForeground = true;
            _ = AppAmbit.AppAmbitSdk.InternalResume();
        }

        AppAmbit.NativePlatforms.TrackPageChange(activity);
    }

    public void OnActivityPaused(Android.App.Activity activity)
    {
        AppAmbit.NativePlatforms.InterlockedExtensions.DecrementToZero(ref AppAmbit.NativePlatforms._resumedActivities);
        if (AppAmbit.NativePlatforms._resumedActivities == 0)
        {
            AppAmbit.NativePlatforms._waitingPause = true;
            AppAmbit.NativePlatforms._handler.PostDelayed(AppAmbit.NativePlatforms._pauseRunnable, AppAmbit.NativePlatforms._activityDelayMs);
        }
    }

    public void OnActivityStopped(Android.App.Activity activity)
    {
        AppAmbit.NativePlatforms.InterlockedExtensions.DecrementToZero(ref AppAmbit.NativePlatforms._startedActivities);
        if (AppAmbit.NativePlatforms._startedActivities == 0 && !activity.IsChangingConfigurations)
            AppAmbit.AppAmbitSdk.InternalEnd();
    }

    public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }

    public void OnActivityDestroyed(Android.App.Activity activity)
    {
        if (AppAmbit.NativePlatforms._startedActivities == 0 && AppAmbit.NativePlatforms._resumedActivities == 0 && !activity.IsChangingConfigurations)
            AppAmbit.AppAmbitSdk.InternalEnd();
    }
}

public sealed class NetCb : Android.Net.ConnectivityManager.NetworkCallback
{
    public NetCb() { }
    public NetCb(IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public override void OnAvailable(Android.Net.Network network)
    {
        base.OnAvailable(network);
        if (AppAmbit.NativePlatforms._firstConnectivityEvent)
        {
            AppAmbit.NativePlatforms._firstConnectivityEvent = false;
            return;
        }
        AppAmbit.NativePlatforms._handler.PostDelayed(new Java.Lang.Runnable(async () =>
        {
            await AppAmbit.BreadcrumbManager.AddAsync(AppAmbit.BreadcrumbsConstants.online);
            _ = AppAmbit.NativePlatforms.OnConnectivityAvailableAsync();
        }), 3000);
    }

    public override void OnLost(Android.Net.Network network)
    {
        base.OnLost(network);
        AppAmbit.BreadcrumbManager.SaveFile(AppAmbit.BreadcrumbsConstants.offline);
    }
}
#endif
