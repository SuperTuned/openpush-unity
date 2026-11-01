using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor;
using UnityEditor.Build;

namespace OpenPushSDK.Tests
{
    /// Explicit extension identifiers and signing, through the real post-build steps.
    public class OpenPushExtensionSigningTests
    {
        private const string App = "com.example.game";
        // The extension App ID and App Group the previous push SDK registered for the app.
        private const string Nse = App + ".OneSignalNotificationServiceExtension";
        private const string Group = "group." + App + ".onesignal";
        private const string Profile = "Example NSE AppStore";
        private const string Service = "OpenPushNotificationService";
        private const string ServiceGuid = "guid-OpenPushNotificationService";
        private const string ServiceEntitlements = "OpenPushNotificationService/OpenPushNotificationService.entitlements";
        private const string AppEntitlements = "Unity-iPhone/Unity-iPhone.entitlements";
        private static readonly string[] Configs = { "Debug", "Release", "ReleaseForProfiling", "ReleaseForRunning" };
        private static readonly string[] SigningKeys = { "CODE_SIGN_STYLE", "CODE_SIGN_IDENTITY", "PROVISIONING_PROFILE_SPECIFIER", "PROVISIONING_PROFILE" };

        private IosExportFixture _export;

        [SetUp]
        public void SetUp() => _export = new IosExportFixture(App);

        [TearDown]
        public void TearDown() => _export.Dispose();

        /// A manually signed app that keeps the previous SDK's extension App ID and App Group.
        private void ManualSigningWithExistingExtension()
        {
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            PlayerSettings.iOS.iOSManualProvisioningProfileID = "Example AppStore";
            UnityEngine.Application.isBatchMode = true;
            _export.Settings.AppGroupIdentifier = Group;
            _export.Settings.ServiceExtensionBundleIdentifier = Nse;
            _export.Settings.ServiceExtensionProvisioningProfile = Profile;
            var project = _export.Project;
            foreach (var config in Configs)
                project.SetBuildPropertyForConfig(project.BuildConfigByName("main", config), "CODE_SIGN_IDENTITY",
                    config == "Debug" ? "iPhone Developer" : "iPhone Distribution");
        }

        [Test]
        public void ManualSigningSignsTheExtensionWithItsOwnAppIdGroupAndProfile()
        {
            ManualSigningWithExistingExtension();
            _export.Export();

            foreach (var config in Configs)
            {
                Assert.AreEqual(Nse, _export.Setting(ServiceGuid, config, "PRODUCT_BUNDLE_IDENTIFIER"), config);
                Assert.AreEqual("Manual", _export.Setting(ServiceGuid, config, "CODE_SIGN_STYLE"), config);
                Assert.AreEqual(Profile, _export.Setting(ServiceGuid, config, "PROVISIONING_PROFILE_SPECIFIER"), config);
                Assert.AreEqual(config == "Debug" ? "iPhone Developer" : "iPhone Distribution",
                                _export.Setting(ServiceGuid, config, "CODE_SIGN_IDENTITY"), "copied per configuration: " + config);
            }
            CollectionAssert.Contains(_export.Groups(AppEntitlements), Group);
            CollectionAssert.AreEqual(new[] { Group }, _export.Groups(ServiceEntitlements));
            Assert.AreEqual(Group, OpenPushIOSBuildSettings.ReadPlistString(Path.Combine(_export.Root, Service, "Info.plist"), "OpenPushAppGroup"));
        }

