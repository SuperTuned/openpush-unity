namespace OpenPushSDK.Core
{
    /// <summary>
    /// A snapshot of who this device belongs to. ExternalId is the account the
    /// host named through Login or Register; SubscriptionId is the server's id
    /// for this device, which survives a Logout — the device stays registered,
    /// anonymously. Either may be null: before the first accepted registration
    /// there is no subscription id, and an anonymous device has no external id.
    /// </summary>
    public sealed class OpenPushUserState
    {
        public string ExternalId;
        public string SubscriptionId;

        internal bool SameAs(OpenPushUserState other)
            => other != null && ExternalId == other.ExternalId &&
               SubscriptionId == other.SubscriptionId;
    }
}
