namespace OpenPushSDK.Core
{
    /// <summary>Outcome of handing a provider data map to the Android Live Update renderer.</summary>
    public enum OpenPushLiveUpdateResult
    {
        Unsupported,
        NotLiveUpdate,
        Displayed,
        Ended,
        Dismissed,
        Invalid,
        /// <summary>The frame repeated or preceded one already applied, so it was dropped.</summary>
        Stale,
    }

    public static class OpenPushLiveUpdateResultExtensions
    {
        /// <summary>Handled results must not fall through to ordinary notification display.</summary>
        public static bool IsHandled(this OpenPushLiveUpdateResult result)
            => result != OpenPushLiveUpdateResult.Unsupported &&
               result != OpenPushLiveUpdateResult.NotLiveUpdate;
    }

    /// <summary>Android's current presentation capability for an eligible Live Update.</summary>
    public enum OpenPushLiveUpdateCapability
    {
        UnsupportedPlatform,
        FallbackOngoing,
        Eligible,
        PromotionDisabled,
    }
}
