using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Compat;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// The compat assembly is only forwarding; these tests prove each alias lands on the same
    /// OpenPush call a direct call makes, by reading the fake platform's recorded calls and state.
    public class OneSignalAliasTests : FacadeTestBase
    {
        [SetUp] public void InitializeThroughTheAlias() { OneSignal.Initialize("app-1"); Main.Pump(); }

        [Test]
        public void InitializeThroughTheAliasConfiguresOpenPush()
        {
            Assert.IsTrue(OneSignal.IsInitialized);
            Assert.AreEqual("pk_asset", Platform.CoreFake.InitOptions.SdkKey);
            Assert.AreEqual(OpenPush.SdkVersion, OneSignal.SdkVersion);
            Assert.AreEqual(OpenPushVersion.Current, OneSignal.Version);
            Assert.AreEqual(OpenPush.DefaultServerUrl, OneSignal.DefaultServerUrl);
        }

        [Test]
        public void RegisterLoginLogoutForward()
        {
            OneSignal.Register("tok-1");
            OneSignal.Login("player-9");
            OneSignal.Logout();
            CollectionAssert.IsSubsetOf(new[] { "Register tok-1 ", "Login player-9 ", "Logout" }, Platform.Calls);
        }

        [Test]
        public void TheTwoArgumentRegisterAliasStillForwards()
        {
#pragma warning disable CS0618 // Deprecated, kept so migrated code keeps compiling.
            OneSignal.Register("tok-2", "player-9");
#pragma warning restore CS0618
            CollectionAssert.Contains(Platform.Calls, "Register tok-2 player-9");
        }

        [Test]
        public void FlushStaysCallableThroughTheAlias() => Assert.DoesNotThrow(OneSignal.Flush);

        [Test]
        public void TagsAndAliasesShareOneStore()
        {
            OneSignal.User.AddTag("tier", "vip");
            OneSignal.User.AddTags(new Dictionary<string, string> { { "level", "42" } });
            OneSignal.User.AddAlias("steam_id", "7656");
            Assert.AreEqual("vip", OpenPush.User.GetTags()["tier"]);
            var copy = OneSignal.User.GetTags();
            copy["tier"] = "mutated";
            Assert.AreEqual("vip", OneSignal.User.GetTags()["tier"]);
            OneSignal.User.RemoveTag("tier");
            Assert.IsFalse(OpenPush.User.GetTags().ContainsKey("tier"));
            Assert.AreEqual("7656", Platform.UserFake.Aliases["steam_id"]);
        }

        [Test]
        public void SmsNumbersForwardToOpenPushPhoneNumbers()
        {
            OneSignal.User.AddSms("0014155550123");
            OneSignal.User.RemoveSms("+14155550123");
            OneSignal.User.AddSms("not a number");
            CollectionAssert.AreEqual(new[] { "AddSms +14155550123", "RemoveSms +14155550123" },
                                      Platform.Calls.FindAll(c => c.Contains("Sms")));
        }

        [Test]
        public void SmsIsForwardedNotRefused()
        {
            foreach (var name in new[] { "AddSms", "RemoveSms" })
            {
                var method = typeof(OneSignal.User).GetMethod(name, new[] { typeof(string) });
                Assert.IsNotNull(method, name);
                Assert.IsNull(System.Attribute.GetCustomAttribute(method, typeof(System.ObsoleteAttribute)), name + " must not be refused");
            }
            Assert.IsNull(typeof(OneSignal.User).GetMethod("AddWhatsApp"), "WhatsApp has no v5 counterpart on the compatibility surface");
        }

        [Test]
        public void EmailAddressesForwardToOpenPushEmailAddresses()
        {
            OneSignal.User.AddEmail(" Player@Example.com ");
            OneSignal.User.RemoveEmail("player@example.com");
            OneSignal.User.AddEmail("not an address");
            CollectionAssert.AreEqual(new[] { "AddEmail player@example.com", "RemoveEmail player@example.com" },
                                      Platform.Calls.FindAll(c => c.Contains("Email")));
        }

        [Test]
        public void EmailIsForwardedNotRefused()
        {
            foreach (var name in new[] { "AddEmail", "RemoveEmail" })
            {
                var method = typeof(OneSignal.User).GetMethod(name, new[] { typeof(string) });
                Assert.IsNotNull(method, name);
                Assert.IsNull(System.Attribute.GetCustomAttribute(method, typeof(System.ObsoleteAttribute)), name + " must not be refused");
            }
        }

        [Test]
        public void NullBulkUpdatesAreSafe()
        {
            Assert.DoesNotThrow(() => { OneSignal.User.AddTags(null); OneSignal.User.AddAliases(null); });
        }

        [Test]
        public void StringTriggersReachTheTriggerStore()
        {
            OneSignal.InAppMessages.AddTriggers(new Dictionary<string, string> { ["plan"] = "pro" });
            CollectionAssert.Contains(Platform.Calls, "AddTriggers {\"plan\":\"pro\"}");
        }

        [Test]
        public void OutcomesAndUserEventsBecomeCustomEvents()
        {
            OneSignal.Session.AddOutcomeWithValue("tutorial_complete", 1f);
            OneSignal.User.TrackEvent("level_complete", new Dictionary<string, object> { ["level"] = 7 });
            CollectionAssert.Contains(Platform.Calls, "TrackEvent tutorial_complete {\"value\":1}");
            CollectionAssert.Contains(Platform.Calls, "TrackEvent level_complete {\"level\":7}");
        }

        [Test]
        public void ObserversShareTheOpenPushLists()
        {
            var seen = 0;
            void Handler(PushSubscriptionState _) => seen++;
            OneSignal.User.PushSubscription.Changed += Handler;
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState { Id = "s" });
            Main.Pump();
            OpenPush.PushSubscription.Changed -= Handler;
            Platform.SubscriptionFake.RaiseChanged(null, new PushSubscriptionState { Id = "t" });
            Main.Pump();
            Assert.AreEqual(1, seen);
        }

        [Test]
        public void NotificationUtilitiesForward()
        {
            Assert.AreEqual(OpenPush.Notifications.CanRequestPermission, OneSignal.Notifications.CanRequestPermission);
            OneSignal.Notifications.ClearAllNotifications();
            OneSignal.User.PushSubscription.OptOut();
            CollectionAssert.IsSubsetOf(new[] { "ClearAll", "OptOut" }, Platform.Calls);
        }
    }
}
