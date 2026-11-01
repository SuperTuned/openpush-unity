using System;
using System.IO;
using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushWireTests
    {
        private static string Fixture(string name)
            => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wire", name));

        [Test]
        public void AndroidClickRoundTrips()
        {
            var wire = ClickEventWire.From(OpenPushWire.Parse(Fixture("click-android.json")));
            var again = ClickEventWire.From(OpenPushWire.Parse(wire.ToJson()));
            Assert.AreEqual("m-1", again.message_id);
            Assert.AreEqual("open", again.action_id);
            Assert.AreEqual("game://shop", again.url);
            Assert.AreEqual("Sale", again.notification.title);
            Assert.AreEqual("open", again.notification.actions[0].id);
            Assert.AreEqual("spring", again.data["promo"]);
        }

        // RF1: iOS userInfo carries numbers, booleans, nested objects and nulls; unknown keys appear.
        [Test]
        public void ClickEventToleratesNullsUnknownKeysAndNonStringData()
        {
            var wire = ClickEventWire.From(OpenPushWire.Parse(Fixture("click-ios.json")));
            Assert.AreEqual("m-2", wire.message_id);
            Assert.IsNull(wire.action_id);
            Assert.AreEqual("10", wire.data["level"]);
            Assert.AreEqual("true", wire.data["vip"]);
            StringAssert.StartsWith("{", wire.data["aps"]);
            StringAssert.Contains("\"title\":\"Level up\"", wire.data["aps"]);
            Assert.IsFalse(wire.data.ContainsKey("unknown_extra"));
        }

        [Test]
        public void ClickCarriesWhetherTheSdkAlreadyOpenedTheUrl()
        {
            var opened = ClickEventWire.From(OpenPushWire.Parse(
                "{\"message_id\":\"m-3\",\"url\":\"https://example.com\",\"launch_url_opened\":true}"));
            Assert.IsTrue(opened.launch_url_opened);
            Assert.IsTrue(ClickEventWire.From(OpenPushWire.Parse(opened.ToJson())).launch_url_opened);
            Assert.IsTrue(opened.ToPublic().LaunchUrlOpened);

            var older = ClickEventWire.From(OpenPushWire.Parse("{\"message_id\":\"m-4\",\"url\":\"https://example.com\"}"));
            Assert.IsFalse(older.ToPublic().LaunchUrlOpened, "a native build without the key never opened it");
        }

        [Test]
        public void ClickWithoutNotificationObjectStillParses()
        {
            var wire = ClickEventWire.From(OpenPushWire.Parse("{\"data\":{\"op_message_id\":\"m-9\"}}"));
            Assert.IsNull(wire.message_id);
            Assert.IsNull(wire.notification);
            Assert.AreEqual("m-9", wire.data["op_message_id"]);
        }

        [Test]
        public void GarbageJsonParsesToNullNotAnException()
        {
            Assert.IsNull(OpenPushWire.Parse("{not json"));
            Assert.IsNull(OpenPushWire.Parse(null));
            Assert.IsNull(ClickEventWire.From(null));
        }

        [Test]
        public void ForegroundCarriesTheCompletionId()
        {
            var wire = ForegroundEventWire.From(OpenPushWire.Parse(Fixture("foreground-ios.json")));
            Assert.AreEqual("fg-7", wire.completion_id);
            Assert.AreEqual("fg-7", ForegroundEventWire.From(OpenPushWire.Parse(wire.ToJson())).completion_id);
        }

        [Test]
        public void SubscriptionChangeRoundTripsWithNullPreviousAndStatus()
        {
            var wire = PushSubscriptionChangeWire.From(OpenPushWire.Parse(Fixture("push-subscription.json")));
            Assert.IsNull(wire.previous);
            Assert.AreEqual("sub_1", wire.current.id);
            Assert.AreEqual(1, wire.current.status);
            var again = PushSubscriptionChangeWire.From(OpenPushWire.Parse(wire.ToJson()));
            Assert.IsNull(again.previous);
            Assert.IsTrue(again.current.opted_in);
            Assert.AreEqual(1, again.current.status);
        }

        [Test]
        public void MissingStatusStaysNull()
            => Assert.IsNull(PushSubscriptionWire.From(OpenPushWire.Parse("{\"id\":null,\"token\":null,\"opted_in\":false}")).status);

        [Test]
        public void IdentityRejectionMapsToARejectedResult()
        {
            var result = IdentityResultWire.From(OpenPushWire.Parse(Fixture("identity-rejected.json"))).ToPublic();
            Assert.IsFalse(result.Success);
            Assert.AreEqual("hash required", result.IdentityRejectedNote);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void StringMapToJsonEscapesAndRoundTrips()
        {
            var json = OpenPushWire.StringMapToJson(new System.Collections.Generic.Dictionary<string, string> { ["q\"uote"] = "a\\b" });
            Assert.AreEqual("a\\b", OpenPushWire.StringMap(OpenPushWire.Parse("{\"m\":" + json + "}"), "m")["q\"uote"]);
        }

        // Spec §4: every OpenPushNotification field rides the notification object, so a payload whose
        // flat data map lacks the op_* keys (iOS userInfo) still yields the full model.
        [Test]
        public void NotificationWireCarriesEveryModelField()
        {
            var wire = ClickEventWire.From(OpenPushWire.Parse(Fixture("click-full-notification.json")));
            var n = wire.notification;
            Assert.AreEqual("high", n.priority);
            Assert.AreEqual("op_actions_123", n.action_category);
            Assert.AreEqual("app-1", n.app_id);
            Assert.AreEqual(3600, n.ttl_seconds);
            Assert.IsTrue(n.system_message);
            Assert.AreEqual("rewards", n.android["channel"]);
            var again = NotificationWire.From(OpenPushWire.Parse(n.ToJson()));
            Assert.AreEqual("high", again.priority);
            Assert.AreEqual("op_actions_123", again.action_category);
            Assert.AreEqual("app-1", again.app_id);
            Assert.AreEqual(3600, again.ttl_seconds);
            Assert.IsTrue(again.system_message);
            Assert.AreEqual("daily", again.android["group"]);
        }

        [Test]
        public void ClickFillsTheModelFromTheNotificationObject()
        {
            var n = ClickEventWire.From(OpenPushWire.Parse(Fixture("click-full-notification.json"))).ToPublic().Notification;
            Assert.AreEqual("m-4", n.MessageId);
            Assert.AreEqual("Chest", n.Title);
            Assert.AreEqual("chest-42", n.CollapseId);
            Assert.AreEqual("high", n.Priority);
            Assert.AreEqual("op_actions_123", n.ActionCategory);
            Assert.AreEqual("app-1", n.AppId);
            Assert.AreEqual(3600, n.TtlSeconds);
            Assert.IsTrue(n.SystemMessage);
            Assert.AreEqual(1, n.Actions.Count);
            Assert.AreEqual("claim", n.Actions[0].Id);
            Assert.IsTrue(n.Actions[0].SystemIcon);
            Assert.AreEqual("rewards", n.AndroidOverrides["channel"]);
            Assert.AreEqual("daily", n.AndroidOverrides["group"]);
        }

        [Test]
        public void ForegroundFillsTheModelFromTheNotificationObject()
        {
            var json = Fixture("click-full-notification.json");
            var n = ForegroundEventWire.From(OpenPushWire.Parse(json)).ToPublic(null, null).Notification;
            Assert.AreEqual("high", n.Priority);
            Assert.AreEqual(3600, n.TtlSeconds);
            Assert.AreEqual("rewards", n.AndroidOverrides["channel"]);
        }

        [Test]
        public void TheDataMapStillWinsOverTheNotificationObject()
        {
            var json = "{\"notification\":{\"priority\":\"normal\",\"ttl_seconds\":5,\"android\":{\"channel\":\"obj\"}},"
                     + "\"data\":{\"op_priority\":\"high\",\"op_ttl\":\"60\",\"op_android_channel\":\"data\"}}";
            var n = ClickEventWire.From(OpenPushWire.Parse(json)).ToPublic().Notification;
            Assert.AreEqual("high", n.Priority);
            Assert.AreEqual(60, n.TtlSeconds);
            Assert.AreEqual("data", n.AndroidOverrides["channel"]);
        }

        // A link per action button. Both bridges report the tapped button's link as
        // action_url; Url stays the message's deep_link.
        [Test]
        public void ClickCarriesTheTappedButtonsLinkSeparatelyFromTheMessageUrl()
        {
            var json = "{\"message_id\":\"m-5\",\"action_id\":\"shop\",\"url\":\"game://home\","
                     + "\"action_url\":\"https://example.com/shop\",\"launch_url_opened\":true}";
            var wire = ClickEventWire.From(OpenPushWire.Parse(json));
            Assert.AreEqual("https://example.com/shop", ClickEventWire.From(OpenPushWire.Parse(wire.ToJson())).action_url);
            var click = wire.ToPublic();
            Assert.AreEqual("shop", click.ActionId);
            Assert.AreEqual("game://home", click.Url);
            Assert.AreEqual("https://example.com/shop", click.ActionUrl);
            Assert.IsTrue(click.LaunchUrlOpened);
        }

        [Test]
        public void WithoutTheBridgeKeyTheButtonLinkComesFromTheParsedActions()
        {
            // A host-forwarded tap, or a native build that predates action_url.
            var json = "{\"action_id\":\"shop\",\"data\":{\"op_message_id\":\"m-6\",\"deep_link\":\"game://home\","
                     + "\"op_actions\":\"[{\\\"id\\\":\\\"shop\\\",\\\"label\\\":\\\"Shop\\\",\\\"url\\\":\\\"game://shop\\\"},"
                     + "{\\\"id\\\":\\\"later\\\",\\\"label\\\":\\\"Later\\\"}]\"}}";
            var click = ClickEventWire.From(OpenPushWire.Parse(json)).ToPublic();
            Assert.AreEqual("game://shop", click.ActionUrl);
            Assert.AreEqual("game://home", click.Url);

            var later = ClickEventWire.From(OpenPushWire.Parse(json.Replace("\"action_id\":\"shop\"", "\"action_id\":\"later\""))).ToPublic();
            Assert.IsNull(later.ActionUrl, "a button without a link has no ActionUrl");
            var body = ClickEventWire.From(OpenPushWire.Parse(json.Replace("\"action_id\":\"shop\",", ""))).ToPublic();
            Assert.IsNull(body.ActionUrl, "a tap on the notification itself has no ActionUrl");
        }

        [Test]
        public void ActionUrlsRideTheNotificationObjectWhenTheDataMapHasNoActions()
        {
            // iOS: the bridge's notification object carries the parsed actions with their links.
            var json = "{\"message_id\":\"m-7\",\"action_id\":\"go\",\"notification\":{\"actions\":["
                     + "{\"id\":\"go\",\"label\":\"Go\",\"icon\":null,\"system_icon\":false,\"url\":\"https://example.com/go\"}]},"
                     + "\"data\":{\"op_message_id\":\"m-7\"}}";
            var wire = ClickEventWire.From(OpenPushWire.Parse(json));
            Assert.AreEqual("https://example.com/go", wire.notification.actions[0].url);
            var click = wire.ToPublic();
            Assert.AreEqual("https://example.com/go", click.Notification.Actions[0].Url);
            Assert.AreEqual("https://example.com/go", click.ActionUrl);
        }

        [Test]
        public void MissingModelFieldsStayNull()
        {
            var n = NotificationWire.From(OpenPushWire.Parse("{\"message_id\":\"m\"}"));
            Assert.IsNull(n.priority);
            Assert.IsNull(n.ttl_seconds);
            Assert.IsFalse(n.system_message);
            Assert.AreEqual(0, n.android.Count);
        }
    }
}
