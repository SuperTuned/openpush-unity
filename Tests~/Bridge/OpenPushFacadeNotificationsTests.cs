using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushFacadeNotificationsTests : FacadeTestBase
    {
        private static OpenPushForegroundEvent NativeForeground(List<string> completions, string id = "fg-1")
            => new OpenPushForegroundEvent
            {
                MessageId = "m", Data = new Dictionary<string, string>(), Notification = OpenPushNotification.From(null),
                CompletionId = id, Completer = (cid, display) => completions.Add(cid + ":" + display), Displayer = _ => { },
            };

        // RF3
        [Test]
        public void ForegroundWithoutSubscriberCompletesDisplayImmediately()
        {
            InitializeAndPump();
            var completions = new List<string>();
            Platform.NotificationsFake.RaiseForeground(NativeForeground(completions));
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "fg-1:True" }, completions);
        }

        [Test]
        public void ForegroundHandlerPreventDefaultSuppressesOnce()
        {
            InitializeAndPump();
            var completions = new List<string>();
            OpenPush.Notifications.ForegroundWillDisplay += e => e.PreventDefault();
            Platform.NotificationsFake.RaiseForeground(NativeForeground(completions));
            Assert.IsEmpty(completions, "nothing completes off the main thread");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "fg-1:False" }, completions);
        }

        [Test]
        public void ProcessForegroundRunsHandlersSynchronouslyAndForwards()
        {
            InitializeAndPump();
            OpenPush.Notifications.ForegroundWillDisplay += e => e.PreventDefault();
            var prevented = OpenPush.Notifications.ProcessForeground(new Dictionary<string, string> { ["op_message_id"] = "m1" });
            Assert.IsTrue(prevented);
            CollectionAssert.Contains(Platform.Calls, "ProcessForeground {\"op_message_id\":\"m1\"}");
        }

        [Test]
        public void ColdStartClickReachesALateSubscriber()
        {
            InitializeAndPump();
            Platform.NotificationsFake.RaiseClicked(new OpenPushClickEvent { MessageId = "cold" });
            Main.Pump();
            OpenPushClickEvent seen = null;
            OpenPush.Notifications.Clicked += c => seen = c;
            Main.Pump();
            Assert.AreEqual("cold", seen.MessageId);
        }

        [Test]
        public void PermissionRequestCompletesOnTheMainThreadAndAsync()
        {
            InitializeAndPump();
            bool? granted = null;
            OpenPush.Notifications.RequestPermission(true, g => granted = g);
            CollectionAssert.Contains(Platform.Calls, "RequestPermission True");
            Platform.NotificationsFake.PendingPermission(true);
            Assert.IsNull(granted);
            Main.Pump();
            Assert.AreEqual(true, granted);

            var task = OpenPush.Notifications.RequestPermissionAsync();
            Platform.NotificationsFake.PendingPermission(false);
            Main.Pump();
            Assert.IsTrue(task.IsCompleted);
            Assert.IsFalse(task.Result);
        }

        [Test]
        public void NotificationTypesIsComputedFromPermissionAndStatus()
        {
            Platform.NotificationsFake.Permission = false;
            Platform.NotificationsFake.CanRequestPermission = true;
            Assert.AreEqual(OpenPushNotificationTypes.NeverPrompted, OpenPush.Notifications.NotificationTypes);
            Platform.NotificationsFake.Permission = true;
            Assert.AreEqual(OpenPushNotificationTypes.Subscribed, OpenPush.Notifications.NotificationTypes);
        }

        [Test]
        public void ShadeAndBadgeCallsForward()
        {
            InitializeAndPump();
            OpenPush.Notifications.ClearAll();
            OpenPush.Notifications.Remove("m1");
            OpenPush.Notifications.SetBadgeCount(3);
            OpenPush.Notifications.ProcessClick(new Dictionary<string, string> { ["op_message_id"] = "m2" });
            OpenPush.Notifications.ProcessDisplayed(new Dictionary<string, string> { ["op_message_id"] = "m3" });
            OpenPush.Notifications.OnAppForegrounded();
            CollectionAssert.IsSubsetOf(new[] { "ClearAll", "Remove m1", "SetBadgeCount 3",
                "ProcessClick {\"op_message_id\":\"m2\"}", "ProcessDisplayed {\"op_message_id\":\"m3\"}", "OnAppForegrounded" }, Platform.Calls);
        }

        [Test]
        public void PermissionChangedIsBuffered()
        {
            InitializeAndPump();
            Platform.NotificationsFake.RaisePermission(true);
            Main.Pump();
            bool? seen = null;
            OpenPush.Notifications.PermissionChanged += g => seen = g;
            Main.Pump();
            Assert.AreEqual(true, seen);
        }
    }
}
