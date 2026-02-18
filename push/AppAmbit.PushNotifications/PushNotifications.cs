#if ANDROID
using Android.Content;
#endif
using System;
using System.Diagnostics;

namespace AppAmbit.PushNotifications;

public record PushNotificationData(string Title, string Body, string Color, string SmallIconName, object Data);

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
            Debug.WriteLine($"[{LogTag}] ERROR: AppAmbit SDK has not been started. Please call AppAmbit.Start() before starting the Push SDK.");
            return;
        }

        Debug.WriteLine($"[{LogTag}] Starting Push SDK and binding to AppAmbit Core.");

#if ANDROID
        if (platformContext is AndroidX.Activity.ComponentActivity activity)
        {
            PushNotificationsAndroid.Init(activity);
            PushNotificationsAndroid.Start(activity);
        }
        else if (platformContext is Context androidContext)
        {
            PushNotificationsAndroid.Start(androidContext);
        }
        else
        {
             // Try to start without context if already initialized
             PushNotificationsAndroid.Start(null!); 
        }
#elif IOS
        // Auto-detect debug mode inside native if needed, or pass it if exposed.
        // For now, we respect the user preference: autoRequestPermissions defaults to false.
        PushNotificationsIos.Start();
        
        // Sync logic
        if (PushNotificationsIos.IsNotificationsEnabled())
        {
            var token = PushNotificationsIos.GetCurrentToken();
            if (!string.IsNullOrEmpty(token))
            {
                Debug.WriteLine($"[{LogTag}] Push SDK started. Syncing current token with backend.");
                _ = System.Threading.Tasks.Task.Run(() => AppAmbitSdk.UpdateConsumerAsync(token, true));
            }
        }
#else
        NotSupported();
#endif
    }

    public static void SetNotificationsEnabled(bool enabled, object? platformContext = null)
    {
        if (!AppAmbitSdk.IsInitialized)
        {
            Debug.WriteLine($"[{LogTag}] ERROR: AppAmbit SDK is not initialized. Cannot set notification status.");
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

    public static bool HasSystemPermission(object? platformContext = null)
    {
#if ANDROID
        return PushNotificationsAndroid.HasSystemPermission();
#elif IOS
        return PushNotificationsIos.HasSystemPermission();
#else
        NotSupported();
        return false;
#endif
    }

    public static void RequestNotificationPermission(object? platformContext = null, Action<bool>? callback = null)
    {
#if ANDROID
        if (platformContext is AndroidX.Activity.ComponentActivity activity)
        {
             // TODO: Update Android implementation to support Action<bool>
             PushNotificationsAndroid.RequestNotificationPermission(activity, null); 
             // Note: Current Android impl uses interface. Future refactor needed for Action<bool> parity if desired.
             // For now, we keep existing behavior or adapt if Android API allows. 
             // Since user focused on iOS/MAUI fix first, we'll leave Android mostly as-is but matching signature.
        }
        else
        {
             PushNotificationsAndroid.RequestNotificationPermission(null);
        }
#elif IOS
        Debug.WriteLine($"[{LogTag}] RequestNotificationPermission generic called. (Reflection Fix Applied) platformContext={platformContext}, callback={callback}");

        // Handle case where overloaded resolution picked this method instead of the IPermissionListener one
        // (common in shared projects targeting netstandard/net8.0 where #if IOS methods are hidden)
        
        // 1. Try Direct Interface Cast
        if (platformContext is IPermissionListener listener)
        {
             Debug.WriteLine($"[{LogTag}] platformContext IS IPermissionListener. Adapting to callback.");
             PushNotificationsIos.RequestNotificationPermission(granted => listener.OnPermissionResult(granted));
        }
        // 2. Try Reflection (Duck Typing) - in case of type mismatch/context issues
        else if (platformContext != null) 
        {
             Debug.WriteLine($"[{LogTag}] platformContext is NOT IPermissionListener (Type: {platformContext.GetType().FullName}). Trying reflection...");
             var method = platformContext.GetType().GetMethod("OnPermissionResult");
             if (method != null)
             {
                 Debug.WriteLine($"[{LogTag}] Found OnPermissionResult method via reflection. Adapting.");
                 PushNotificationsIos.RequestNotificationPermission(granted => 
                 {
                     try 
                     {
                        method.Invoke(platformContext, new object[] { granted });
                     }
                     catch(Exception ex)
                     {
                        Debug.WriteLine($"[{LogTag}] Reflection invocation failed: {ex}");
                     }
                 });
             }
             else
             {
                 Debug.WriteLine($"[{LogTag}] No OnPermissionResult method found. Fallback to callback arg.");
                 PushNotificationsIos.RequestNotificationPermission(callback);
             }
        }
        else
        {
             Debug.WriteLine($"[{LogTag}] platformContext is null. Using callback directly.");
             PushNotificationsIos.RequestNotificationPermission(callback);
        }
#else
        NotSupported();
#endif
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
    public static void RequestNotificationPermission(AndroidX.Activity.ComponentActivity activity, IPermissionListener? listener)
    {
        PushNotificationsAndroid.RequestNotificationPermission(activity, listener);
    }

    public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
    {
        PushNotificationsAndroid.SetNotificationCustomizer(customizer);
    }
#elif IOS
    public static void RequestNotificationPermission(IPermissionListener? listener)
    {
        PushNotificationsIos.RequestNotificationPermission(granted => listener?.OnPermissionResult(granted));
    }

    public static void SetNotificationCustomizer(INotificationCustomizer? customizer)
    {
        PushNotificationsIos.SetNotificationCustomizer(customizer);
    }
#endif
    private static void NotSupported()
    {
        Debug.WriteLine($"[{LogTag}] Push Notifications are not supported on this platform.");
    }
}
