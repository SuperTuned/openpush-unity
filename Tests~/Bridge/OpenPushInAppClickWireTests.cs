using System;
using System.IO;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// The public OpenPushInAppClick: Kind says what the action does, and
    /// ActionId is the custom action's own name, null for every other kind.
    public class OpenPushInAppClickWireTests
    {
        private static OpenPushInAppClick Click(string fixture)
            => InAppClickWire.From(OpenPushWire.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "wire", fixture)))).ToPublic();

        [Test]
        public void AUrlClickIsKindUrlWithNoActionId()
        {
            var click = Click("iam-click.json");
            Assert.AreEqual("url", click.Kind);
            Assert.IsNull(click.ActionId);
            Assert.AreEqual("https://example.com/p", click.Url);
            Assert.AreEqual("webview", click.UrlTarget);
            Assert.IsTrue(click.Closes);
        }

        [Test]
        public void APushPromptIsKindPromptPush()
        {
            var click = Click("iam-click-prompt-push.json");
            Assert.AreEqual("prompt_push", click.Kind);
            Assert.AreEqual("push", click.Prompt);
            Assert.IsNull(click.ActionId);
        }

        [Test]
        public void ALocationPromptIsKindPromptLocation()
        {
            var click = Click("iam-click-prompt-location.json");
            Assert.AreEqual("prompt_location", click.Kind);
            Assert.AreEqual("location", click.Prompt);
            Assert.IsFalse(click.Closes);
        }

        [Test]
        public void ATagClickIsKindTagAndCarriesThePairs()
        {
            var click = Click("iam-click-tag.json");
            Assert.AreEqual("tag", click.Kind);
            Assert.AreEqual("gold", click.Tags["vip"]);
            Assert.IsNull(click.ActionId);
        }

        [Test]
        public void AnOutcomeClickIsKindOutcome()
        {
            var click = Click("iam-click-outcome.json");
            Assert.AreEqual("outcome", click.Kind);
            Assert.AreEqual("purchase_intent", click.Outcome);
        }

        [Test]
        public void ACloseIsKindClose()
        {
            var click = Click("iam-click-close.json");
            Assert.AreEqual("close", click.Kind);
            Assert.IsTrue(click.Closes);
            Assert.IsNull(click.ActionId);
        }

        [Test]
        public void ANamedActionIsKindCustomWithItsName()
        {
            var click = Click("iam-click-custom.json");
            Assert.AreEqual("custom", click.Kind);
            Assert.AreEqual("open_shop", click.ActionId);
        }

        [Test]
        public void AnActionThatOnlyRecordsIsKindClick()
            => Assert.AreEqual("click", InAppClickWire.From(OpenPushWire.Parse("{\"message_id\":\"iam_1\"}")).ToPublic().Kind);

        // A tag action whose value the composer left unset has no pair, but the bridge still names it.
        [Test]
        public void TheKindTheBridgeReportsWins()
        {
            var click = Click("iam-click-tag-kind.json");
            Assert.AreEqual("tag", click.Kind);
            Assert.IsNull(click.ActionId);
        }

        [Test]
        public void KindRoundTrips()
        {
            var wire = InAppClickWire.From(OpenPushWire.Parse(File.ReadAllText(
                Path.Combine(AppContext.BaseDirectory, "Fixtures", "wire", "iam-click-tag-kind.json"))));
            Assert.AreEqual("tag", InAppClickWire.From(OpenPushWire.Parse(wire.ToJson())).kind);
        }
    }
}
