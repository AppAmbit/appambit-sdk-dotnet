using System;
using Android.Content;
using AndroidX.Core.App;
using Com.Appambit.Sdk.Models;
using ActivityBase = AndroidX.Activity.ComponentActivity;

namespace AppAmbit.PushNotifications;

/// <summary>
/// Cross-platform facade for AppAmbit Push.
/// </summary>
public static class PushNotifications
{
    internal const string LogTag = "AppAmbitPushSDK";

    public interface IPermissionListener
    {
        void OnPermissionResult(bool isGranted);
    }

    public interface INotificationCustomizer
    {
        void Customize(Context context, NotificationCompat.Builder builder, AppAmbitNotification notification);
    }

    public static void Start(Context context, bool enableNotifications = true)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        PushNotificationsAndroid.Start(context, enableNotifications);
    }

    public static void SetNotificationsEnabled(Context context, bool enabled)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        PushNotificationsAndroid.SetNotificationsEnabled(context, enabled);
    }

    public static bool IsNotificationsEnabled(Context context)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        return PushNotificationsAndroid.IsNotificationsEnabled(context);
    }

    public static void RequestNotificationPermission(ActivityBase activity)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        PushNotificationsAndroid.RequestNotificationPermission(activity, null);
    }

    public static void RequestNotificationPermission(ActivityBase activity, IPermissionListener? listener)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        PushNotificationsAndroid.RequestNotificationPermission(activity, listener);
    }

    public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        PushNotificationsAndroid.SetNotificationCustomizer(customizer);
    }

    public static INotificationCustomizer? GetNotificationCustomizer()
    {
        if (!OperatingSystem.IsAndroid())
            throw new PlatformNotSupportedException("AppAmbit push notifications are only supported on Android.");

        return PushNotificationsAndroid.GetNotificationCustomizer();
    }
}
