#if ANDROID
using Android.Content;
using AndroidApp = global::Android.App;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;

namespace AppAmbit.PushNotifications;

public record PushNotificationData(
    string? Title,
    string? Body,
    string? ImageUrl,
    IDictionary<string, string>? Data,
    AndroidPushData? Android = null,
    IosPushData? Ios = null)
{
    public override string ToString()
    {
        var dataStr = Data is { Count: > 0 }
            ? "{" + string.Join(", ", Data.Select(kv => $"{kv.Key}={kv.Value}")) + "}"
            : "{}";
        return $"PushNotificationData {{ Title={Title}, Body={Body}, ImageUrl={ImageUrl}, Data={dataStr}, Android={Android}, Ios={Ios} }}";
    }
}

public record AndroidPushData(
    string? Color,
    string? SmallIconName,
    string? Ticker = null,
    bool? Sticky = null,
    string? Visibility = null,
    string? ChannelId = null,
    string? Priority = null,
    string? Tag = null,
    string? Sound = null,
    string? ClickAction = null);

public record IosPushData(
    string? Sound = null,
    int? Badge = null,
    string? ThreadId = null,
    string? Category = null);

/// <summary>
/// Cross-platform facade for AppAmbit push notifications.
/// </summary>
public static class PushNotifications
{
    internal const string LogTag = "AppAmbitPushSDKNET";

    public static void Start(object? platformContext = null)
    {
        if (!AppAmbitSdk.IsInitialized)
        {
            Debug.WriteLine($"[{LogTag}] ERROR: AppAmbit SDK has not been started.");
            return;
        }

#if ANDROID
        PushNotificationsAndroid.Start(platformContext as Context);
#elif IOS
        PushNotificationsIos.Start();
#else
        NotSupported();
#endif
    }

    public static void SetNotificationsEnabled(bool enabled, object? platformContext = null)
    {
        if (!AppAmbitSdk.IsInitialized)
        {
            Debug.WriteLine($"[{LogTag}] ERROR: AppAmbit SDK is not initialized.");
            return;
        }

#if ANDROID
        PushNotificationsAndroid.SetNotificationsEnabled(platformContext as Context, enabled);
#elif IOS
        PushNotificationsIos.SetNotificationsEnabled(enabled);
#else
        NotSupported();
#endif
    }

    public static bool IsNotificationsEnabled(object? platformContext = null)
    {
#if ANDROID
        return PushNotificationsAndroid.IsNotificationsEnabled(platformContext as Context);
#elif IOS
        return PushNotificationsIos.IsNotificationsEnabled();
#else
        NotSupported();
        return false;
#endif
    }

    public static bool HasNotificationPermission(object? platformContext = null)
    {
#if ANDROID
        return PushNotificationsAndroid.HasNotificationPermission();
#elif IOS
        return PushNotificationsIos.HasNotificationPermission();
#else
        NotSupported();
        return false;
#endif
    }

    public static void RequestNotificationPermission(object? platformContext = null, Action<bool>? callback = null)
    {
#if ANDROID
        if (platformContext is IPermissionListener listener)
            PushNotificationsAndroid.RequestNotificationPermission(listener);
        else if (platformContext is AndroidApp.Activity activity)
            PushNotificationsAndroid.RequestNotificationPermission(activity, null);
        else
            PushNotificationsAndroid.RequestNotificationPermission(null);
#elif IOS
        if (platformContext is IPermissionListener listener)
            PushNotificationsIos.RequestNotificationPermission(granted => listener.OnPermissionResult(granted));
        else if (platformContext != null)
        {
            var method = platformContext.GetType().GetMethod("OnPermissionResult");
            if (method != null)
                PushNotificationsIos.RequestNotificationPermission(granted =>
                {
                    try { method.Invoke(platformContext, new object[] { granted }); }
                    catch (Exception ex) { Debug.WriteLine($"[{LogTag}] Reflection invocation failed: {ex}"); }
                });
            else
                PushNotificationsIos.RequestNotificationPermission(callback);
        }
        else
            PushNotificationsIos.RequestNotificationPermission(callback);
#else
        NotSupported();
#endif
    }

    // ── Cross-platform listeners ───────────────────────────────────────────

    public static void SetForegroundListener(Action<PushNotificationData> listener)
    {
#if ANDROID
        PushNotificationsAndroid.SetForegroundListener(listener);
#elif IOS
        PushNotificationsIos.SetForegroundListener(listener);
#else
        NotSupported();
#endif
    }

    public static void SetOpenedListener(Action<PushNotificationData> listener)
    {
#if ANDROID
        PushNotificationsAndroid.SetOpenedListener(listener);
#elif IOS
        PushNotificationsIos.SetOpenedListener(listener);
#else
        NotSupported();
#endif
    }

#if IOS
    /// <summary>
    /// Delivers a cold-start launch notification (app launched by tapping a notification
    /// while fully terminated) to the opened listener. Call from the iOS AppDelegate's
    /// <c>FinishedLaunching</c>, passing its launch options. iOS hands a cold-start tap to
    /// the launch options rather than the notification-center delegate, so this covers that gap.
    /// </summary>
    public static void HandleLaunchOptions(Foundation.NSDictionary? launchOptions)
        => PushNotificationsIos.HandleColdStartLaunch(launchOptions);
#endif

    // ── Interfaces ────────────────────────────────────────────────────────

    public interface IPermissionListener
    {
        void OnPermissionResult(bool isGranted);
    }

    public interface INotificationCustomizer
    {
        void Customize(object context, object builder, PushNotificationData notification);
    }

    // ── Platform-specific overloads ───────────────────────────────────────

#if ANDROID
    public static void RequestNotificationPermission(IPermissionListener? listener)
        => PushNotificationsAndroid.RequestNotificationPermission(listener);

    public static void RequestNotificationPermission(AndroidApp.Activity activity, IPermissionListener? listener)
        => PushNotificationsAndroid.RequestNotificationPermission(activity, listener);

#elif IOS
    public static void RequestNotificationPermission(IPermissionListener? listener)
        => PushNotificationsIos.RequestNotificationPermission(granted => listener?.OnPermissionResult(granted));

#else
    public static void RequestNotificationPermission(IPermissionListener? listener) => NotSupported();
#endif

    // ── Android-only ──────────────────────────────────────────────────────

    /// <summary>
    /// Android-specific push notification extensions.
    /// </summary>
    public static class Android
    {
        /// <summary>
        /// Registers a listener that fires when a push notification arrives while the app is in the background.
        /// Only available on Android — on iOS, background notifications are handled by the Notification Service Extension.
        /// </summary>
        [SupportedOSPlatform("android")]
        public static void SetBackgroundListener(Action<PushNotificationData> listener)
        {
#if ANDROID
            PushNotificationsAndroid.SetBackgroundListener(listener);
#endif
        }

        /// <summary>
        /// Registers a customizer that mutates the <c>NotificationCompat.Builder</c> before the system posts the notification.
        /// Only available on Android — on iOS, equivalent mutation is performed in the Notification Service Extension.
        /// </summary>
        [SupportedOSPlatform("android")]
        public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
        {
#if ANDROID
            PushNotificationsAndroid.SetNotificationCustomizer(customizer);
#endif
        }

#if ANDROID
        public static void HandleNotificationOpened(global::Android.Content.Intent? intent)
            => PushNotificationsAndroid.TryHandleOpenedIntent(intent);
#endif
    }

    // ─────────────────────────────────────────────────────────────────────

    private static void NotSupported() =>
        Debug.WriteLine($"[{LogTag}] Push Notifications are not supported on this platform.");
}
