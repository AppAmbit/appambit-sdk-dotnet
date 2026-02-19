using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
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

    private static Activity? _currentActivity;

    // Hold reference to listener to prevent GC
    internal static PushNotifications.IPermissionListener? _permissionListener;

    // Try to get current activity from MAUI Platform using reflection (to avoid hard dependency)
    private static Activity? GetCurrentActivity()
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
                    var activity = currentActivityProperty.GetValue(null) as Activity;
                    if (activity != null)
                    {
                        _currentActivity = activity;
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
        if (context is Activity activity)
        {
            _currentActivity = activity;
        }

        var appContext = context.ApplicationContext;
        InternalStart(appContext ?? context);
    }
    
    // Explicit Init for Activity
    public static void Init(Activity activity)
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
        // Try to get context from stored activity, then reflection, then Application fallback
        var targetContext = context ?? GetCurrentActivity()?.ApplicationContext ?? Android.App.Application.Context;
        
        if (targetContext == null) 
        {
            Log.Error(LogTag, "SetNotificationsEnabled: Context is null and no activity initialized.");
            return;
        }

        try
        {
            PushKernel.SetNotificationsEnabled(targetContext, enabled);
        }
        catch (Java.Lang.IllegalStateException ex)
        {
            Log.Error(LogTag, $"Failed to set notifications enabled={enabled}: {ex}");
        }

        // Update consumer logic - always sync state with backend
        var token = _lastPushToken;
        _ = Task.Run(async () =>
        {
            try
            {
                // Wait a bit if token is null to give FCM time to generate it
                if (token == null && enabled)
                {
                    await Task.Delay(2000);
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
        var targetContext = context ?? GetCurrentActivity()?.ApplicationContext ?? Android.App.Application.Context;
        if (targetContext == null) return false;
        return PushKernel.IsNotificationsEnabled(targetContext);
    }

    // New parameterless/simplified methods
    public static bool HasSystemPermission()
    {
        var context = (Context?)GetCurrentActivity() ?? Android.App.Application.Context;

        if ((int)Android.OS.Build.VERSION.SdkInt < 33) return true;
        return AndroidX.Core.Content.ContextCompat.CheckSelfPermission(context, Android.Manifest.Permission.PostNotifications) == Android.Content.PM.Permission.Granted;
    }

    public static void RequestNotificationPermission(PushNotifications.IPermissionListener? listener)
    {
        var activity = GetCurrentActivity();
        if (activity == null)
        {
            Log.Error(LogTag, "RequestNotificationPermission: Activity is not initialized. Call PushNotifications.Init(activity) or Start(activity) first.");
            return;
        }
        
        // Pre-Android 13: permission is auto-granted at install time
        if ((int)Build.VERSION.SdkInt < 33)
        {
            Log.Debug(LogTag, "Pre-Android 13: notification permission auto-granted.");
            listener?.OnPermissionResult(true);
            return;
        }

        // Already granted
        if (HasSystemPermission())
        {
            Log.Debug(LogTag, "Notification permission already granted.");
            listener?.OnPermissionResult(true);
            return;
        }

        Log.Debug(LogTag, $"Launching PushPermissionActivity from: {activity.GetType().Name}");

        // Store listener - PushPermissionActivity will call HandlePermissionResult
        _permissionListener = listener;

        // Launch transparent activity that handles the permission request directly.
        // This bypasses PushKernel's Java-to-C# callback which doesn't work in .NET bindings.
        var intent = new Intent(activity, typeof(PushPermissionActivity));
        activity.StartActivity(intent);
    }

    /// <summary>
    /// Called by PushPermissionActivity when the user grants or denies permission.
    /// </summary>
    internal static void HandlePermissionResult(bool granted)
    {
        var listener = _permissionListener;
        _permissionListener = null;

        Log.Debug(LogTag, $"HandlePermissionResult: granted={granted}");
        listener?.OnPermissionResult(granted);
    }

    // Keep old signature for compatibility/internal use but forward
    public static void RequestNotificationPermission(Activity activity, PushNotifications.IPermissionListener? listener) 
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
            // Only cache the token. Do NOT sync to backend here.
            // Backend sync happens exclusively via SetNotificationsEnabled()
            // when the user explicitly enables/disables notifications.
            _lastPushToken = token;
            Log.Debug(LogTag, $"FCM token cached: {token.Substring(0, Math.Min(10, token.Length))}...");
        }
    }
}

/// <summary>
/// Transparent Activity that handles POST_NOTIFICATIONS permission request.
/// Uses AndroidX RegisterForActivityResult API for reliable callback handling.
/// This bypasses PushKernel's broken Java-to-C# callback mechanism.
/// </summary>
[Activity(
    Theme = "@android:style/Theme.Translucent.NoTitleBar",
    Exported = false,
    Name = "com.appambit.pushnotifications.PushPermissionActivity")]
public class PushPermissionActivity : AndroidX.Activity.ComponentActivity
{
    private const string Tag = "AppAmbitPushSDKNET";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        Log.Debug(Tag, "PushPermissionActivity: OnCreate - registering ActivityResult launcher");

        try
        {
            // Use the modern ActivityResult API (must be called before onStart, which OnCreate satisfies)
            var launcher = RegisterForActivityResult(
                new AndroidX.Activity.Result.Contract.ActivityResultContracts.RequestPermission(),
                new PermissionResultCallback(this));

            Log.Debug(Tag, "PushPermissionActivity: Launching permission request for POST_NOTIFICATIONS");
            launcher.Launch(Android.Manifest.Permission.PostNotifications);
        }
        catch (System.Exception ex)
        {
            Log.Error(Tag, $"PushPermissionActivity: Failed to launch permission request: {ex}");
            PushNotificationsAndroid.HandlePermissionResult(false);
            Finish();
        }
    }

    private sealed class PermissionResultCallback : Java.Lang.Object, AndroidX.Activity.Result.IActivityResultCallback
    {
        private readonly PushPermissionActivity _activity;

        public PermissionResultCallback(PushPermissionActivity activity)
        {
            _activity = activity;
        }

        public void OnActivityResult(Java.Lang.Object? result)
        {
            var granted = result is Java.Lang.Boolean b && b.BooleanValue();
            Log.Debug(Tag, $"PushPermissionActivity: OnActivityResult - granted={granted}");
            PushNotificationsAndroid.HandlePermissionResult(granted);
            _activity.Finish();
        }
    }
}
