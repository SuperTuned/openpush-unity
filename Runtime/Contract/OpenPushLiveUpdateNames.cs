using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>The Android live-updates module's enum names, as the adapter returns them.</summary>
    internal static class OpenPushLiveUpdateNames
    {
        internal static OpenPushLiveUpdateResult Result(string nativeName)
        {
            switch (nativeName)
            {
                case "NOT_LIVE_UPDATE": return OpenPushLiveUpdateResult.NotLiveUpdate;
                case "DISPLAYED": return OpenPushLiveUpdateResult.Displayed;
                case "ENDED": return OpenPushLiveUpdateResult.Ended;
                case "DISMISSED": return OpenPushLiveUpdateResult.Dismissed;
                case "STALE": return OpenPushLiveUpdateResult.Stale;
                case "INVALID": return OpenPushLiveUpdateResult.Invalid;
                default: return OpenPushLiveUpdateResult.Unsupported;
            }
        }

        internal static OpenPushLiveUpdateCapability Capability(string nativeName)
        {
            switch (nativeName)
            {
                case "FALLBACK_ONGOING": return OpenPushLiveUpdateCapability.FallbackOngoing;
                case "ELIGIBLE": return OpenPushLiveUpdateCapability.Eligible;
                case "PROMOTION_DISABLED": return OpenPushLiveUpdateCapability.PromotionDisabled;
                default: return OpenPushLiveUpdateCapability.UnsupportedPlatform;
            }
        }
    }
}
