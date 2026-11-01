using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>notification_types before the explicit opt-out is applied (server semantics unchanged).</summary>
    internal static class OpenPushNotificationTypesCalculator
    {
        internal static int Compute(bool permission, bool canRequestPermission, int? nativeStatus)
        {
            if (nativeStatus.HasValue && nativeStatus.Value != OpenPushNotificationTypes.OptedOut)
                return nativeStatus.Value;
            if (permission) return OpenPushNotificationTypes.Subscribed;
            return canRequestPermission ? OpenPushNotificationTypes.NeverPrompted : OpenPushNotificationTypes.NoPermission;
        }
    }
}
