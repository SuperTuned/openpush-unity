using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushFacadeMigrationTests : FacadeTestBase
    {
        [Test]
        public void FirstInitializeReplaysLegacyStateOnce()
        {
            var prefs = new InMemoryLegacyPrefs();
            prefs.Strings["openpush.deviceToken.session"] = "{\"t:level\":\"3\",\"x\":\"player-9\"}";
            OpenPushHost.LegacyPrefs = prefs;
            OpenPush.Initialize("app-1");
            CollectionAssert.AreEqual(new[] { "Initialize app-1", "AddTags level", "Login player-9 " }, Platform.Calls);
            OpenPush.Initialize("app-1");
            Assert.AreEqual(1, Platform.Calls.FindAll(c => c.StartsWith("Login")).Count);
        }

        [Test]
        public void VerificationAppsGetTheEventInstead()
        {
            var prefs = new InMemoryLegacyPrefs();
            prefs.Strings["openpush.deviceToken.session"] = "{\"x\":\"player-9\"}";
            OpenPushHost.LegacyPrefs = prefs;
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk", ReloginWithoutAuthHash = false };
            string asked = null;
            OpenPush.OnIdentityVerificationRequired += id => asked = id;
            OpenPush.Initialize("app-1");
            Main.Pump();
            Assert.AreEqual("player-9", asked);
        }

        [Test]
        public void MigratedExternalIdRaisesUserChanged()
        {
            var prefs = new InMemoryLegacyPrefs();
            prefs.Strings["openpush.deviceToken.session"] = "{\"x\":\"player-9\"}";
            OpenPushHost.LegacyPrefs = prefs;
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk", ReloginWithoutAuthHash = true };
            var seen = new List<string>();
            OpenPush.User.Changed += s => seen.Add(s.ExternalId);
            OpenPush.Initialize("app-1");
            Platform.CoreFake.PendingIdentity(OpenPushIdentityResult.Ok());
            Main.Pump();
            CollectionAssert.Contains(seen, "player-9");
        }

        private const string SharedToken = "openpush.deviceToken";
        private const string SharedSession = "openpush.deviceToken.session";
        private const string LegacySession = "{\"t:level\":\"3\",\"a:steam\":\"765\",\"x\":\"player-9\"}";

        /// On iOS PlayerPrefs is the native SDK's own store and both use the same key names, so the
        /// native initialize rewrites them. This stands in for that write.
        private InMemoryLegacyPrefs IosWithNativeOverwritingSharedKeys(List<string> sessionSeenByNative = null)
        {
            var prefs = new InMemoryLegacyPrefs();
            prefs.Strings[SharedToken] = "apns-legacy";
            prefs.Strings[SharedSession] = LegacySession;
            prefs.Strings["openpush.iam.app-1"] = "{}";
            OpenPushHost.LegacyPrefs = prefs;
            Platform.Name = "ios";
            Platform.CoreFake.OnInitialize = () =>
            {
                sessionSeenByNative?.Add(prefs.GetString(SharedSession));
                prefs.Strings[SharedToken] = "apns-native";
                prefs.Strings[SharedSession] = "{\"t:native\":\"1\",\"x\":\"native-id\"}";
            };
            return prefs;
        }

        [Test]
        public void IosSharedKeysAreSnapshotBeforeNativeInitialize()
        {
            IosWithNativeOverwritingSharedKeys();
            OpenPush.Initialize("app-1");
            CollectionAssert.AreEqual(new[] { "Initialize app-1", "AddTags level", "AddAliases steam", "Login player-9 " }, Platform.Calls);
        }

        [Test]
        public void IosNeverDeletesNativeOwnedKeysAfterInitialize()
        {
            var prefs = IosWithNativeOverwritingSharedKeys();
            OpenPush.Initialize("app-1");
            Assert.AreEqual("apns-native", prefs.GetString(SharedToken));
            Assert.AreEqual("{\"t:native\":\"1\",\"x\":\"native-id\"}", prefs.GetString(SharedSession));
            Assert.IsFalse(prefs.HasKey("openpush.iam.app-1"));
            Assert.IsFalse(prefs.HasKey("openpush.unity.legacy.session"));
            Assert.AreEqual(1, prefs.GetInt("openpush.migrated.1.1.5", 0));
        }

        [Test]
        public void IosMovesLegacySessionOutOfTheSharedKeyBeforeInitialize()
        {
            var seen = new List<string>();
            IosWithNativeOverwritingSharedKeys(seen);
            OpenPush.Initialize("app-1");
            CollectionAssert.AreEqual(new string[] { null }, seen);
        }

        [Test]
        public void IosRetriesFromTheMovedCopyAfterAFailedLaunch()
        {
            var prefs = IosWithNativeOverwritingSharedKeys();
            Platform.UserFake.ThrowOnAddTags = true;
            OpenPush.Initialize("app-1");
            Assert.AreEqual(0, prefs.GetInt("openpush.migrated.1.1.5", 0));
            Assert.AreEqual(LegacySession, prefs.GetString("openpush.unity.legacy.session"));

            OpenPush.ResetForTests();
            OpenPushHost.LegacyPrefs = prefs;
            OpenPushHost.Dispatcher = Main;
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk_asset" };
            Platform.UserFake.ThrowOnAddTags = false;
            Platform.Calls.Clear();
            OpenPush.Initialize("app-1");
            CollectionAssert.AreEqual(new[] { "Initialize app-1", "AddTags level", "AddAliases steam", "Login player-9 " }, Platform.Calls);
            Assert.AreEqual(1, prefs.GetInt("openpush.migrated.1.1.5", 0));
            Assert.AreEqual("apns-native", prefs.GetString(SharedToken));
        }

        [Test]
        public void AndroidStillDeletesLegacyKeys()
        {
            var prefs = new InMemoryLegacyPrefs();
            prefs.Strings[SharedToken] = "fcm-token";
            prefs.Strings[SharedSession] = LegacySession;
            prefs.Strings["openpush.deviceToken.events"] = "[]";
            prefs.Strings["openpush.iam.app-1"] = "{}";
            OpenPushHost.LegacyPrefs = prefs;
            Platform.Name = "android";
            var seen = new List<string>();
            Platform.CoreFake.OnInitialize = () => seen.Add(prefs.GetString(SharedSession));
            OpenPush.Initialize("app-1");
            CollectionAssert.AreEqual(new[] { LegacySession }, seen);
            CollectionAssert.AreEqual(new[] { "Initialize app-1", "AddTags level", "AddAliases steam", "Login player-9 " }, Platform.Calls);
            foreach (var key in new[] { SharedToken, SharedSession, "openpush.deviceToken.events", "openpush.iam.app-1" })
                Assert.IsFalse(prefs.HasKey(key), key);
            Assert.AreEqual(1, prefs.GetInt("openpush.migrated.1.1.5", 0));
        }
    }
}
