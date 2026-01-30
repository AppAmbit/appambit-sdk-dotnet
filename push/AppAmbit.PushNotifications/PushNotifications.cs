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
        if (platformContext is ActivityBase activity)
        {
            PushNotificationsAndroid.Init(activity);
            PushNotificationsAndroid.Start(activity, enableNotifications);
            return;
        }
        
        if (platformContext is Context androidContext)
        {
            PushNotificationsAndroid.Start(androidContext, enableNotifications);
        }
        else
        {
             // Try to start without context if already initialized, or it will throw/log inside
             PushNotificationsAndroid.Start(null!, enableNotifications); 
        }
#elif IOS
        PushNotificationsIos.Start();
#else
        NotSupported();
#endif
    }

    public static void SetNotificationsEnabled(bool enabled, object? platformContext = null)
    {
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

    public static void RequestNotificationPermission(object? platformActivity = null)
    {
#if ANDROID
        if (platformActivity is ActivityBase activity)
        {
             PushNotificationsAndroid.RequestNotificationPermission(activity, null);
        }
        else
        {
             PushNotificationsAndroid.RequestNotificationPermission(null);
        }
#elif IOS
        PushNotificationsIos.RequestNotificationPermission();
#else
        NotSupported();
#endif
    }

    public static bool HasSystemPermission()
    {
#if ANDROID
        return PushNotificationsAndroid.HasSystemPermission();
#elif IOS
        // On iOS, IsNotificationsEnabled typically reflects system permission status + user preference
        return PushNotificationsIos.IsNotificationsEnabled(); 
#else
        NotSupported();
        return false;
#endif
    }

    private static void NotSupported()
    {
        // throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android and iOS.");
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
