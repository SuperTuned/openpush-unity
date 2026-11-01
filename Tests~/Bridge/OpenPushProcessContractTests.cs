using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// The host-forwarded Process* payloads must reach C# handlers exactly once. The facade runs
    /// the foreground handlers itself; the platform raises the click (IOpenPushNotifications docs).
    public class OpenPushProcessContractTests : FacadeTestBase
    {
        private static readonly Dictionary<string, string> Payload =
            new Dictionary<string, string> { ["op_message_id"] = "m1", ["title"] = "Hi" };

        [Test]
        public void ProcessForegroundRunsTheHandlerOnceAndForwardsOnce()
        {
            InitializeAndPump();
            var seen = 0;
            OpenPush.Notifications.ForegroundWillDisplay += _ => seen++;
            OpenPush.Notifications.ProcessForeground(Payload);
            Main.Pump();
            Assert.AreEqual(1, seen, "one foreground handler invocation");
            Assert.AreEqual(1, Platform.NotificationsFake.ProcessForegroundCalls.Count, "one platform ProcessForeground call");
            Assert.AreEqual(0, Platform.NotificationsFake.ForegroundRaisedByPlatform, "the platform must not raise it again");
        }

        [Test]
        public void AResentCopyIsDroppedBeforeHandlersAndReportedAsNotToDisplay()
        {
            InitializeAndPump();
            var seen = 0;
            OpenPush.Notifications.ForegroundWillDisplay += _ => seen++;
            Assert.IsFalse(OpenPush.Notifications.ProcessForeground(Payload), "the first copy displays");
            Assert.IsTrue(OpenPush.Notifications.ProcessForeground(Payload), "the repeat must not be displayed");
            Main.Pump();
            Assert.AreEqual(1, seen, "handlers run for the first copy only");
            Assert.AreEqual(1, Platform.NotificationsFake.ProcessForegroundCalls.Count, "one received receipt");
            Assert.AreEqual(1, Platform.NotificationsFake.FinishReceivedCalls.Count, "a repeat is never claimed, so never finished");
            StringAssert.EndsWith(" True", Platform.NotificationsFake.FinishReceivedCalls[0]);
        }

        [Test]
        public void ProcessClickRaisesExactlyOneClickFromThePlatform()
        {
            InitializeAndPump();
            var clicks = new List<OpenPushClickEvent>();
            OpenPush.Notifications.Clicked += clicks.Add;
            OpenPush.Notifications.ProcessClick(Payload);
            Main.Pump();
            Assert.AreEqual(1, Platform.NotificationsFake.ProcessClickCalls.Count);
            Assert.AreEqual(1, Platform.NotificationsFake.ClicksRaisedByPlatform, "the platform raises the click");
            Assert.AreEqual(1, clicks.Count, "exactly one Clicked per ProcessClick");
            Assert.AreEqual("m1", clicks[0].MessageId);
        }

        [Test]
        public void ProcessDisplayedForwardsOnceAndRaisesNothing()
        {
            InitializeAndPump();
            var events = 0;
            OpenPush.Notifications.Clicked += _ => events++;
            OpenPush.Notifications.ForegroundWillDisplay += _ => events++;
            OpenPush.Notifications.ProcessDisplayed(Payload);
            Main.Pump();
            Assert.AreEqual(1, Platform.NotificationsFake.ProcessDisplayedCalls.Count);
            Assert.AreEqual(0, events);
        }

        [Test]
        public void TheStubHonoursTheSameContract()
        {
            OpenPushPlatformRegistry.Set(new OpenPushStubPlatform());
            InitializeAndPump();
            var foregrounds = 0; var clicks = 0;
            OpenPush.Notifications.ForegroundWillDisplay += _ => foregrounds++;
            OpenPush.Notifications.Clicked += _ => clicks++;
            OpenPush.Notifications.ProcessForeground(Payload);
            OpenPush.Notifications.ProcessClick(Payload);
            OpenPush.Notifications.ProcessDisplayed(Payload);
            Main.Pump();
            Assert.AreEqual(1, foregrounds);
            Assert.AreEqual(1, clicks);
        }
    }
}
