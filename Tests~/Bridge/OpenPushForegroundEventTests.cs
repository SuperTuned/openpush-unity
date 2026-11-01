using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    // RF3: native waits on exactly one completion per foreground event.
    public class OpenPushForegroundEventTests
    {
        private List<string> _completions;
        private List<string> _displays;

        private OpenPushForegroundEvent Event(string id = "fg-1")
        {
            _completions = new List<string>(); _displays = new List<string>();
            return new OpenPushForegroundEvent
            {
                MessageId = "m", Data = new Dictionary<string, string>(), CompletionId = id,
                Completer = (cid, display) => _completions.Add(cid + ":" + display),
                Displayer = cid => _displays.Add(cid),
            };
        }

        [Test]
        public void FinishWithoutPreventDefaultDisplays()
        {
            var e = Event(); e.Finish();
            CollectionAssert.AreEqual(new[] { "fg-1:True" }, _completions);
        }

        [Test]
        public void PreventDefaultSuppressesAndFinishRunsOnce()
        {
            var e = Event(); e.PreventDefault(); e.Finish(); e.Finish();
            CollectionAssert.AreEqual(new[] { "fg-1:False" }, _completions);
        }

        [Test]
        public void DisplayBeforeFinishMeansDisplay()
        {
            var e = Event(); e.PreventDefault(); e.Display(); e.Finish();
            CollectionAssert.AreEqual(new[] { "fg-1:True" }, _completions);
            Assert.IsEmpty(_displays);
        }

        [Test]
        public void DisplayAfterAPreventedFinishPresentsOnceThroughTheDisplayer()
        {
            var e = Event(); e.PreventDefault(); e.Finish(); e.Display(); e.Display();
            CollectionAssert.AreEqual(new[] { "fg-1" }, _displays);
        }

        [Test]
        public void PreventDefaultAfterFinishIsIgnored()
        {
            var e = Event(); e.Finish(); e.PreventDefault();
            Assert.IsFalse(e.DefaultPrevented);
            CollectionAssert.AreEqual(new[] { "fg-1:True" }, _completions);
        }

        [Test]
        public void LocalEventWithoutCompletionIdNeverCallsNative()
        {
            var e = Event(null); e.PreventDefault(); e.Finish(); e.Display();
            Assert.IsEmpty(_completions); Assert.IsEmpty(_displays);
            Assert.IsTrue(e.DefaultPrevented);
        }

        [Test]
        public void WireForegroundMapsToAPublicEventWiredToTheBridge()
        {
            var wire = ForegroundEventWire.From(OpenPushWire.Parse(
                "{\"message_id\":\"m-3\",\"completion_id\":\"fg-7\",\"notification\":{\"title\":\"Hi\"},\"data\":{\"op_message_id\":\"m-3\"}}"));
            var seen = new List<string>();
            var e = wire.ToPublic((id, display) => seen.Add(id + ":" + display), id => seen.Add("show " + id));
            Assert.AreEqual("m-3", e.MessageId);
            Assert.AreEqual("Hi", e.Notification.Title);
            e.Finish();
            CollectionAssert.AreEqual(new[] { "fg-7:True" }, seen);
        }

        [Test]
        public void StaleLiveUpdateCountsAsHandled()
            => Assert.IsTrue(OpenPushLiveUpdateResult.Stale.IsHandled());

        [Test]
        public void InAppClickWireMapsToThePublicClick()
        {
            var click = InAppClickWire.From(OpenPushWire.Parse(
                "{\"message_id\":\"iam_1\",\"action_id\":\"claim\",\"url\":\"https://e.com\",\"url_target\":\"webview\",\"close\":true}")).ToPublic();
            Assert.IsNull(click.ActionId, "a url click carries no custom action name");
            Assert.AreEqual("https://e.com", click.Url);
            Assert.AreEqual("webview", click.UrlTarget);
            Assert.IsTrue(click.Closes);
            Assert.AreEqual("url", click.Kind);
        }
    }
}
