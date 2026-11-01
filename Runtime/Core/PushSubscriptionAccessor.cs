using System;

namespace OpenPushSDK.Core
{
    /// <summary>This device's subscription snapshot and controls, read live from the native SDK.</summary>
    public sealed class PushSubscriptionAccessor
    {
        internal PushSubscriptionAccessor() { }
        public string Id => OpenPush.Platform.PushSubscription.Id;
        public string Token => OpenPush.Platform.PushSubscription.Token;
        public bool OptedIn => OpenPush.Platform.PushSubscription.OptedIn;
        public void OptIn() => OpenPush.PushSubscription.OptIn();
        public void OptOut() => OpenPush.PushSubscription.OptOut();

        public PushSubscriptionState Snapshot()
        {
            var s = OpenPush.Platform.PushSubscription;
            return new PushSubscriptionState { Id = s.Id, Token = s.Token, OptedIn = s.OptedIn, Status = s.Status };
        }

        /// <summary>One observer list, two spellings: the same list as OpenPush.PushSubscription.Changed.</summary>
        public event Action<PushSubscriptionState> Changed
        {
            add => OpenPush.PushSubscription.Changed += value;
            remove => OpenPush.PushSubscription.Changed -= value;
        }
    }
}
