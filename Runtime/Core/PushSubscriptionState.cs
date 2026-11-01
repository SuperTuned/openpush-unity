namespace OpenPushSDK.Core
{
    /// <summary>
    /// A snapshot of this device's subscription. OptedIn is true when the device
    /// has a provider token, the OS currently allows notifications, and the host
    /// has not called OptOut(). It is no longer only a report —
    /// PushSubscription.OptIn/OptOut are the controls — but the response's own
    /// status field is deliberately not read back into it, because that value can
    /// only echo what was just sent.
    /// </summary>
    public sealed class PushSubscriptionState
    {
        public string Id;
        public string Token;
        public bool OptedIn;
        /// <summary>The native wire status (1 subscribed, -2 opted out, 0 no permission, -18/-19 not answered); null when the platform does not report one (Android, Editor).</summary>
        public int? Status;

        internal bool SameAs(PushSubscriptionState other)
            => other != null && Id == other.Id && Token == other.Token && OptedIn == other.OptedIn;
    }
}
