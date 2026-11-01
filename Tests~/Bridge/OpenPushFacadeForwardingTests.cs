using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// ClearToken and accessor-observer forwarding over the platform facade.
    public class OpenPushFacadeForwardingTests : FacadeTestBase
    {
        [Test]
        public void ClearTokenRequiresInitializeAndForwardsOnce()
        {
            Assert.Throws<System.InvalidOperationException>(() => OpenPush.ClearToken());
            InitializeAndPump();
            OpenPush.ClearToken();
            Assert.AreEqual(1, Platform.Calls.FindAll(c => c == "ClearToken").Count);
        }

        [Test]
        public void AHandlerAddedThroughTheAccessorReceivesChanges()
        {
            InitializeAndPump();
            PushSubscriptionState seen = null;
            OpenPush.User.PushSubscription.Changed += s => seen = s;
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState { Id = "sub_1", OptedIn = true });
            Main.Pump();
            Assert.IsNotNull(seen);
            Assert.AreEqual("sub_1", seen.Id);
            Assert.IsTrue(seen.OptedIn);
        }

        [Test]
        public void RemovingThroughTheAccessorStopsDelivery()
        {
            InitializeAndPump();
            var hits = 0;
            System.Action<PushSubscriptionState> handler = _ => hits++;
            OpenPush.User.PushSubscription.Changed += handler;
            OpenPush.User.PushSubscription.Changed -= handler;
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState());
            Main.Pump();
            Assert.AreEqual(0, hits);
        }
    }
}
