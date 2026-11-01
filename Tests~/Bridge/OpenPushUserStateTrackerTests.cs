using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushUserStateTrackerTests
    {
        [Test]
        public void FiresOnlyWhenTheIdentityPairChanges()
        {
            var tracker = new OpenPushUserStateTracker(); var seen = new List<string>();
            tracker.Changed += s => seen.Add(s.ExternalId + "|" + s.SubscriptionId);
            tracker.OnSubscription(new PushSubscriptionState());                 // both null: silent
            tracker.OnSubscription(new PushSubscriptionState { Id = "sub_1" });
            tracker.OnSubscription(new PushSubscriptionState { Id = "sub_1", Token = "t" }); // same pair
            tracker.OnExternalId("player-9");
            tracker.OnExternalId(null);
            CollectionAssert.AreEqual(new[] { "|sub_1", "player-9|sub_1", "|sub_1" }, seen);
        }

        [Test]
        public void SeedDoesNotFire()
        {
            var tracker = new OpenPushUserStateTracker(); var fired = false;
            tracker.Changed += _ => fired = true;
            tracker.Seed("p", "s");
            tracker.OnSubscription(new PushSubscriptionState { Id = "s" });
            Assert.IsFalse(fired);
        }

        [TestCase(true, false, null, OpenPushNotificationTypes.Subscribed)]
        [TestCase(false, true, null, OpenPushNotificationTypes.NeverPrompted)]
        [TestCase(false, false, null, OpenPushNotificationTypes.NoPermission)]
        [TestCase(false, true, -19, OpenPushNotificationTypes.PromptUnanswered)]
        [TestCase(true, false, -2, OpenPushNotificationTypes.Subscribed)]
        public void ComputesPreOptOutNotificationTypes(bool permission, bool canRequest, int? status, int expected)
            => Assert.AreEqual(expected, OpenPushNotificationTypesCalculator.Compute(permission, canRequest, status));
    }
}
