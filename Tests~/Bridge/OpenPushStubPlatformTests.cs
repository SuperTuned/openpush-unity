using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushStubPlatformTests
    {
        private OpenPushStubPlatform _stub;
        private List<string> _events;

        [SetUp]
        public void SetUp()
        {
            OpenPushHost.Reset();
            _stub = new OpenPushStubPlatform();
            _events = new List<string>();
            _stub.Core.Registered += () => _events.Add("registered");
            _stub.PushSubscription.Changed += (p, c) => _events.Add($"sub {p?.OptedIn}->{c.OptedIn} {c.Token}");
            _stub.Notifications.PermissionChanged += g => _events.Add("permission " + g);
            _stub.Core.Initialize(new OpenPushInitOptions { AppId = "a", SdkKey = "k" });
        }

        [Test]
        public void RegisterStoresTheTokenAndRaisesChangeThenRegistered()
        {
            _stub.Core.Register("tok", null);
            Assert.AreEqual("tok", _stub.Core.CurrentToken);
            CollectionAssert.AreEqual(new[] { "sub False->False tok", "registered" }, _events);
        }

        [Test]
        public void PermissionThenTokenOptsIn()
        {
            var granted = false;
            _stub.Notifications.RequestPermission(false, g => granted = g);
            _stub.Core.Register("tok", null);
            Assert.IsTrue(granted);
            Assert.IsTrue(_stub.PushSubscription.OptedIn);
            Assert.AreEqual(OpenPushNotificationTypes.Subscribed, _stub.PushSubscription.Status);
            _stub.PushSubscription.OptOut();
            Assert.IsFalse(_stub.PushSubscription.OptedIn);
            Assert.AreEqual(OpenPushNotificationTypes.OptedOut, _stub.PushSubscription.Status);
        }

        [Test]
        public void TagsAliasesAndIdentityLiveInMemory()
        {
            _stub.User.AddTags(new Dictionary<string, string> { ["level"] = "3" });
            _stub.User.AddAliases(new Dictionary<string, string> { ["steam"] = "765" });
            _stub.User.RemoveAliases(new[] { "steam" });
            OpenPushIdentityResult result = null;
            _stub.Core.Login("player-9", null, r => result = r);
            Assert.AreEqual("3", _stub.User.GetTags()["level"]);
            Assert.AreEqual("player-9", _stub.User.ExternalId);
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void FetchShowsTheDemoFixtureWhenTheHostProvidesOne()
        {
            var shown = 0;
            OpenPushHost.ShowDemoInAppFixture = () => shown++;
            _stub.InAppMessages.Fetch();
            Assert.AreEqual(1, shown);
        }

        [Test]
        public void LiveFeaturesReportUnsupported()
        {
            bool? ok = null;
            _stub.LiveActivities.Enter("a", "t", null, r => ok = r);
            Assert.AreEqual(false, ok);
            Assert.AreEqual(OpenPushLiveUpdateResult.Unsupported, _stub.LiveUpdates.HandleAndroidPayload("{}", null));
            Assert.AreEqual(OpenPushLiveUpdateCapability.UnsupportedPlatform, _stub.LiveUpdates.Capability);
        }

        [Test]
        public void ProcessClickRaisesAClickFromTheData()
        {
            OpenPushClickEvent click = null;
            _stub.Notifications.Clicked += c => click = c;
            _stub.Notifications.ProcessClick("{\"op_message_id\":\"m1\",\"deep_link\":\"game://x\"}");
            Assert.AreEqual("m1", click.MessageId);
            Assert.AreEqual("game://x", click.Url);
        }
    }
}
