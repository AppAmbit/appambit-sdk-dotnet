#if IOS
using System;
using System.Threading.Tasks;
using Foundation;
using ObjCRuntime;

namespace AppAmbit.PushNotifications;

internal static partial class PushNotificationsIos
{
    [Preserve(AllMembers = true)]
    internal sealed class TokenListenerImpl : NSObject
    {
        [Export("onNewToken:")]
        public void OnNewToken(NSString token)
        {
            var tokenStr = token.ToString();
            _lastPushToken = tokenStr;

            _ = Task.Run(async () =>
            {
                try
                {
                    var isEnabled = PushNotificationsIos.IsNotificationsEnabled();
                    await AppAmbitSdk.UpdateConsumerAsync(tokenStr, isEnabled);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{LogTag}: Error syncing token: {ex.Message}");
                }
            });
        }
    }
}

#endif
