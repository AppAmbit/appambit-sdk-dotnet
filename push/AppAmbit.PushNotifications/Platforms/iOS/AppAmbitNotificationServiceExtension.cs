#if IOS
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;
using UserNotifications;

namespace AppAmbit.PushNotifications;

/// <summary>
/// Base class for AppAmbit Notification Service Extensions.
/// Subclass this in your NSE project instead of UNNotificationServiceExtension.
/// Mirrors the Swift AppAmbitNotificationService open class from the iOS SDK.
/// </summary>
[SupportedOSPlatform("ios12.0")]
public abstract class AppAmbitNotificationServiceExtension : UNNotificationServiceExtension
{
    private Action<UNNotificationContent>? _contentHandler;
    private UNMutableNotificationContent? _bestAttemptContent;

    [Export("init")]
    protected AppAmbitNotificationServiceExtension() : base(NSObjectFlag.Empty) { }

    protected AppAmbitNotificationServiceExtension(NSObjectFlag t) : base(t) { }

    protected AppAmbitNotificationServiceExtension(IntPtr handle) : base(handle) { }

    public override void DidReceiveNotificationRequest(
        UNNotificationRequest request,
        Action<UNNotificationContent> contentHandler)
    {
        _contentHandler      = contentHandler;
        _bestAttemptContent  = (UNMutableNotificationContent)request.Content.MutableCopy();

        OnNotificationArrived(request);

        AppAmbitNotificationProcessor.Process(request, contentHandler, HandlePayload);
    }

    public override void TimeWillExpire()
    {
        OnTimeExpiring();
        if (_bestAttemptContent is not null)
            _contentHandler?.Invoke(_bestAttemptContent);
    }

    /// <summary>
    /// Called when the notification arrives, before any processing begins.
    /// </summary>
    protected virtual void OnNotificationArrived(UNNotificationRequest request) { }

    /// <summary>
    /// Called synchronously during processing. Mutate <paramref name="content"/> to change
    /// the title, body, badge, or attachments before the notification is displayed.
    /// </summary>
    protected virtual void HandlePayload(
        AppAmbitNotificationData notification,
        UNMutableNotificationContent content) { }

    /// <summary>
    /// Called when iOS is about to terminate the extension (30-second limit reached).
    /// The base class delivers the best attempt content after this returns.
    /// </summary>
    protected virtual void OnTimeExpiring() { }
}
#endif
