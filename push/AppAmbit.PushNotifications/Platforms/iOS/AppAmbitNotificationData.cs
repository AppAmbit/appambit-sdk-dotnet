#if IOS
using System.Runtime.Versioning;
using Foundation;

namespace AppAmbit.PushNotifications;

/// <summary>
/// Parsed AppAmbit push payload for use inside a Notification Service Extension.
/// <c>Title</c> and <c>Body</c> come from <c>aps.alert</c>;
/// <c>ImageUrl</c> from the top-level <c>"image"</c> key.
/// </summary>
[SupportedOSPlatform("ios12.0")]
public sealed class AppAmbitNotificationData
{
    public string? Title    { get; }
    public string? Body     { get; }
    public string? ImageUrl { get; }
    public NSDictionary Data { get; }

    internal AppAmbitNotificationData(NSDictionary userInfo)
    {
        var aps   = userInfo[(NSString)"aps"]   as NSDictionary;
        var alert = aps?[(NSString)"alert"]     as NSDictionary;

        Title    = (alert?[(NSString)"title"] as NSString)?.ToString();
        Body     = (alert?[(NSString)"body"]  as NSString)?.ToString();
        ImageUrl = (userInfo[(NSString)"image"] as NSString)?.ToString();

        var mutable = new NSMutableDictionary(userInfo);
        mutable.Remove((NSString)"image");
        Data = mutable;
    }
}
#endif
