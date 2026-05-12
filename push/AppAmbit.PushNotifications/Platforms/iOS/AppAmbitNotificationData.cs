#if IOS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;

namespace AppAmbit.PushNotifications;

/// <summary>
/// Wraps the ObjC AppAmbitNotification payload delivered to a Notification Service Extension.
/// </summary>
[SupportedOSPlatform("ios12.0")]
public sealed class AppAmbitNotificationData
{
    private static readonly IntPtr _selTitle    = Selector.GetHandle("title");
    private static readonly IntPtr _selSubtitle = Selector.GetHandle("subtitle");
    private static readonly IntPtr _selBody     = Selector.GetHandle("body");
    private static readonly IntPtr _selImageUrl = Selector.GetHandle("imageUrl");
    private static readonly IntPtr _selData     = Selector.GetHandle("data");

    internal IntPtr Handle { get; }

    internal AppAmbitNotificationData(IntPtr handle) => Handle = handle;

    public string? Title    => GetString(_selTitle);
    public string? Subtitle => GetString(_selSubtitle);
    public string? Body     => GetString(_selBody);
    public string? ImageUrl => GetString(_selImageUrl);

    public NSDictionary Data
    {
        get
        {
            var ptr = objc_msgSend_ptr(Handle, _selData);
            return ptr != IntPtr.Zero
                ? Runtime.GetNSObject<NSDictionary>(ptr) ?? new NSDictionary()
                : new NSDictionary();
        }
    }

    private string? GetString(IntPtr sel)
    {
        var ptr = objc_msgSend_ptr(Handle, sel);
        return ptr != IntPtr.Zero ? NSString.FromHandle(ptr) : null;
    }

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern IntPtr objc_msgSend_ptr(IntPtr receiver, IntPtr selector);
}
#endif
