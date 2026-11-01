using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    // RF4: legacy state that is corrupt, partial, legacy-format or from an identity-verification app.
    public class OpenPushLegacyStateMigratorTests
    {
        private InMemoryLegacyPrefs _prefs;
        private FakePlatform _platform;
        private List<string> _verification;
        private List<string> _log;

        [SetUp]
        public void SetUp()
        {
            _prefs = new InMemoryLegacyPrefs(); _platform = new FakePlatform();
            _verification = new List<string>(); _log = new List<string>();
        }

        private OpenPushMigrationResult Run(bool hostOwned = false, bool relogin = true)
            => OpenPushLegacyStateMigrator.Run(new OpenPushLegacyMigrationContext
            {
                Prefs = _prefs, Platform = _platform, AppId = "app-1", HostOwnedAndroid = hostOwned,
                ReloginWithoutAuthHash = relogin, IdentityVerificationRequired = _verification.Add,
                Log = (l, m) => _log.Add(l + " " + m),
            });

        private void Session(string json) => _prefs.Strings["openpush.deviceToken.session"] = json;

        [Test]
        public void FreshInstallMarksDoneAndTouchesNothing()
        {
            var result = Run();
            Assert.IsFalse(result.SessionRead);
            Assert.IsEmpty(_platform.Calls);
            Assert.AreEqual(1, _prefs.GetInt("openpush.migrated.1.1.5", 0));
        }

        [Test]
        public void FullSessionIsReplayedOntoTheNativeSdkThenDeleted()
        {
            Session("{\"a:steam\":\"765\",\"n\":\"1\",\"o\":\"1\",\"t:level\":\"3\",\"t:vip\":\"yes\",\"x\":\"player-9\"}");
            _prefs.Strings["openpush.deviceToken"] = "fcm-token";
            _prefs.Strings["openpush.deviceToken.events"] = "[]";
            _prefs.Strings["openpush.iam.app-1"] = "{}";
            var result = Run();
            CollectionAssert.AreEqual(new[] { "OptOut", "AddTags level,vip", "AddAliases steam", "Login player-9 " }, _platform.Calls);
            Assert.IsTrue(result.OptedOut); Assert.AreEqual(2, result.Tags); Assert.AreEqual(1, result.Aliases); Assert.IsTrue(result.Relogged);
            foreach (var key in new[] { "openpush.deviceToken", "openpush.deviceToken.session", "openpush.deviceToken.events", "openpush.iam.app-1" })
                Assert.IsFalse(_prefs.HasKey(key), key);
            Assert.AreEqual(1, _prefs.GetInt("openpush.migrated.1.1.5", 0));
        }

        [Test]
        public void TokenIsRegisteredOnlyForAndroidHostOwned()
        {
            _prefs.Strings["openpush.deviceToken"] = "fcm-token";
            Run(hostOwned: true);
            CollectionAssert.Contains(_platform.Calls, "Register fcm-token ");
            SetUp();
            _prefs.Strings["openpush.deviceToken"] = "fcm-token";
            Run(hostOwned: false);
            CollectionAssert.DoesNotContain(_platform.Calls, "Register fcm-token ");
        }

        [Test]
        public void IdentityVerificationAppsAreAskedInsteadOfReloggedBlind()
        {
            Session("{\"x\":\"player-9\"}");
            var result = Run(relogin: false);
            CollectionAssert.AreEqual(new[] { "player-9" }, _verification);
            CollectionAssert.DoesNotContain(_platform.Calls, "Login player-9 ");
            Assert.IsTrue(result.VerificationRequired);
        }

        [Test]
        public void DetachedIdentityIsNotRestored()
        {
            Session("{\"d\":\"1\",\"x\":\"player-9\"}");
            Run();
            Assert.IsFalse(_platform.Calls.Exists(c => c.StartsWith("Login")));
            Assert.IsEmpty(_verification);
        }

        [Test]
        public void LegacyStatusMinusTwoMeansOptedOut()
        {
            Session("{\"s\":\"-2\"}");
            Assert.IsTrue(Run().OptedOut);
            CollectionAssert.Contains(_platform.Calls, "OptOut");
        }

        [Test]
        public void EmptyTagValuesAreNotReplayed()
        {
            Session("{\"t:gone\":\"\",\"t:kept\":\"1\"}");
            Run();
            CollectionAssert.Contains(_platform.Calls, "AddTags kept");
        }

        [Test]
        public void CorruptSessionIsLoggedSkippedAndCompleted()
        {
            Session("{not json");
            Assert.DoesNotThrow(() => Run());
            Assert.IsEmpty(_platform.Calls);
            Assert.IsTrue(_log.Exists(l => l.Contains("could not read")));
            Assert.AreEqual(1, _prefs.GetInt("openpush.migrated.1.1.5", 0));
            Assert.IsFalse(_prefs.HasKey("openpush.deviceToken.session"));
        }

        [Test]
        public void RunsOnlyOnce()
        {
            Session("{\"t:a\":\"1\"}");
            Run();
            Session("{\"t:b\":\"1\"}");
            Assert.IsFalse(Run().Ran);
            CollectionAssert.AreEqual(new[] { "AddTags a" }, _platform.Calls);
        }

        [Test]
        public void APlatformFailureIsLoggedAndRetriedNextLaunch()
        {
            Session("{\"t:a\":\"1\"}");
            var throwing = new ThrowingPlatform();
            var result = OpenPushLegacyStateMigrator.Run(new OpenPushLegacyMigrationContext
            { Prefs = _prefs, Platform = throwing, AppId = "app-1", Log = (l, m) => _log.Add(m) });
            Assert.IsFalse(result.Ran);
            Assert.AreEqual(0, _prefs.GetInt("openpush.migrated.1.1.5", 0));
            Assert.IsTrue(_prefs.HasKey("openpush.deviceToken.session"));
        }

        private sealed class ThrowingPlatform : IOpenPushPlatform
        {
            private readonly FakePlatform _inner = new FakePlatform();
            public string Name => "throwing";
            public IOpenPushCore Core => _inner.Core;
            public IOpenPushUser User => new ThrowingUser();
            public IOpenPushPushSubscription PushSubscription => _inner.PushSubscription;
            public IOpenPushNotifications Notifications => _inner.Notifications;
            public IOpenPushInAppMessages InAppMessages => _inner.InAppMessages;
            public IOpenPushLiveActivities LiveActivities => _inner.LiveActivities;
            public IOpenPushLiveUpdates LiveUpdates => _inner.LiveUpdates;
            public IOpenPushVerify Verify => _inner.Verify;
        }

        private sealed class ThrowingUser : IOpenPushUser
        {
            public void AddTags(IReadOnlyDictionary<string, string> tags) => throw new System.InvalidOperationException("native down");
            public void RemoveTags(IReadOnlyList<string> keys) { }
            public Dictionary<string, string> GetTags() => new Dictionary<string, string>();
            public void AddAliases(IReadOnlyDictionary<string, string> aliases) { }
            public void RemoveAliases(IReadOnlyList<string> labels) { }
            public string ExternalId => null;
            public void AddSms(string number) { }
            public void RemoveSms(string number) { }
            public void AddWhatsApp(string number) { }
            public void RemoveWhatsApp(string number) { }
            public void AddEmail(string address) { }
            public void RemoveEmail(string address) { }
        }
    }
}
