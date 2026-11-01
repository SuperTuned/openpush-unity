using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushFacadeInAppAndLiveTests : FacadeTestBase
    {
        [Test]
        public void TriggersSerializeAndForward()
        {
            InitializeAndPump();
            OpenPush.InAppMessages.AddTrigger("level", 3);
            OpenPush.InAppMessages.AddTriggers(null);
            OpenPush.InAppMessages.RemoveTriggers("level", "plan");
            OpenPush.InAppMessages.ClearTriggers();
            OpenPush.InAppMessages.Fetch();
            OpenPush.InAppMessages.Paused = true;
            CollectionAssert.IsSubsetOf(new[] { "AddTriggers {\"level\":3}", "RemoveTriggers level,plan", "ClearTriggers", "Fetch" }, Platform.Calls);
            Assert.IsTrue(Platform.InAppFake.Paused);
        }

        [Test]
        public void LifecycleAndClickEventsAreBufferedAndUnsubscribable()
        {
            InitializeAndPump();
            Platform.InAppFake.RaiseLifecycle("iam_1", "willDisplay");
            Main.Pump();
            var seen = new List<string>();
            System.Action<string, string> lifecycle = (id, phase) => seen.Add(id + ":" + phase);
            OpenPush.InAppMessages.Lifecycle += lifecycle;
            Main.Pump();
            OpenPush.InAppMessages.Clicked += (id, click) => seen.Add(id + ":" + click.ActionId);
            Platform.InAppFake.RaiseClicked("iam_1", new OpenPushInAppClick { ActionId = "claim" });
            OpenPush.InAppMessages.Lifecycle -= lifecycle;
            Platform.InAppFake.RaiseLifecycle("iam_1", "didDismiss");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "iam_1:willDisplay", "iam_1:claim" }, seen);
        }

#pragma warning disable CS0618
        [Test]
        public void ResetDisplayStateIsAnObsoleteNoOp()
        {
            InitializeAndPump();
            Assert.DoesNotThrow(OpenPush.InAppMessages.ResetDisplayState);
        }
#pragma warning restore CS0618

        [Test]
        public void LiveActivitiesForwardAndMarshalCompletion()
        {
            InitializeAndPump();
            bool? ok = null;
            OpenPush.LiveActivities.SetupDefault();
            OpenPush.LiveActivities.StartDefault("a1", new Dictionary<string, object> { ["team"] = "red" }, new Dictionary<string, object> { ["score"] = 1 });
            OpenPush.LiveActivities.Enter("a1", "tok", "DefaultLiveActivityAttributes", r => ok = r);
            Platform.LiveActivitiesFake.Pending(true);
            Assert.IsNull(ok);
            Main.Pump();
            Assert.AreEqual(true, ok);
            OpenPush.LiveActivities.RemovePushToStartToken("DefaultLiveActivityAttributes");
            CollectionAssert.IsSubsetOf(new[] { "SetupDefault True True", "StartDefault a1 {\"team\":\"red\"} {\"score\":1}",
                "Enter a1 tok DefaultLiveActivityAttributes", "RemovePushToStartToken DefaultLiveActivityAttributes" }, Platform.Calls);
        }

        [Test]
        public void LiveUpdatesForward()
        {
            InitializeAndPump();
            var result = OpenPush.LiveUpdates.HandleAndroidPayload(new Dictionary<string, string> { ["op_live_update"] = "{}" }, "c1");
            Assert.AreEqual(OpenPushLiveUpdateResult.Displayed, result);
            CollectionAssert.Contains(Platform.Calls, "HandleAndroidPayload {\"op_live_update\":\"{}\"} c1");
            Assert.AreEqual(OpenPushLiveUpdateCapability.Eligible, OpenPush.LiveUpdates.Capability);
        }
    }
}
