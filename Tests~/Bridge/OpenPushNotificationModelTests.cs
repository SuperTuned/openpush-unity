using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// OpenPushNotification.From runs on every click and foreground event.
    public class OpenPushNotificationModelTests
    {
        // Every key the server's composer puts in the data map it delivers to a
        // device. iOS-only alert keys are folded into `aps` before the payload
        // leaves the server, so they are deliberately absent here.
        private static Dictionary<string, string> FullPayload() => new Dictionary<string, string>
        {
            ["op_message_id"] = "m-1",
            ["title"] = "Daily reward",
            ["body"] = "Your chest is ready",
            ["image_url"] = "https://cdn.example/chest.png",
            ["deep_link"] = "game://chest",
            ["op_app"] = "app-1",
            ["op_ttl"] = "3600",
            ["op_priority"] = "high",
            ["op_collapse_id"] = "chest-42",
            ["op_action_category"] = "op_actions_123",
            ["op_system_message"] = "true",
            ["op_actions"] =
                "[{\"id\":\"claim\",\"label\":\"Claim\",\"icon\":\"gift\",\"system_icon\":true}," +
                "{\"id\":\"later\",\"label\":\"Later\",\"icon\":\"\",\"system_icon\":false}]",
            ["op_android_channel"] = "rewards",
            ["op_android_group"] = "daily",
        };

        [Test]
        public void MapsEveryWireKeyTheComposerSends()
        {
            var data = FullPayload();
            var notification = OpenPushNotification.From(data);

            Assert.AreEqual("m-1", notification.MessageId);
            Assert.AreEqual("Daily reward", notification.Title);
            Assert.AreEqual("Your chest is ready", notification.Body);
            Assert.AreEqual("https://cdn.example/chest.png", notification.ImageUrl);
            Assert.AreEqual("game://chest", notification.LaunchUrl);
            Assert.AreEqual("app-1", notification.AppId);
            Assert.AreEqual(3600, notification.TtlSeconds);
            Assert.AreEqual("high", notification.Priority);
            Assert.AreEqual("chest-42", notification.CollapseId);
            Assert.AreEqual("op_actions_123", notification.ActionCategory);
            Assert.IsTrue(notification.SystemMessage);

            Assert.AreEqual(2, notification.Actions.Count);
            Assert.AreEqual("claim", notification.Actions[0].Id);
            Assert.AreEqual("Claim", notification.Actions[0].Label);
            Assert.AreEqual("gift", notification.Actions[0].Icon);
            Assert.IsTrue(notification.Actions[0].SystemIcon);
            Assert.AreEqual("later", notification.Actions[1].Id);
            Assert.AreEqual("Later", notification.Actions[1].Label);
            Assert.IsFalse(notification.Actions[1].SystemIcon);

            Assert.AreEqual(2, notification.AndroidOverrides.Count);
            Assert.AreEqual("rewards", notification.AndroidOverrides["channel"]);
            Assert.AreEqual("daily", notification.AndroidOverrides["group"]);
        }

        [Test]
        public void MissingKeysReadAsNullOrEmpty()
        {
            var notification = OpenPushNotification.From(new Dictionary<string, string>());

            Assert.IsNull(notification.MessageId);
            Assert.IsNull(notification.Title);
            Assert.IsNull(notification.Body);
            Assert.IsNull(notification.ImageUrl);
            Assert.IsNull(notification.LaunchUrl);
            Assert.IsNull(notification.AppId);
            Assert.IsNull(notification.TtlSeconds);
            Assert.IsNull(notification.Priority);
            Assert.IsNull(notification.CollapseId);
            Assert.IsNull(notification.ActionCategory);
            Assert.IsFalse(notification.SystemMessage);
            Assert.AreEqual(0, notification.Actions.Count);
            Assert.AreEqual(0, notification.AndroidOverrides.Count);
        }

        [Test]
        public void ANonNumericTtlReadsAsNoTtl()
        {
            var notification = OpenPushNotification.From(
                new Dictionary<string, string> { ["op_ttl"] = "soon" });
            Assert.IsNull(notification.TtlSeconds);
        }

        [Test]
        public void SystemMessageIsFalseOnlyForAnAbsentOrFalsyValue()
        {
            Assert.IsTrue(OpenPushNotification.From(
                new Dictionary<string, string> { ["op_system_message"] = "1" }).SystemMessage);
            Assert.IsFalse(OpenPushNotification.From(
                new Dictionary<string, string> { ["op_system_message"] = "false" }).SystemMessage);
            Assert.IsFalse(OpenPushNotification.From(
                new Dictionary<string, string> { ["op_system_message"] = "0" }).SystemMessage);
            Assert.IsFalse(OpenPushNotification.From(
                new Dictionary<string, string> { ["op_system_message"] = "" }).SystemMessage);
        }

        [Test]
        public void MalformedActionsJsonYieldsNoActions()
        {
            var notification = OpenPushNotification.From(
                new Dictionary<string, string> { ["op_actions"] = "[{\"id\":" });
            Assert.AreEqual(0, notification.Actions.Count);
        }

        [Test]
        public void ActionsThatCarryNoIdOrNoLabelAreSkipped()
        {
            var notification = OpenPushNotification.From(new Dictionary<string, string>
            {
                ["op_actions"] =
                    "[{\"label\":\"No id\"},{\"id\":\"no-label\"},{\"id\":\"\",\"label\":\"Empty\"}," +
                    "\"not an object\",{\"id\":\"ok\",\"label\":\"Ok\"}]"
            });
            Assert.AreEqual(1, notification.Actions.Count);
            Assert.AreEqual("ok", notification.Actions[0].Id);
            Assert.IsNull(notification.Actions[0].Icon);
            Assert.IsFalse(notification.Actions[0].SystemIcon);
        }

        [Test]
        public void AnActionsOwnLinkIsReadAndAbsentLinksReadAsNull()
        {
            var actions = OpenPushNotification.ParseActions(
                "[{\"id\":\"shop\",\"label\":\"Shop\",\"url\":\"https://example.com/shop\"},"
                + "{\"id\":\"later\",\"label\":\"Later\"},{\"id\":\"blank\",\"label\":\"Blank\",\"url\":\"\"}]");
            Assert.AreEqual(3, actions.Count);
            Assert.AreEqual("https://example.com/shop", actions[0].Url);
            Assert.IsNull(actions[1].Url);
            Assert.IsNull(actions[2].Url);
        }

        [Test]
        public void DataIsTheDictionaryThatWasPassedIn()
        {
            var data = FullPayload();
            Assert.AreSame(data, OpenPushNotification.From(data).Data);
        }

        [Test]
        public void FromNullIsSafe()
        {
            var notification = OpenPushNotification.From(null);
            Assert.IsNotNull(notification);
            Assert.IsNull(notification.MessageId);
            Assert.IsNull(notification.Title);
            Assert.AreEqual(0, notification.Data.Count);
            Assert.AreEqual(0, notification.Actions.Count);
            Assert.AreEqual(0, notification.AndroidOverrides.Count);
        }

        [Test]
        public void ParseActionsIsTheParserFromUses()
        {
            var actions = OpenPushNotification.ParseActions(FullPayload()["op_actions"]);
            Assert.AreEqual(2, actions.Count);
            Assert.AreEqual("claim", actions[0].Id);
            Assert.AreEqual(0, OpenPushNotification.ParseActions(null).Count);
            Assert.AreEqual(0, OpenPushNotification.ParseActions("{\"id\":\"x\"}").Count);
        }

        // The data map a bridge delivers (Fixtures/wire) reads through the same model.
        [Test]
        public void ReadsTheAndroidClickFixtureDataMap()
        {
            var wire = ClickEventWire.From(OpenPushWire.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wire", "click-android.json"))));
            var notification = OpenPushNotification.From(wire.data);
            Assert.AreEqual("m-1", notification.MessageId);
            Assert.AreEqual("Sale", notification.Title);
            Assert.AreEqual("50% off", notification.Body);
            Assert.AreEqual("game://shop", notification.LaunchUrl);
            Assert.AreEqual("spring", notification.Data["promo"]);
            Assert.AreSame(wire.data, notification.Data);
        }
    }
}
