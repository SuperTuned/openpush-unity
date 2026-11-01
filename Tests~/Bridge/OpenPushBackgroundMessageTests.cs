using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// Background (data-only) pushes — op_background "1", no title or body — are never
    /// displayed and reach OpenPush.Notifications.BackgroundMessageReceived instead.
    public class OpenPushBackgroundMessageTests : FacadeTestBase
    {
        private static Dictionary<string, string> Background() => new Dictionary<string, string>
        { ["op_message_id"] = "bg-1", ["op_background"] = "1", ["level"] = "7" };

        [Test]
        public void TheModelReadsABackgroundPushOnlyWithoutTitleAndBody()
        {
            Assert.IsTrue(OpenPushNotification.From(Background()).IsBackground);
            var titled = Background(); titled["title"] = "Hi";
            Assert.IsFalse(OpenPushNotification.From(titled).IsBackground, "a push with text is displayed");
            var bodied = Background(); bodied["body"] = "Hi";
            Assert.IsFalse(OpenPushNotification.From(bodied).IsBackground);
            var other = Background(); other["op_background"] = "0";
            Assert.IsFalse(OpenPushNotification.From(other).IsBackground);
            Assert.IsFalse(OpenPushNotification.From(new Dictionary<string, string> { ["op_message_id"] = "m" }).IsBackground);
        }

        [Test]
        public void TheRouterTurnsTheBridgeEventIntoABackgroundMessage()
        {
            var router = new OpenPushNativeEventRouter(null, null);
            var seen = new List<OpenPushBackgroundMessage>();
            router.BackgroundMessage += seen.Add;
            router.Route("background",
                "{\"message_id\":\"bg-1\",\"notification\":{\"message_id\":\"bg-1\",\"actions\":[]},"
                + "\"data\":{\"op_message_id\":\"bg-1\",\"op_background\":\"1\",\"level\":\"7\"}}");
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual("bg-1", seen[0].MessageId);
            Assert.AreEqual("7", seen[0].Data["level"]);
            Assert.IsTrue(seen[0].Notification.IsBackground);
        }

        [Test]
        public void TheWireRoundTrips()
        {
            var wire = BackgroundMessageWire.From(OpenPushWire.Parse(
                "{\"message_id\":\"bg-2\",\"data\":{\"op_message_id\":\"bg-2\",\"op_background\":\"1\"}}"));
            var again = BackgroundMessageWire.From(OpenPushWire.Parse(wire.ToJson()));
            Assert.AreEqual("bg-2", again.message_id);
            Assert.AreEqual("1", again.data["op_background"]);
        }

        [Test]
        public void APlatformBackgroundMessageReachesTheFacadeEventOnTheMainThread()
        {
            InitializeAndPump();
            var seen = new List<OpenPushBackgroundMessage>();
            OpenPush.Notifications.BackgroundMessageReceived += seen.Add;
            Platform.NotificationsFake.RaiseBackground(new BackgroundMessageWire { data = Background() }.ToPublic());
            Assert.AreEqual(0, seen.Count, "delivered through the dispatcher, not on the native thread");
            Main.Pump();
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual("bg-1", seen[0].MessageId);
        }

        [Test]
        public void OneRaisedBeforeTheFirstHandlerIsReplayedToIt()
        {
            InitializeAndPump();
            Platform.NotificationsFake.RaiseBackground(new BackgroundMessageWire { data = Background() }.ToPublic());
            Main.Pump();
            var seen = new List<OpenPushBackgroundMessage>();
            OpenPush.Notifications.BackgroundMessageReceived += seen.Add;
            Main.Pump();
            Assert.AreEqual(1, seen.Count);
        }

        [Test]
        public void AForwardedBackgroundPushSkipsTheForegroundHandlersAndIsNeverDisplayed()
        {
            InitializeAndPump();
            var foregrounds = 0;
            var background = new List<OpenPushBackgroundMessage>();
            OpenPush.Notifications.ForegroundWillDisplay += _ => foregrounds++;
            OpenPush.Notifications.BackgroundMessageReceived += background.Add;
            Assert.IsTrue(OpenPush.Notifications.ProcessForeground(Background()), "a background push is never displayed");
            Main.Pump();
            Assert.AreEqual(0, foregrounds);
            Assert.AreEqual(1, background.Count, "the platform hands it to the background listeners once");
            Assert.AreEqual(1, Platform.NotificationsFake.ProcessForegroundCalls.Count, "one received receipt");
            StringAssert.EndsWith(" True", Platform.NotificationsFake.FinishReceivedCalls[0], "recorded, so a resent copy is dropped");
            Assert.IsTrue(OpenPush.Notifications.ProcessForeground(Background()));
            Main.Pump();
            Assert.AreEqual(1, background.Count, "a resent copy is not delivered again");
        }

        [Test]
        public void TheStubHandsAForwardedBackgroundPushToTheEvent()
        {
            OpenPushPlatformRegistry.Set(new OpenPushStubPlatform());
            InitializeAndPump();
            var background = 0;
            OpenPush.Notifications.BackgroundMessageReceived += _ => background++;
            Assert.IsTrue(OpenPush.Notifications.ProcessForeground(Background()));
            Main.Pump();
            Assert.AreEqual(1, background);
        }
    }
}
