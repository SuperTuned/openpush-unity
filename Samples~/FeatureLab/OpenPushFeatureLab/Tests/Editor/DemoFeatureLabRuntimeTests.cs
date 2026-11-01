using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace OpenPushFeatureLab.Tests
{
    public sealed class DemoFeatureLabRuntimeTests
    {
        [TestCase("app-1", "sdk-1", true)]
        [TestCase("", "sdk-1", false)]
        [TestCase("app-1", "", false)]
        public void ConfiguredProjectsDefaultToTheLiveSdk(
            string appId, string sdkKey, bool expectLive)
            => Assert.That(FeatureLabRuntimeDefaults.Mode(appId, sdkKey),
                Is.EqualTo(expectLive ? LabRuntimeMode.Live : LabRuntimeMode.Demo));

        [Test]
        public void PnsRowsExposeSeparateTitleAndWrappingDescription()
        {
            var tree = Resources.Load<VisualTreeAsset>("OpenPushFeatureLab");
            Assert.That(tree, Is.Not.Null);

            var root = new VisualElement();
            tree.CloneTree(root);
            using (var controller = new FeatureLabController())
            using (var view = new FeatureLabView(root, controller, null))
            {
                foreach (var name in new[] { "pns-simple", "pns-image", "pns-button", "pns-custom", "pns-sound" })
                {
                    var row = root.Q<Button>(name);
                    Assert.That(row, Is.Not.Null, name);
                    Assert.That(row.text, Is.Empty, name + " must not depend on an escaped newline");
                    Assert.That(row.Q<VisualElement>(className: "fixture-copy"), Is.Not.Null, name);
                    Assert.That(row.Q<Label>(className: "fixture-title"), Is.Not.Null, name);
                    Assert.That(row.Q<Label>(className: "fixture-detail"), Is.Not.Null, name);
                }

                Assert.That(root.Q<VisualElement>(className: "top-bar"), Is.Null);
                Assert.That(root.Q<Label>("navigation-title").text, Is.EqualTo("Status"));
                Assert.That(root.Q<Button>("settings-button").text, Is.Empty);
                Assert.That(root.Q<VisualElement>(className: "settings-gear"), Is.Not.Null);
                Assert.That(root.Q<Label>("authorization-value").parent[0].ClassListContains("row-label"), Is.True);
            }
        }

        [Test]
        public void PermissionAndSubscriptionRemainConsistent()
        {
            using (var runtime = new DemoFeatureLabRuntime())
            {
                Assert.That(runtime.Snapshot.PermissionKnown, Is.False);
                runtime.RequestPermission();
                Assert.That(runtime.Snapshot.Permission, Is.True);
                Assert.That(runtime.Snapshot.PermissionKnown, Is.True);
                Assert.That(runtime.Snapshot.OptedIn, Is.True);
                Assert.That(runtime.Snapshot.NotificationTypes, Is.GreaterThan(0));

                runtime.SetPushEnabled(false);
                Assert.That(runtime.Snapshot.OptedIn, Is.False);
                Assert.That(runtime.Snapshot.Token, Is.Not.Empty);
            }
        }

        [Test]
        public void IosActivityCopyNamesLiveActivities()
        {
            var copy = FeatureLabView.ActivityCopy(RuntimePlatform.IPhonePlayer, "ignored");

            Assert.That(copy.Title, Is.EqualTo("iOS Live Activities"));
            Assert.That(copy.PreviewButton, Is.EqualTo("Start iOS Live Activity"));
            Assert.That(copy.Capability, Does.Contain("ActivityKit"));
            Assert.That(copy.ShowSettings, Is.False);
        }

        [Test]
        public void TagsAndAliasesExposeTheSessionWrites()
        {
            using (var runtime = new DemoFeatureLabRuntime())
            {
                runtime.AddTag("plan", "pro");
                runtime.AddAlias("player_id", "p-1042");

                Assert.That(runtime.Snapshot.Tags, Is.EqualTo(new Dictionary<string, string> { ["plan"] = "pro" }));
                Assert.That(runtime.Snapshot.Aliases, Is.EqualTo(new Dictionary<string, string> { ["player_id"] = "p-1042" }));

                runtime.RemoveTag("plan");
                runtime.RemoveAlias("player_id");
                Assert.That(runtime.Snapshot.Tags, Is.Empty);
                Assert.That(runtime.Snapshot.Aliases, Is.Empty);
            }
        }

        [TestCase("Simple", "Welcome to OpenPush")]
        [TestCase("Image", "A new feature just landed")]
        [TestCase("Button", "Your order is ready")]
        [TestCase("Custom", "Feature unlocked")]
        [TestCase("Sound", "New message")]
        public void EveryPnsFixtureHasEditablePayloadData(string kind, string expectedTitle)
        {
            var draft = FeatureLabController.DefaultDraft(kind);
            Assert.That(draft.Kind, Is.EqualTo(kind));
            Assert.That(draft.Title, Is.EqualTo(expectedTitle));
            Assert.That(draft.Description, Is.Not.Empty);
            Assert.That(draft.DataKey, Is.Not.Empty);
            Assert.That(draft.DataValue, Is.Not.Empty);
        }

        [Test]
        public void SnapshotShowsNativeAndWrapperVersionsAndNoTokenForwarding()
        {
            var runtime = new OpenPushFeatureLabRuntime();
            Assert.AreEqual(OpenPushSDK.OpenPush.SdkVersion, runtime.Snapshot.SdkVersion);
            Assert.AreEqual("1.2.2", runtime.Snapshot.WrapperVersion);
            // The iOS token-forwarding bridge is gone: the package forwards Unity's APNs token itself.
            Assert.IsEmpty(System.Array.FindAll(typeof(OpenPushFeatureLabRuntime).Assembly.GetTypes(), t => t.Name.Contains("PushToken")));
            runtime.Dispose();
        }

        [Test]
        public void ObserverPreviewsRaiseReadableEvents()
        {
            using (var runtime = new DemoFeatureLabRuntime())
            {
                var events = new List<LabEvent>();
                runtime.EventRaised += events.Add;
                runtime.PreviewNotification(FeatureLabController.DefaultDraft("Simple"), true);
                runtime.PreviewClick(FeatureLabController.DefaultDraft("Button"));

                Assert.That(events, Has.Count.EqualTo(2));
                Assert.That(events[0].Detail, Does.Contain("presentation suppressed"));
                Assert.That(events[1].Title, Is.EqualTo("Click listener"));
                Assert.That(events[1].Detail, Does.Contain(FeatureLabController.DefaultDraft("Button").LaunchUrl));
            }
        }
    }
}
