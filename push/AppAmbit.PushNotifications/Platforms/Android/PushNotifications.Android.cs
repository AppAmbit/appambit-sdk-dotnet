using Android.Content;
using Android.Util;
using AndroidX.Core.App;
using Com.Appambit.Sdk.Models;
using Com.Appambit.Sdk;
using AppAmbit;
using System.Threading.Tasks;

namespace AppAmbit.PushNotifications;

internal static class PushNotificationsAndroid
{
    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;

    private static AndroidX.Activity.ComponentActivity? _currentActivity;

    // Try to get current activity from MAUI Platform using reflection (to avoid hard dependency)
    private static AndroidX.Activity.ComponentActivity? GetCurrentActivity()
    {
        try
        {
            // Try to get Microsoft.Maui.ApplicationModel.Platform.CurrentActivity using reflection
            var platformType = System.Type.GetType("Microsoft.Maui.ApplicationModel.Platform, Microsoft.Maui.Essentials");
            if (platformType != null)
            {
                var currentActivityProperty = platformType.GetProperty("CurrentActivity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (currentActivityProperty != null)
                {
                    var activity = currentActivityProperty.GetValue(null) as AndroidX.Activity.ComponentActivity;
                    if (activity != null)
                    {
                        _currentActivity = activity; // Update cached reference
                        return activity;
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Debug(LogTag, $"Could not get MAUI Platform.CurrentActivity: {ex.Message}");
        }

        return _currentActivity;
    }

    public static void Start(Context context)
    {
        if (context == null) throw new System.ArgumentNullException(nameof(context));

        // Attempt to capture activity if context is one
        if (context is AndroidX.Activity.ComponentActivity activity)
        {
            _currentActivity = activity;
        }

        var appContext = context.ApplicationContext;
        InternalStart(appContext ?? context);
    }
    
    // Explicit Init for Activity
    public static void Init(AndroidX.Activity.ComponentActivity activity)
    {
        _currentActivity = activity;
    }

    private static void InternalStart(Context appContext) 
    {
        if (!_initialized)
        {
            PushKernel.SetTokenListener(new TokenListenerProxy(appContext));
            _initialized = true;
        }

        _ = Task.Run(() =>
        {
            try
            {
                // Disable notifications by default before starting SDK
                PushKernel.SetNotificationsEnabled(appContext, false);
            }
            catch (Java.Lang.IllegalStateException ex)
            {
                Log.Warn(LogTag, $"Failed to disable notifications before start: {ex}");
            }

            try
            {
                PushKernel.Start(appContext);
            }
            catch (Java.Lang.IllegalStateException ex)
            {
                Log.Error(LogTag, $"Failed to start push: {ex}");
            }
        });
    }

    public static void SetNotificationsEnabled(Context? context, bool enabled)
    {
        // Try to get context from stored activity if null
        var targetContext = context ?? GetCurrentActivity()?.ApplicationContext;
        if (targetContext == null) 
        {
            Log.Error(LogTag, "SetNotificationsEnabled: Context is null and no activity initialized.");
            return;
        }

        try
        {
            PushKernel.SetNotificationsEnabled(targetContext.ApplicationContext, enabled);
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            Log.Error(LogTag, $"Failed to set notifications enabled={enabled}: {ex}");
        }

        // Update consumer logic - always sync state, even without token
        var token = _lastPushToken;
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait a bit if token is null to give FCM time to generate it
                if (token == null && enabled)
                {
                    await Task.Delay(1000);
                    token = _lastPushToken;
                }
                
                await AppAmbitSdk.UpdateConsumerAsync(token, enabled);
                Log.Debug(LogTag, $"Consumer push state synced: enabled={enabled}, token={token?.Substring(0, Math.Min(10, token?.Length ?? 0))}");
            }
            catch (System.Exception ex)
            {
                Log.Error(LogTag, $"Failed to sync consumer push state (enabled={enabled}): {ex}");
            }
            finally
            {
                if (!enabled)
                {
                    _lastPushToken = null;
                }
            }
        });
    }

    public static bool IsNotificationsEnabled(Context? context = null)
    {
        var targetContext = context ?? GetCurrentActivity()?.ApplicationContext;
        if (targetContext == null) return false;
        return PushKernel.IsNotificationsEnabled(targetContext);
    }

    // New parameterless/simplified methods
    public static bool HasSystemPermission()
    {
        var activity = GetCurrentActivity();
        if (activity == null) return false;
        if ((int)Android.OS.Build.VERSION.SdkInt < 33) return true;
        return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted;
    }

    public static void RequestNotificationPermission(PushNotifications.IPermissionListener? listener)
    {
        var activity = GetCurrentActivity();
        if (activity == null)
        {
            Log.Error(LogTag, "RequestNotificationPermission: Activity is not initialized. Call PushNotifications.Init(activity) or Start(activity) first.");
            return;
        }
        
        Log.Debug(LogTag, $"Requesting notification permission with activity: {activity.GetType().Name}");
        PushKernel.RequestNotificationPermission(activity, listener is null ? null : new PermissionListenerProxy(listener));
    }

    // Keep old signature for compatibility/internal use but forward
    public static void RequestNotificationPermission(AndroidX.Activity.ComponentActivity activity, PushNotifications.IPermissionListener? listener) 
    {
        _currentActivity = activity; // Update reference
        RequestNotificationPermission(listener);
    }

    public static void SetNotificationCustomizer(PushNotifications.INotificationCustomizer? customizer)
    {
        PushKernel.NotificationCustomizer = customizer is null
            ? null
            : new NotificationCustomizerProxy(customizer);
    }


    private sealed class PermissionListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.IPermissionListener
    {
        private readonly PushNotifications.IPermissionListener _managed;

        public PermissionListenerProxy(PushNotifications.IPermissionListener managed)
        {
            _managed = managed;
        }

        public void OnPermissionResult(bool isGranted)
        {
            _managed.OnPermissionResult(isGranted);
        }
    }

    private sealed class NotificationCustomizerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.INotificationCustomizer
    {
        public PushNotifications.INotificationCustomizer Managed { get; }

        public NotificationCustomizerProxy(PushNotifications.INotificationCustomizer managed)
        {
            Managed = managed;
        }

        public void Customize(Context context, NotificationCompat.Builder builder, AppAmbitNotification notification)
        {
            var managedNotification = new PushNotificationData(
                notification.Title,
                notification.Body,
                notification.Color,
                notification.SmallIconName,
                notification.Data);

            Managed.Customize((object)context, (object)builder, managedNotification);
        }
    }

    private sealed class TokenListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.ITokenListener
    {
        private readonly Context _context;

        public TokenListenerProxy(Context context)
        {
            _context = context;
        }

        public void OnNewToken(string token)
        {
            var isEnabled = PushKernel.IsNotificationsEnabled(_context);

            Log.Debug(LogTag, $"FCM token received: {token} || {isEnabled}");
            _lastPushToken = token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await AppAmbitSdk.UpdateConsumerAsync(token, isEnabled);
                }
                catch (System.Exception ex)
                {
                    Log.Error(LogTag, $"Failed to update consumer with new FCM token: {ex}");
                }
            });
        }
    }
}
