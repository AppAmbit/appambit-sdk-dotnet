using Android.Content;
using Android.Util;
using AndroidX.Core.App;
using Com.Appambit.Sdk.Models;
using Com.Appambit.Sdk;
using AppAmbit;
using System.Threading.Tasks;
using ActivityBase = AndroidX.Activity.ComponentActivity;

namespace AppAmbit.PushNotifications;

internal static class PushNotificationsAndroid
{
    private static bool _initialized;
    private static string? _lastPushToken;
    private const string LogTag = PushNotifications.LogTag;

    private static ActivityBase? _currentActivity;

    public static void Start(Context context, bool enableNotifications)
    {
        if (context == null) throw new System.ArgumentNullException(nameof(context));

        // Attempt to capture activity if context is one, though explicit Init is better
        if (context is ActivityBase activity)
        {
            _currentActivity = activity;
        }

        var appContext = context.ApplicationContext;
        // ... rest of start logic
        InternalStart(appContext ?? context, enableNotifications);
    }
    
    // Explicit Init for Activity
    public static void Init(ActivityBase activity)
    {
        _currentActivity = activity;
    }

    private static void InternalStart(Context appContext, bool enableNotifications) 
    {
        if (!_initialized)
        {
            PushKernel.SetTokenListener(new TokenListenerProxy(appContext));
            _initialized = true;
        }

        _ = Task.Run(() =>
        {
            var needsPostStartSync = false;

            try
            {
                PushKernel.SetNotificationsEnabled(appContext, enableNotifications);
            }
            catch (Java.Lang.IllegalStateException ex)
            {
                needsPostStartSync = true;
                Log.Warn(LogTag, $"Failed to apply notifications enabled={enableNotifications} before start: {ex}");
            }

            try
            {
                PushKernel.Start(appContext);
            }
            catch (Java.Lang.IllegalStateException ex)
            {
                Log.Error(LogTag, $"Failed to start push: {ex}");
                return;
            }

            if (needsPostStartSync)
            {
                try
                {
                    PushKernel.SetNotificationsEnabled(appContext, enableNotifications);
                }
                catch (Java.Lang.IllegalStateException ex)
                {
                    Log.Error(LogTag, $"Failed to apply notifications enabled={enableNotifications} after start: {ex}");
                }
            }
        });
    }

    public static void SetNotificationsEnabled(Context? context, bool enabled)
    {
        // Try to get context from stored activity if null
        var targetContext = context ?? _currentActivity?.ApplicationContext;
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

        var token = _lastPushToken;
        if (!string.IsNullOrWhiteSpace(token))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await AppAmbitSdk.UpdateConsumerAsync(token, enabled);
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
        else if (!enabled)
        {
            _lastPushToken = null;
        }
    }

    public static bool IsNotificationsEnabled(Context? context = null)
    {
        var targetContext = context ?? _currentActivity?.ApplicationContext;
        if (targetContext == null) return false;
        return PushKernel.IsNotificationsEnabled(targetContext);
    }

    // New parameterless/simplified methods
    public static bool HasSystemPermission()
    {
        if (_currentActivity == null) return false;
        if ((int)Android.OS.Build.VERSION.SdkInt < 33) return true;
        return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(_currentActivity, Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted;
    }

    public static void RequestNotificationPermission(PushNotifications.IPermissionListener? listener)
    {
        if (_currentActivity == null)
        {
            Log.Error(LogTag, "RequestNotificationPermission: Activity is not initialized. Call PushNotifications.Init(activity) or Start(activity) first.");
            return;
        }
        PushKernel.RequestNotificationPermission(_currentActivity, listener is null ? null : new PermissionListenerProxy(listener));
    }

    // Keep old signature for compatibility/internal use but forward
    public static void RequestNotificationPermission(ActivityBase activity, PushNotifications.IPermissionListener? listener) 
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

    public static PushNotifications.INotificationCustomizer? GetNotificationCustomizer()
    {
        return PushKernel.NotificationCustomizer is NotificationCustomizerProxy proxy
            ? proxy.Managed
            : null;
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
            if (!PushKernel.IsNotificationsEnabled(_context))
                return;

            Log.Debug(LogTag, $"FCM token received: {token}");
            _lastPushToken = token;
            _ = Task.Run(async () =>
            {
                try
                {
                    await AppAmbitSdk.UpdateConsumerAsync(token, true);
                }
                catch (System.Exception ex)
                {
                    Log.Error(LogTag, $"Failed to update consumer with new FCM token: {ex}");
                }
            });
        }
    }
}
