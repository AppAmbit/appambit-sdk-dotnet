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

    public static void Start(Context context, bool enableNotifications)
    {
        if (context == null) throw new System.ArgumentNullException(nameof(context));

        var appContext = context.ApplicationContext;

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

    public static void SetNotificationsEnabled(Context context, bool enabled)
    {
        if (context == null) throw new System.ArgumentNullException(nameof(context));

        try
        {
            PushKernel.SetNotificationsEnabled(context.ApplicationContext, enabled);
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

    public static bool IsNotificationsEnabled(Context context) =>
        PushKernel.IsNotificationsEnabled(context.ApplicationContext);

    public static void RequestNotificationPermission(ActivityBase activity, PushNotifications.IPermissionListener? listener) =>
        PushKernel.RequestNotificationPermission(activity, listener is null ? null : new PermissionListenerProxy(listener));

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
            var managedNotification = new AppAmbitNotification(
                notification.Title,
                notification.Body,
                notification.Color,
                notification.SmallIconName,
                notification.Data);

            Managed.Customize(context, builder, managedNotification);
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
