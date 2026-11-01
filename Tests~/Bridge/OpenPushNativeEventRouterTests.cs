using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushNativeEventRouterTests
    {
        private List<string> _seen;
        private List<string> _completions;
        private OpenPushNativeEventRouter _router;

        [SetUp]
        public void SetUp()
        {
            _seen = new List<string>(); _completions = new List<string>();
            _router = new OpenPushNativeEventRouter((id, d) => _completions.Add(id + ":" + d), id => _completions.Add("show " + id));
            _router.Log += (l, m) => _seen.Add(l + " " + m);
            _router.Registered += () => _seen.Add("registered");
            _router.RegistrationFailed += m => _seen.Add("failed " + m);
            _router.IdentityVerificationRequired += id => _seen.Add("ivr " + id);
            _router.SubscriptionChanged += (p, c) => _seen.Add($"sub {p?.Id}->{c.Id}");
            _router.PermissionChanged += g => _seen.Add("perm " + g);
            _router.Clicked += c => _seen.Add("click " + c.MessageId + " " + c.Url);
            _router.ForegroundWillDisplay += f => { _seen.Add("fg " + f.MessageId); f.Finish(); };
            _router.InAppLifecycle += (id, phase) => _seen.Add("iam " + id + " " + phase);
            _router.InAppClicked += (id, click) => _seen.Add("iamclick " + id + " " + click.ActionId);
            _router.Result += (id, payload) => _seen.Add("result " + id);
        }

        [Test]
        public void RoutesEveryKind()
        {
            _router.Route("log", "{\"message\":\"✗ nope\"}");
            _router.Route("registered", "{}");
            _router.Route("registration_failed", "{\"message\":\"-1\"}");
            _router.Route("identity_verification_required", "{\"external_id\":\"p9\"}");
            _router.Route("push_subscription", "{\"previous\":null,\"current\":{\"id\":\"s1\",\"opted_in\":true}}");
            _router.Route("permission", "{\"granted\":true}");
            _router.Route("click", "{\"message_id\":\"m1\",\"url\":\"game://x\",\"data\":{\"op_message_id\":\"m1\"}}");
            _router.Route("foreground", "{\"message_id\":\"m2\",\"completion_id\":\"fg-1\",\"data\":{}}");
            _router.Route("iam_lifecycle", "{\"message_id\":\"i1\",\"phase\":\"didDisplay\"}");
            _router.Route("iam_click", "{\"message_id\":\"i1\",\"action_id\":\"a\"}");
            _router.Route("result", "{\"correlation_id\":7,\"payload\":{\"granted\":true}}");
            CollectionAssert.AreEqual(new[]
            {
                "Error ✗ nope", "registered", "failed -1", "ivr p9", "sub ->s1", "perm True",
                "click m1 game://x", "fg m2", "iam i1 didDisplay", "iamclick i1 a", "result 7",
            }, _seen);
            CollectionAssert.AreEqual(new[] { "fg-1:True" }, _completions);
            Assert.AreEqual("s1", _router.LastSubscription.Id);
        }

        [Test]
        public void UnknownKindAndGarbageNeverThrow()
        {
            Assert.DoesNotThrow(() => _router.Route("future_kind", "{}"));
            Assert.DoesNotThrow(() => _router.Route("click", "{broken"));
            Assert.DoesNotThrow(() => _router.Route(null, null));
            StringAssert.Contains("future_kind", string.Join("|", _seen));
        }

        [Test]
        public void AThrowingSubscriberDoesNotEscapeToNativeCode()
        {
            _router.Registered += () => throw new System.Exception("host bug");
            Assert.DoesNotThrow(() => _router.Route("registered", "{}"));
        }

        [TestCase("STALE", OpenPushLiveUpdateResult.Stale)]
        [TestCase("NOT_LIVE_UPDATE", OpenPushLiveUpdateResult.NotLiveUpdate)]
        [TestCase("DISPLAYED", OpenPushLiveUpdateResult.Displayed)]
        [TestCase(null, OpenPushLiveUpdateResult.Unsupported)]
        [TestCase("SOMETHING_NEW", OpenPushLiveUpdateResult.Unsupported)]
        public void MapsLiveUpdateResults(string name, OpenPushLiveUpdateResult expected)
            => Assert.AreEqual(expected, OpenPushLiveUpdateNames.Result(name));

        [Test]
        public void MapsCapabilities()
        {
            Assert.AreEqual(OpenPushLiveUpdateCapability.Eligible, OpenPushLiveUpdateNames.Capability("ELIGIBLE"));
            Assert.AreEqual(OpenPushLiveUpdateCapability.UnsupportedPlatform, OpenPushLiveUpdateNames.Capability(""));
        }
    }
}
