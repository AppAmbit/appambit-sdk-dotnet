#if IOS
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;
using UserNotifications;

namespace AppAmbit.PushNotifications;

/// <summary>
/// Base class for AppAmbit Notification Service Extensions.
/// Subclass this in your NSE project instead of UNNotificationServiceExtension.
/// Mirrors the Swift AppAmbitNotificationService open class: parses the push payload,
/// downloads the image from the "image" key, attaches it, then delivers the content.
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
        _contentHandler     = contentHandler;
        _bestAttemptContent = (UNMutableNotificationContent)request.Content.MutableCopy();

        try
        {
            var content      = _bestAttemptContent;
            var notification = new AppAmbitNotificationData(request.Content.UserInfo);

            HandlePayload(notification, content);
            AttachImageThenDeliver(notification.ImageUrl, content, contentHandler);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[AppAmbit NSE] Processing failed, delivering best-attempt content: {ex.Message}");
            contentHandler(_bestAttemptContent);
        }
    }

    public override void TimeWillExpire()
    {
        OnTimeExpiring();
        if (_bestAttemptContent is not null)
            _contentHandler?.Invoke(_bestAttemptContent);
    }

    /// <summary>
    /// Called synchronously before image download. Mutate <paramref name="content"/> to change
    /// the title, body, badge, or other fields before the notification is displayed.
    /// </summary>
    protected virtual void HandlePayload(
        AppAmbitNotificationData notification,
        UNMutableNotificationContent content) { }

    /// <summary>
    /// Called when iOS is about to terminate the extension (30-second limit reached).
    /// The base class delivers the best attempt content after this returns.
    /// </summary>
    protected virtual void OnTimeExpiring() { }

    // ── Image download — mirrors PushNotificationAttachments.swift ────────────

    private static void AttachImageThenDeliver(
        string? imageUrl,
        UNMutableNotificationContent content,
        Action<UNNotificationContent> contentHandler)
    {
        if (string.IsNullOrEmpty(imageUrl))
        {
            contentHandler(content);
            return;
        }

        var url = NSUrl.FromString(imageUrl);
        if (url is null)
        {
            contentHandler(content);
            return;
        }

        NSUrlSession.SharedSession.CreateDownloadTask(url, (downloadedUrl, response, downloadError) =>
        {
            if (downloadError is null && downloadedUrl is not null)
            {
                try
                {
                    var ext      = Path.GetExtension(imageUrl);
                    if (string.IsNullOrEmpty(ext)) ext = ".tmp";
                    var localUrl = NSUrl.FromFilename(
                        Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ext));

                    var srcPath = downloadedUrl.Path;
                    var dstPath = localUrl.Path;
                    if (srcPath is not null && dstPath is not null)
                    {
                        File.Move(srcPath, dstPath, overwrite: true);
                        NSError? attachError;
                        var attachment = UNNotificationAttachment.FromIdentifier(
                            "image", localUrl, (NSDictionary?)null, out attachError);
                        if (attachment is not null && attachError is null)
                            content.Attachments = new[] { attachment };
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[AppAmbit NSE] Attachment failed: {ex.Message}");
                }
            }
            else
            {
                Debug.WriteLine($"[AppAmbit NSE] Image download failed: {downloadError?.LocalizedDescription ?? "unknown error"}");
            }

            contentHandler(content);
        }).Resume();
    }
}
#endif
