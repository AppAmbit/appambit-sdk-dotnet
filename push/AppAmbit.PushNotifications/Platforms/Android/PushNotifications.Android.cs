using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Util;
using Com.Appambit.Sdk;
using System.Threading.Tasks;

namespace AppAmbit.PushNotifications;

internal static class PushNotificationsAndroid
{
    private static bool _initialized;
    internal static string? _lastPushToken;
    internal const string LogTag = PushNotifications.LogTag;

    private static Activity? _currentActivity;
    private static bool _lifecycleRegistered;

    internal static PushNotifications.IPermissionListener? _permissionListener;

    internal static void SetCurrentActivity(Activity activity) => _currentActivity = activity;
    internal static void ClearCurrentActivity(Activity activity) { if (_currentActivity == activity) _currentActivity = null; }

    private static Activity? GetCurrentActivity()
    {
        if (!_lifecycleRegistered)
        {
            if (Application.Context is Application app)
            {
                app.RegisterActivityLifecycleCallbacks(new PushLifecycleCallbacks());
                _lifecycleRegistered = true;
            }
        }

        try
        {
            var platformType = System.Type.GetType("Microsoft.Maui.ApplicationModel.Platform, Microsoft.Maui.Essentials");
            if (platformType != null)
            {
                var prop = platformType.GetProperty("CurrentActivity", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (prop?.GetValue(null) is Activity activity)
                {
                    _currentActivity = activity;
                    return activity;
                }
            }
        }
        catch (System.Exception ex)
        {
            Log.Debug(LogTag, $"Could not get MAUI Platform.CurrentActivity: {ex.Message}");
        }

        return _currentActivity;
    }

    public static void Start(Context? context)
    {
        var target = context ?? GetCurrentActivity() ?? Android.App.Application.Context;
        if (target == null) throw new System.ArgumentNullException(nameof(context));

        if (target is Activity activity)
            _currentActivity = activity;

        InternalStart(target.ApplicationContext ?? target);
    }

    private static void InternalStart(Context appContext)
    {
        try
        {
            if (Firebase.FirebaseApp.Instance == null)
            {
                Firebase.FirebaseApp.InitializeApp(appContext);
                Log.Debug(LogTag, "Firebase explicitly initialized.");
            }
        }
        catch (System.Exception ex)
        {
            Log.Warn(LogTag, $"Firebase.InitializeApp skipped or failed: {ex.Message}");
        }

        if (!_initialized)
        {
            PushKernel.SetTokenListener(new TokenListenerProxy(appContext));
            _initialized = true;
        }

        _ = Task.Run(() =>
        {
            try { PushKernel.Start(appContext); }
            catch (Java.Lang.IllegalStateException ex) { Log.Error(LogTag, $"Failed to start push: {ex}"); }
        });
    }

    public static void SetNotificationsEnabled(Context? context, bool enabled)
    {
        var target = context ?? GetCurrentActivity()?.ApplicationContext ?? Android.App.Application.Context;
        if (target == null)
        {
            Log.Error(LogTag, "SetNotificationsEnabled: Context is null.");
            return;
        }

        try { PushKernel.SetNotificationsEnabled(target, enabled); }
        catch (Java.Lang.IllegalStateException ex) { Log.Error(LogTag, $"Failed to set notifications enabled={enabled}: {ex}"); }

        var token = _lastPushToken;
        _ = Task.Run(async () =>
        {
            try
            {
                if (token == null && enabled)
                {
                    await Task.Delay(2000);
                    token = _lastPushToken;
                }
                await AppAmbitSdk.UpdateConsumerAsync(token, enabled);
            }
            catch (System.Exception ex) { Log.Error(LogTag, $"Failed to sync consumer push state: {ex}"); }
            finally { if (!enabled) _lastPushToken = null; }
        });
    }

    public static bool IsNotificationsEnabled(Context? context = null)
    {
        var target = context ?? GetCurrentActivity()?.ApplicationContext ?? Android.App.Application.Context;
        if (target == null) return false;
        return PushKernel.IsNotificationsEnabled(target);
    }

    public static bool HasSystemPermission()
    {
        var context = (Context?)GetCurrentActivity() ?? Android.App.Application.Context;
        if ((int)Build.VERSION.SdkInt < 33) return true;
        return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.PostNotifications) == Permission.Granted;
    }

    public static void RequestNotificationPermission(PushNotifications.IPermissionListener? listener)
    {
        var activity = GetCurrentActivity();
        if (activity == null)
        {
            Log.Error(LogTag, "RequestNotificationPermission: No activity available.");
            return;
        }

        if ((int)Build.VERSION.SdkInt < 33)
        {
            listener?.OnPermissionResult(true);
            return;
        }

        if (HasSystemPermission())
        {
            listener?.OnPermissionResult(true);
            return;
        }

        _permissionListener = listener;
        activity.StartActivity(new Android.Content.Intent(activity, typeof(PushPermissionActivity)));
    }

    public static void RequestNotificationPermission(Activity activity, PushNotifications.IPermissionListener? listener)
    {
        _currentActivity = activity;
        RequestNotificationPermission(listener);
    }

    internal static void HandlePermissionResult(bool granted)
    {
        var listener = _permissionListener;
        _permissionListener = null;
        Log.Debug(LogTag, $"HandlePermissionResult: granted={granted}");
        listener?.OnPermissionResult(granted);
    }

    public static void SetNotificationCustomizer(PushNotifications.INotificationCustomizer? customizer)
    {
        PushKernel.NotificationCustomizer = customizer is null ? null : new NotificationCustomizerProxy(customizer);
    }

    public static void SetForegroundListener(System.Action<PushNotificationData> listener)
    {
        PushKernel.ForegroundNotificationListener = new ForegroundListenerProxy(listener);
    }

    public static void SetOpenedListener(System.Action<PushNotificationData> listener)
    {
        PushKernel.OpenedNotificationListener = new OpenedListenerProxy(listener);
        // Cold-start: the notification intent arrived in OnCreate before this listener
        // was registered. Check the current activity's intent and replay it now.
        var activity = GetCurrentActivity();
        if (activity != null)
            TryHandleOpenedIntent(activity.Intent);
    }

    public static void SetBackgroundListener(System.Action<PushNotificationData> listener)
    {
        PushKernel.BackgroundNotificationListener = new BackgroundListenerProxy(listener);
    }

    private const string ActionNotificationOpened = "com.appambit.sdk.NOTIFICATION_OPENED";

    internal static void TryHandleOpenedIntent(Intent? intent)
    {
        if (intent == null || intent.Action != ActionNotificationOpened) return;
        var context = (Context?)GetCurrentActivity() ?? Android.App.Application.Context;
        PushKernel.HandleNotificationOpened(context, intent);
        intent.SetAction(null);
    }
}
