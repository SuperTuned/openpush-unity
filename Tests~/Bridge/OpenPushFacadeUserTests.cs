using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushFacadeUserTests : FacadeTestBase
    {
        [Test]
        public void TagsAndAliasesForwardAndIgnoreEmptyInput()
        {
            InitializeAndPump();
            OpenPush.User.AddTag("level", "3");
            OpenPush.User.AddTags(new Dictionary<string, string>());
            OpenPush.User.RemoveTags();
            OpenPush.User.AddAlias("steam", "765");
            OpenPush.User.RemoveAlias("steam");
            CollectionAssert.AreEqual(new[] { "Initialize app-1", "AddTags level", "AddAliases steam", "RemoveAliases steam" }, Platform.Calls);
            Assert.AreEqual("3", OpenPush.User.GetTags()["level"]);
        }

        [Test]
        public void SubscriptionEventsAreBufferedUntilTheFirstSubscriber()
        {
            InitializeAndPump();
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState { Id = "sub_1", Token = "t", OptedIn = true });
            Main.Pump();
            PushSubscriptionState seen = null;
            OpenPush.PushSubscription.Changed += s => seen = s;
            Main.Pump();
            Assert.AreEqual("sub_1", seen.Id);
        }

        [Test]
        public void ChangedWithPreviousCarriesBothStatesAndUnsubscribes()
        {
            InitializeAndPump();
            var seen = new List<string>();
            System.Action<PushSubscriptionState, PushSubscriptionState> handler = (p, c) => seen.Add((p?.OptedIn ?? false) + "->" + c.OptedIn);
            OpenPush.PushSubscription.ChangedWithPrevious += handler;
            Platform.SubscriptionFake.RaiseChanged(new PushSubscriptionState { OptedIn = false }, new PushSubscriptionState { OptedIn = true });
            Main.Pump();
            OpenPush.PushSubscription.ChangedWithPrevious -= handler;
            Platform.SubscriptionFake.RaiseChanged(new PushSubscriptionState { OptedIn = true }, new PushSubscriptionState { OptedIn = false });
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "False->True" }, seen);
        }

        [Test]
        public void UserChangedFollowsSubscriptionIdAndLogin()
        {
            InitializeAndPump();
            var seen = new List<string>();
            OpenPush.User.Changed += s => seen.Add(s.ExternalId + "|" + s.SubscriptionId);
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState { Id = "sub_1" });
            Main.Pump();
            OpenPush.Login("player-9");
            Platform.CoreFake.PendingIdentity(OpenPushIdentityResult.Ok());
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "|sub_1", "player-9|sub_1" }, seen);
        }

        [Test]
        public void RejectedLoginDoesNotAdoptTheExternalId()
        {
            InitializeAndPump();
            var seen = new List<string>();
            OpenPush.User.Changed += s => seen.Add(s.ExternalId);
            OpenPush.Login("player-9");
            Platform.CoreFake.PendingIdentity(OpenPushIdentityResult.Rejected("hash required"));
            Main.Pump();
            Assert.IsEmpty(seen);
        }

        [Test]
        public void AccessorReadsThePlatformAndSharesTheObserverList()
        {
            InitializeAndPump();
            Platform.SubscriptionFake.Id = "sub_1"; Platform.SubscriptionFake.Token = "t"; Platform.SubscriptionFake.Status = -18;
            var snapshot = OpenPush.User.PushSubscription.Snapshot();
            Assert.AreEqual("sub_1", snapshot.Id);
            Assert.AreEqual(-18, snapshot.Status);
            var hits = 0;
            System.Action<PushSubscriptionState> handler = _ => hits++;
            OpenPush.User.PushSubscription.Changed += handler;
            OpenPush.PushSubscription.Changed -= handler;   // one list, two spellings
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState());
            Main.Pump();
            Assert.AreEqual(0, hits);
        }

        [Test]
        public void OptInOptOutForwardAndRequireInitialize()
        {
            Assert.Throws<System.InvalidOperationException>(() => OpenPush.PushSubscription.OptIn());
            InitializeAndPump();
            OpenPush.PushSubscription.OptOut();
            OpenPush.User.PushSubscription.OptIn();
            CollectionAssert.Contains(Platform.Calls, "OptOut");
            CollectionAssert.Contains(Platform.Calls, "OptIn");
        }
    }
}