        [Test]
        public void AnAppendExportKeepsAProfileAlreadyOnTheTarget()
        {
            ManualSigningWithExistingExtension();
            _export.Export();
            var project = _export.Project;
            project.SetBuildPropertyForConfig(project.BuildConfigByName(ServiceGuid, "Release"), "PROVISIONING_PROFILE_SPECIFIER", "Hand Picked");
            project.SetBuildPropertyForConfig(project.BuildConfigByName(ServiceGuid, "Debug"), "CODE_SIGN_STYLE", "Automatic");
            _export.Settings.ServiceExtensionProvisioningProfile = "Another Profile";

            _export.Export();

            Assert.AreEqual("Hand Picked", _export.Setting(ServiceGuid, "Release", "PROVISIONING_PROFILE_SPECIFIER"));
            Assert.AreEqual(Profile, _export.Setting(ServiceGuid, "Debug", "PROVISIONING_PROFILE_SPECIFIER"), "never overwritten");
            Assert.AreEqual("Automatic", _export.Setting(ServiceGuid, "Debug", "CODE_SIGN_STYLE"));
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.Contains("kept") && l.Contains("Hand Picked")), "what was kept is logged");
        }

        [Test]
        public void BlankFieldsWriteOnlyTheDefaultSettings()
        {
            _export.Export();
            AssertOnlyDefaultSettings($"{App}.{Service}");
            CollectionAssert.AreEqual(new[] { "group." + App + ".openpush" }, _export.Groups(ServiceEntitlements));
        }

        [Test]
        public void AutomaticSigningOffWithoutAnyAppProfileStillWritesNoSigning()
        {
            // A fresh project, or CI that exports with signing off: nobody signs in Unity.
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            UnityEngine.Application.isBatchMode = true;
            _export.Export();
            AssertOnlyDefaultSettings($"{App}.{Service}");
        }

        private void AssertOnlyDefaultSettings(string bundleId)
        {
            // Every key the injector (and the capability manager) writes on the extension by default, and no other.
            var written = PBXProject().Where(r => r.StartsWith(ServiceGuid) && !r.Contains(":link "))
                .Select(r => r.Substring(r.IndexOf(':') + 1).Split('=')[0]).Distinct().OrderBy(k => k, System.StringComparer.Ordinal);
            CollectionAssert.AreEqual(new[]
            {
                "APPLICATION_EXTENSION_API_ONLY", "CODE_SIGN_ENTITLEMENTS", "GENERATE_INFOPLIST_FILE", "INFOPLIST_FILE",
                "IPHONEOS_DEPLOYMENT_TARGET", "PRODUCT_BUNDLE_IDENTIFIER", "SKIP_INSTALL", "SWIFT_VERSION", "TARGETED_DEVICE_FAMILY",
            }, written.ToArray());
            foreach (var config in Configs)
            {
                Assert.AreEqual(bundleId, _export.Setting(ServiceGuid, config, "PRODUCT_BUNDLE_IDENTIFIER"));
                foreach (var key in SigningKeys) Assert.IsNull(_export.Setting(ServiceGuid, config, key), key);
            }
        }

        private static System.Collections.Generic.IEnumerable<string> PBXProject() => UnityEditor.iOS.Xcode.PBXProject.Recorded;

        [Test]
        public void ABatchBuildWithManualSigningAndNoExtensionProfileFailsNamingTheField()
        {
            ManualSigningWithExistingExtension();
            _export.Settings.ServiceExtensionProvisioningProfile = "";
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("ServiceExtensionProvisioningProfile", e.Message);
            StringAssert.Contains(Nse, e.Message);
            Assert.IsFalse(Directory.Exists(Path.Combine(_export.Root, Service)), "fails before anything is written");
        }

        [Test]
        public void AnInteractiveBuildWithoutTheProfileWarnsAndExports()
        {
            ManualSigningWithExistingExtension();
            UnityEngine.Application.isBatchMode = false;
            _export.Settings.ServiceExtensionProvisioningProfile = " ";
            _export.Export();
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.StartsWith("warning:") && l.Contains("ServiceExtensionProvisioningProfile")));
            Assert.AreEqual("Manual", _export.Setting(ServiceGuid, "Release", "CODE_SIGN_STYLE"));
            Assert.IsNull(_export.Setting(ServiceGuid, "Release", "PROVISIONING_PROFILE_SPECIFIER"));
        }

        [Test]
        public void AProfileWithAutomaticAppSigningSignsOnlyThatExtensionManually()
        {
            _export.Settings.ServiceExtensionProvisioningProfile = Profile;
            _export.Export();
            Assert.AreEqual("Manual", _export.Setting(ServiceGuid, "Release", "CODE_SIGN_STYLE"));
            Assert.AreEqual(Profile, _export.Setting(ServiceGuid, "Release", "PROVISIONING_PROFILE_SPECIFIER"));
            Assert.IsNull(_export.Setting("main", "Release", "CODE_SIGN_STYLE"), "the app target is never touched");
        }

        [Test]
        public void ABundleIdOutsideTheAppsFailsTheBuild()
        {
            _export.Settings.ServiceExtensionBundleIdentifier = "com.other.NotificationService";
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("ServiceExtensionBundleIdentifier", e.Message);
            StringAssert.Contains(App + ".", e.Message);
        }

        [Test]
        public void TheWidgetUsesItsOwnIdAndProfile()
        {
            ManualSigningWithExistingExtension();
            _export.Settings.InjectLiveActivityWidget = true;
            _export.Settings.LiveActivityWidgetBundleIdentifier = App + ".Widgets";
            _export.Settings.LiveActivityWidgetProvisioningProfile = "Example Widget AppStore";
            _export.Export();
            const string widget = "guid-OpenPushLiveActivityWidget";
            Assert.AreEqual(App + ".Widgets", _export.Setting(widget, "Release", "PRODUCT_BUNDLE_IDENTIFIER"));
            Assert.AreEqual("Manual", _export.Setting(widget, "Release", "CODE_SIGN_STYLE"));
            Assert.AreEqual("Example Widget AppStore", _export.Setting(widget, "Release", "PROVISIONING_PROFILE_SPECIFIER"));
        }

        [Test]
        public void ABatchBuildFailsOnAWidgetWithoutAProfileToo()
        {
            ManualSigningWithExistingExtension();
            _export.Settings.InjectLiveActivityWidget = true;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("LiveActivityWidgetProvisioningProfile", e.Message);
        }

        [TestCase(null, null)]
        [TestCase("  ", null)]
        [TestCase("com.example.game.NSE", null)]
        [TestCase("com.example.game.Ext-1.Sub", null)]
        [TestCase("com.example.game", "must start with")]
        [TestCase("com.example.game.", "is not a bundle id")]
        [TestCase("com.example.gameX.NSE", "must start with")]
        [TestCase("com.example.game.N SE", "is not a bundle id")]
        public void BundleIdValidation(string configured, string problem)
        {
            var actual = OpenPushIOSIdentifiers.BundleIdProblem("ServiceExtensionBundleIdentifier", configured, App);
            if (problem == null) Assert.IsNull(actual);
            else StringAssert.Contains(problem, actual);
        }

        [Test]
        public void TheSetupRowReportsMissingProfilesOnlyUnderManualSigning()
        {
            Assert.IsEmpty(OpenPushIOSIdentifiers.Problems(App, false, true, Nse, null, true, null, null));
            var problems = OpenPushIOSIdentifiers.Problems(App, true, true, Nse, null, false, null, null);
            Assert.AreEqual(1, problems.Count);
            StringAssert.Contains("ServiceExtensionProvisioningProfile", problems[0]);
            StringAssert.Contains(Nse, problems[0]);
            Assert.AreEqual(2, OpenPushIOSIdentifiers.Problems(App, true, true, "bad id", Profile, true, null, null).Count);
            Assert.IsEmpty(OpenPushIOSIdentifiers.Problems(App, true, true, Nse, null, false, null, null, OpenPushSDK.Core.OpenPushIosExtensionMode.Off),
                           "Off needs no profile");
            Assert.IsEmpty(OpenPushIOSIdentifiers.Problems(App, true, true, Nse, null, false, null, null,
                OpenPushSDK.Core.OpenPushIosExtensionMode.TakeOver, "Kept Profile"), "TakeOver keeps the target's own profile");
            Assert.AreEqual(1, OpenPushIOSIdentifiers.Problems(App, true, true, Nse, null, false, null, null,
                OpenPushSDK.Core.OpenPushIosExtensionMode.Share, null).Count, "a reused target without a profile still needs one");
        }
    }
}
