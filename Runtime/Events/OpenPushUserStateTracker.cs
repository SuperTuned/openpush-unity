using System;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Neither native SDK exposes a user observer, so User.Changed is derived here from the
    /// subscription id (subscription observations) and the external id (Login/Logout results).
    /// </summary>
    internal sealed class OpenPushUserStateTracker
    {
        private readonly object _gate = new object();
        private string _externalId;
        private string _subscriptionId;

        public event Action<OpenPushUserState> Changed;

        public void Seed(string externalId, string subscriptionId)
        { lock (_gate) { _externalId = externalId; _subscriptionId = subscriptionId; } }

        public void OnSubscription(PushSubscriptionState current) => Update(null, false, current?.Id, true);

        public void OnExternalId(string externalId) => Update(externalId, true, null, false);

        public void Reset() { lock (_gate) { _externalId = null; _subscriptionId = null; } }

        private void Update(string externalId, bool setExternal, string subscriptionId, bool setSubscription)
        {
            OpenPushUserState state = null;
            lock (_gate)
            {
                var nextExternal = setExternal ? (string.IsNullOrEmpty(externalId) ? null : externalId) : _externalId;
                var nextSubscription = setSubscription ? subscriptionId : _subscriptionId;
                if (nextExternal == _externalId && nextSubscription == _subscriptionId) return;
                _externalId = nextExternal; _subscriptionId = nextSubscription;
                if (_externalId == null && _subscriptionId == null) return;
                state = new OpenPushUserState { ExternalId = _externalId, SubscriptionId = _subscriptionId };
            }
            Changed?.Invoke(state);
        }
    }
}
