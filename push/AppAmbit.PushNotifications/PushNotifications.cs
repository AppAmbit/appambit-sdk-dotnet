#if ANDROID
using Android.Content;
using AndroidX.Core.App;
using Com.Appambit.Sdk.Models;
//using Com.Appambit.Sdk.Models;
using ActivityBase = AndroidX.Activity.ComponentActivity;
#endif
using System;

namespace AppAmbit.PushNotifications;

public record PushNotificationData(string Title, string Body, string Color, string SmallIconName, object Data);

/// <summary>
/// Cross-platform facade for AppAmbit push notifications.
/// </summary>
public static class PushNotifications
{
    internal const string LogTag = "AppAmbitPushSDK";

    public static void Start(object? platformContext = null, bool enableNotifications = true)
    {
#if ANDROID
        if (platformContext is not Context androidContext)
        {
            throw new ArgumentNullException(nameof(platformContext), "Android context is required when running on Android.");
        }

        PushNotificationsAndroid.Start(androidContext, enableNotifications);
#elif IOS
        PushNotificationsIos.Start();
#else
        NotSupported();
#endif
    }

    public static void SetNotificationsEnabled(bool enabled, object? platformContext = null)
    {
#if ANDROID
        if (platformContext is not Context androidContext)
        {
            throw new ArgumentNullException(nameof(platformContext), "Android context is required when running on Android.");
        }

        PushNotificationsAndroid.SetNotificationsEnabled(androidContext, enabled);
#elif IOS
        PushNotificationsIos.SetNotificationsEnabled(enabled);
#else
        NotSupported();
#endif
    }

    public static bool IsNotificationsEnabled(object? platformContext = null)
    {
#if ANDROID
        if (platformContext is not Context androidContext)
        {
            throw new ArgumentNullException(nameof(platformContext), "Android context is required when running on Android.");
        }

        return PushNotificationsAndroid.IsNotificationsEnabled(androidContext);
#elif IOS
        return PushNotificationsIos.IsNotificationsEnabled();
#else
        NotSupported();
        return false;
#endif
    }

    public static void RequestNotificationPermission(object? platformActivity = null)
    {
#if ANDROID
        if (platformActivity is not ActivityBase activity)
        {
            throw new ArgumentNullException(nameof(platformActivity), "Android Activity is required when running on Android.");
        }

        PushNotificationsAndroid.RequestNotificationPermission(activity, null);
#elif IOS
        PushNotificationsIos.RequestNotificationPermission();
#else
        NotSupported();
#endif
    }

    private static void NotSupported()
    {
        throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android and iOS.");
    }

    public interface IPermissionListener
    {
        void OnPermissionResult(bool isGranted);
    }

    public interface INotificationCustomizer
    {
        void Customize(object context, object builder, PushNotificationData notification);
    }

#if ANDROID
    public static void RequestNotificationPermission(ActivityBase activity, IPermissionListener? listener)
    {
        PushNotificationsAndroid.RequestNotificationPermission(activity, listener);
    }

    public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
    {
        PushNotificationsAndroid.SetNotificationCustomizer(customizer);
    }

    public static INotificationCustomizer? GetNotificationCustomizer()
    {
        return PushNotificationsAndroid.GetNotificationCustomizer();
    }
#elif IOS
    public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
    {
        PushNotificationsIos.SetNotificationCustomizer(customizer);
    }

    public static INotificationCustomizer? GetNotificationCustomizer()
    {
        return PushNotificationsIos.GetNotificationCustomizer();
    }
#endif
}
