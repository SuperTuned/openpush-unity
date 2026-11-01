using OpenPushSDK.Core;

namespace OpenPushSDK
{
    /// <summary>Turns settings values into the facade's snapshot. Pure, so it is tested headless.</summary>
    internal static class OpenPushSettingsSnapshotReader
    {
        internal static OpenPushSettingsSnapshot From(string sdkKey, string appId, string appGroupIdentifier,
                                                      bool injectServiceExtension, string bundleId,
                                                      OpenPushAndroidPushDelivery delivery, bool relogin)
        {
            string group = null;
            if (injectServiceExtension)
                group = string.IsNullOrWhiteSpace(appGroupIdentifier) ? "group." + bundleId + ".openpush" : appGroupIdentifier.Trim();
            return new OpenPushSettingsSnapshot
            {
                SdkKey = sdkKey, AppId = appId, AppGroup = group,
                AndroidPushDelivery = delivery, ReloginWithoutAuthHash = relogin,
            };
        }
    }
}
