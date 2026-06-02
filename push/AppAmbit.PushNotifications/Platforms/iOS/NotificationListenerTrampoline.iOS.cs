#if IOS
using System;
using System.Runtime.InteropServices;
using System.Diagnostics;
using ObjCRuntime;
using Foundation;
using UIKit;

namespace AppAmbit.PushNotifications;

internal static partial class PushNotificationsIos
{
    private static Action<PushNotificationData>? _foregroundAction;
    private static Action<PushNotificationData>? _openedAction;
    private static IntPtr _notificationListenerBlockPtr = IntPtr.Zero;
    private static object? _listenerAction;

    // Cold-start tap: when the app is fully terminated and the user taps a notification,
    // iOS hands the payload in the FinishedLaunching launch options (under
    // UIApplicationLaunchOptionsRemoteNotificationKey) instead of calling the
    // notification-center delegate. We buffer it here and deliver it to the opened
    // listener once that listener registers (which happens after launch completes).
    private static NSDictionary? _coldStartUserInfo;
    private static bool _coldStartDelivered;

    private delegate void NotificationListenerDelegate(IntPtr block, IntPtr userInfoPtr, nint state);

    [ObjCRuntime.MonoPInvokeCallback(typeof(NotificationListenerDelegate))]
    private static void NotificationListenerTrampoline(IntPtr block, IntPtr userInfoPtr, nint state)
    {
        try
        {
            var userInfo = userInfoPtr != IntPtr.Zero
                ? Runtime.GetNSObject<NSDictionary>(userInfoPtr) ?? new NSDictionary()
                : new NSDictionary();

            var data = IosNotificationMapper.ToData(userInfo);

            if ((int)state == 0) _foregroundAction?.Invoke(data);
            else                 _openedAction?.Invoke(data);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit] Error in notification listener trampoline: {ex}");
        }
    }

    private static void RegisterNativeListener()
    {
        EnsureNativeAvailable();

        if (_notificationListenerBlockPtr != IntPtr.Zero)
        {
            unsafe
            {
                BlockLiteral* old = (BlockLiteral*)_notificationListenerBlockPtr;
                old->CleanupBlock();
                Marshal.FreeHGlobal(_notificationListenerBlockPtr);
            }
            _notificationListenerBlockPtr = IntPtr.Zero;
        }

        Action<PushNotificationData, int> combined = (data, s) =>
        {
            if (s == 0) _foregroundAction?.Invoke(data);
            else        _openedAction?.Invoke(data);
        };

        _listenerAction = combined;

        unsafe
        {
            _notificationListenerBlockPtr = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
            BlockLiteral* blockPtr = (BlockLiteral*)_notificationListenerBlockPtr;
            blockPtr->SetupBlockUnsafe(NotificationListenerTrampoline, combined);
            objc_msgSend_IntPtr(_pushNotificationsClassHandle, _selSetNotificationListener, _notificationListenerBlockPtr);
        }
    }

    public static void SetForegroundListener(Action<PushNotificationData> listener)
    {
        _foregroundAction = listener;
        RegisterNativeListener();
    }

    public static void SetOpenedListener(Action<PushNotificationData> listener)
    {
        _openedAction = listener;
        RegisterNativeListener();
        FlushColdStartIfPossible();
    }

    /// <summary>
    /// Captures a cold-start launch notification (app launched by a tap while fully
    /// terminated). Call from FinishedLaunching with its launch options.
    /// </summary>
    public static void HandleColdStartLaunch(NSDictionary? launchOptions)
    {
        if (launchOptions == null) return;

        if (launchOptions[UIApplication.LaunchOptionsRemoteNotificationKey] is NSDictionary userInfo)
        {
            _coldStartUserInfo = userInfo;
            FlushColdStartIfPossible();
        }
    }

    private static void FlushColdStartIfPossible()
    {
        if (_coldStartDelivered) return;

        var userInfo = _coldStartUserInfo;
        if (userInfo == null || _openedAction == null) return;

        _coldStartDelivered = true;
        _coldStartUserInfo = null;

        var data = IosNotificationMapper.ToData(userInfo);
        _openedAction.Invoke(data);
    }
}

#endif
