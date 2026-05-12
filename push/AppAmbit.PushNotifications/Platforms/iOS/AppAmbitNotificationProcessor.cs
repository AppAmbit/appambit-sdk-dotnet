#if IOS
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Foundation;
using ObjCRuntime;
using UserNotifications;

namespace AppAmbit.PushNotifications;

/// <summary>
/// .NET binding for the native AppAmbitNotificationProcessor ObjC class.
/// Processes a push payload, downloads any image attachment, then delivers the final content.
/// </summary>
[SupportedOSPlatform("ios12.0")]
public static class AppAmbitNotificationProcessor
{
    private static readonly IntPtr _class;
    private static readonly IntPtr _selProcess;

    static AppAmbitNotificationProcessor()
    {
        _class      = Class.GetHandle("AppAmbitNotificationProcessor");
        _selProcess = Selector.GetHandle("processRequest:contentHandler:handlePayload:");
    }

    /// <summary>
    /// Processes an incoming notification request: invokes the optional <paramref name="handlePayload"/>
    /// callback synchronously, downloads any image attachment asynchronously, then calls
    /// <paramref name="contentHandler"/> with the final content.
    /// </summary>
    public static void Process(
        UNNotificationRequest request,
        Action<UNNotificationContent> contentHandler,
        Action<AppAmbitNotificationData, UNMutableNotificationContent>? handlePayload = null)
    {
        if (_class == IntPtr.Zero)
            throw new PlatformNotSupportedException(
                "AppAmbitNotificationProcessor native class not found. " +
                "Ensure AppAmbitPushNotifications.framework is embedded in the extension bundle.");

        unsafe
        {
            var chMem = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
            try
            {
                BlockLiteral* ch = (BlockLiteral*)chMem;
                ch->SetupBlockUnsafe(ContentHandlerTrampoline, contentHandler);

                IntPtr hpMem = IntPtr.Zero;
                try
                {
                    if (handlePayload != null)
                    {
                        hpMem = Marshal.AllocHGlobal(Marshal.SizeOf<BlockLiteral>());
                        BlockLiteral* hp = (BlockLiteral*)hpMem;
                        hp->SetupBlockUnsafe(HandlePayloadTrampoline, handlePayload);
                    }

                    objc_msgSend_process(_class, _selProcess, request.Handle, chMem, hpMem);
                }
                finally
                {
                    if (hpMem != IntPtr.Zero)
                    {
                        BlockLiteral* hp = (BlockLiteral*)hpMem;
                        hp->CleanupBlock();
                        Marshal.FreeHGlobal(hpMem);
                    }
                }
            }
            finally
            {
                BlockLiteral* ch = (BlockLiteral*)chMem;
                ch->CleanupBlock();
                Marshal.FreeHGlobal(chMem);
            }
        }
    }

    // ── ObjC block trampolines (private — implementation detail) ─────────────

    private delegate void ContentHandlerDelegate(IntPtr block, IntPtr contentPtr);
    private delegate void HandlePayloadDelegate(IntPtr block, IntPtr notifPtr, IntPtr contentPtr);

    [ObjCRuntime.MonoPInvokeCallback(typeof(ContentHandlerDelegate))]
    private static void ContentHandlerTrampoline(IntPtr block, IntPtr contentPtr)
    {
        try
        {
            var action  = BlockLiteral.GetTarget<Action<UNNotificationContent>>(block);
            var content = contentPtr != IntPtr.Zero
                ? Runtime.GetNSObject<UNNotificationContent>(contentPtr) : null;
            if (action != null && content != null)
                action(content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppAmbit NSE] ContentHandlerTrampoline error: {ex}");
        }
    }

    [ObjCRuntime.MonoPInvokeCallback(typeof(HandlePayloadDelegate))]
    private static void HandlePayloadTrampoline(IntPtr block, IntPtr notifPtr, IntPtr contentPtr)
    {
        try
        {
            var action  = BlockLiteral.GetTarget<Action<AppAmbitNotificationData, UNMutableNotificationContent>>(block);
            var content = contentPtr != IntPtr.Zero
                ? Runtime.GetNSObject<UNMutableNotificationContent>(contentPtr) : null;
            if (action != null && content != null)
                action(new AppAmbitNotificationData(notifPtr), content);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[AppAmbit NSE] HandlePayloadTrampoline error: {ex}");
        }
    }

    // ── P/Invoke ─────────────────────────────────────────────────────────────

    [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
    private static extern void objc_msgSend_process(
        IntPtr receiver, IntPtr selector,
        IntPtr request, IntPtr contentHandler, IntPtr handlePayload);
}
#endif
