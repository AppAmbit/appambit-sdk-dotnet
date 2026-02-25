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
    internal static string? _lastPushToken;
    internal const string LogTag = PushNotifications.LogTag;

    private static Activity? _currentActivity;

    // Hold reference to listener to prevent GC
    internal static PushNotifications.IPermissionListener? _permissionListener;

    internal static void SetCurrentActivity(Activity activity) => _currentActivity = activity;
    internal static void ClearCurrentActivity(Activity activity) { if (_currentActivity == activity) _currentActivity = null; }

    private static bool _lifecycleRegistered;

    // Try to get current activity from MAUI Platform using reflection (to avoid hard dependency)
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

    public static void Start(Context? context)
    {
        var targetContext = context ?? GetCurrentActivity() ?? Android.App.Application.Context;
        if (targetContext == null) throw new System.ArgumentNullException(nameof(context), "Context could not be implicitly resolved.");

        // Attempt to capture activity if context is one
        if (targetContext is Activity activity)
        {
            _currentActivity = activity;
        }

        var appContext = targetContext.ApplicationContext;
        InternalStart(appContext ?? targetContext);
    }
    
    // Explicit Init for Activity
    public static void Init(Activity activity)
    {
        _currentActivity = activity;
    }

    private static void InternalStart(Context appContext)
    {
        // Explicitly initialize Firebase before any SDK call.
        // FirebaseApp.InitializeApp is a no-op if already initialized, so this is safe.
        // This avoids relying solely on FirebaseInitProvider auto-init timing.
        try
        {
            if (Firebase.FirebaseApp.Instance == null)
            {
                Firebase.FirebaseApp.InitializeApp(appContext);
                Log.Debug(LogTag, "Firebase explicitly initialized.");
            }
        }
        catch (Exception ex)
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
}

public sealed class TokenListenerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.ITokenListener
{
    private readonly Android.Content.Context _context;

    public TokenListenerProxy() { _context = Android.App.Application.Context; }
    public TokenListenerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { _context = Android.App.Application.Context; }

    public TokenListenerProxy(Android.Content.Context context)
    {
        _context = context;
    }

    public void OnNewToken(string token)
    {
        AppAmbit.PushNotifications.PushNotificationsAndroid._lastPushToken = token;
        Android.Util.Log.Debug(AppAmbit.PushNotifications.PushNotificationsAndroid.LogTag, $"FCM token cached: {token.Substring(0, System.Math.Min(10, token.Length))}...");
    }
}

public sealed class PermissionResultCallback : Java.Lang.Object, AndroidX.Activity.Result.IActivityResultCallback
{
    private readonly AppAmbit.PushNotifications.PushPermissionActivity? _activity;

    public PermissionResultCallback() { }
    public PermissionResultCallback(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public PermissionResultCallback(AppAmbit.PushNotifications.PushPermissionActivity activity)
    {
        _activity = activity;
    }

    public void OnActivityResult(Java.Lang.Object? result)
    {
        var granted = result is Java.Lang.Boolean b && b.BooleanValue();
        Android.Util.Log.Debug("AppAmbitPushSDKNET", $"PushPermissionActivity: OnActivityResult - granted={granted}");
        AppAmbit.PushNotifications.PushNotificationsAndroid.HandlePermissionResult(granted);
        _activity?.Finish();
    }
}

public sealed class PushLifecycleCallbacks : Java.Lang.Object, Android.App.Application.IActivityLifecycleCallbacks
{
    public PushLifecycleCallbacks() { }
    public PushLifecycleCallbacks(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public void OnActivityCreated(Android.App.Activity activity, Android.OS.Bundle? savedInstanceState) => AppAmbit.PushNotifications.PushNotificationsAndroid.SetCurrentActivity(activity);
    public void OnActivityResumed(Android.App.Activity activity) => AppAmbit.PushNotifications.PushNotificationsAndroid.SetCurrentActivity(activity);
    public void OnActivityDestroyed(Android.App.Activity activity) => AppAmbit.PushNotifications.PushNotificationsAndroid.ClearCurrentActivity(activity);
    public void OnActivityPaused(Android.App.Activity activity) { }
    public void OnActivitySaveInstanceState(Android.App.Activity activity, Android.OS.Bundle outState) { }
    public void OnActivityStarted(Android.App.Activity activity) { }
    public void OnActivityStopped(Android.App.Activity activity) { }
}

public sealed class NotificationCustomizerProxy : Java.Lang.Object, Com.Appambit.Sdk.PushKernel.INotificationCustomizer
{
    public AppAmbit.PushNotifications.PushNotifications.INotificationCustomizer? Managed { get; }

    public NotificationCustomizerProxy() { }
    public NotificationCustomizerProxy(System.IntPtr handle, Android.Runtime.JniHandleOwnership transfer) : base(handle, transfer) { }

    public NotificationCustomizerProxy(AppAmbit.PushNotifications.PushNotifications.INotificationCustomizer managed)
    {
        Managed = managed;
    }

    public void Customize(Android.Content.Context context, AndroidX.Core.App.NotificationCompat.Builder builder, Com.Appambit.Sdk.Models.AppAmbitNotification notification)
    {
        if (Managed == null) return;
        var managedNotification = new AppAmbit.PushNotifications.PushNotificationData(
            notification.Title,
            notification.Body,
            notification.Color,
            notification.SmallIconName,
            notification.Data);

        Managed.Customize((object)context, (object)builder, managedNotification);
    }
}
