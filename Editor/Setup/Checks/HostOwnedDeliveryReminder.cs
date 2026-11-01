using OpenPushSDK.Core;
namespace OpenPushSDK.Editor
{
    public sealed class HostOwnedDeliveryReminder : OpenPushSetupCheck
    {
        public override int Order => 64;
        public override string Title => "Android delivery mode";
        public override bool Passes() => true;
        public override string Detail
        {
            get
            {
                var s = OpenPushSetupAssets.Loadable();
                if (s == null || s.AndroidPushDelivery == OpenPushAndroidPushDelivery.Automatic)
                    return "Automatic: OpenPush fetches the FCM token, renders notifications and reports taps. No code needed.";
                return "Forward (also accepted as HostOwned): your own FirebaseMessagingService receives every push. Call OpenPush.Register(token) on every new token, "
                     + "Notifications.ProcessForeground(data) for foreground messages, Notifications.ProcessClick(data) for taps "
                     + "and Notifications.ProcessDisplayed(data) after your service posts a notification.";
            }
        }
    }
}
