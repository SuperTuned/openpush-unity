using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    /// Generic detection and Reuse / Create new on iOS, on exported-project fixtures
    /// (Tests~/Fixtures/ios, generated with XcodeGen from each project.yml).
    public class OpenPushExistingExtensionTests
    {
        private const string App = "com.example.game";
        private static readonly string VendorTarget = IosExportFixture.Vendor + "NotificationServiceExtension";
        private static readonly string VendorGroup = "group." + App + "." + IosExportFixture.VendorLower;
        private const string Shared = "group.com.example.game.shared";
        private IosExportFixture _export;

        private IosExportFixture Fixture(string name)
        {
            _export = new IosExportFixture(App, name);
            return _export;
        }

        [TearDown]
        public void TearDown() => _export?.Dispose();

        private string Guid(string target) => _export.Project.TargetGuidByName(target);

        // ---- detection

        [Test]
        public void AVendorExportIsFoundByWhatItIsAndNamedByItsFingerprint()
        {
            var export = OpenPushXcodeExport.Scan(Fixture("VendorExport").Root);
            var nse = export.HostServiceExtensions.Single();
            Assert.AreEqual(VendorTarget, nse.Target);
            Assert.AreEqual(App + "." + VendorTarget, nse.BundleId);
            Assert.AreEqual(IosExportFixture.VendorLower, nse.Vendor);
            Assert.AreEqual("NotificationService", nse.ClassName);
            Assert.AreEqual(VendorTarget + "/NotificationService.swift", nse.PrincipalFile);
            CollectionAssert.AreEqual(new[] { VendorGroup }, nse.Groups);
            CollectionAssert.AreEqual(new[] { VendorGroup }, export.AppGroups);

            var findings = export.Findings();
            var group = findings.Single(f => f.Kind == OpenPushSetupFinding.AppGroup);
            Assert.AreEqual(VendorGroup, group.Id);
            Assert.AreEqual(IosExportFixture.VendorLower, group.Vendor);
            Assert.IsTrue(group.Flag("on_app"));
            CollectionAssert.AreEqual(new[] { VendorTarget }, group.Strings("on_extensions"));
        }

        [Test]
        public void AnUnknownExtensionHasNoVendor()
        {
            var nse = OpenPushXcodeExport.Scan(Fixture("CustomExtension").Root).HostServiceExtensions.Single();
            Assert.AreEqual("RichPush", nse.Target);
            Assert.IsNull(nse.Vendor);
            Assert.AreEqual(OpenPushIosExtensionMode.Share, OpenPushExtensionModeDecision.ReuseMode(nse));
            Assert.AreEqual("RichPushService", nse.ClassName);
        }

        // ---- the mode switch at export

        [Test]
        public void ABatchBuildWithNoAnswerKeepsTheExportsOwnExtensionAsEarlierBuildsDid()
        {
            // An upgraded project that never answered in Setup: its CI build must not start failing.
            Fixture("VendorExport");
            UnityEngine.Application.isBatchMode = true;
            var pbxproj = _export.Read("Unity-iPhone.xcodeproj/project.pbxproj");
            var source = _export.Read(VendorTarget + "/NotificationService.swift");

            Assert.DoesNotThrow(() => _export.Export());

            Assert.IsNull(Guid(OpenPushIOSIdentifiers.ServiceExtensionTarget), "no second extension");
            Assert.AreEqual(source, _export.Read(VendorTarget + "/NotificationService.swift"), "the extension is not touched");
            Assert.AreEqual(pbxproj, _export.Read("Unity-iPhone.xcodeproj/project.pbxproj"));
            Assert.IsFalse(Directory.Exists(Path.Combine(_export.Root, ".openpush-backup")));
            Assert.AreEqual(OpenPushIosExtensionMode.Inject, _export.Settings.IosExtensionMode, "nothing is stored");
            Assert.IsEmpty(EditorUtility.Dialogs, "a batch build never prompts");
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.StartsWith("warning: ") && l.Contains(VendorTarget)
                                                            && l.Contains("adds none")), "the build log says why");
        }

        [Test]
        public void ABatchBuildWithAnAnsweredInjectStillFailsNamingTheStoredAnswerAndTheExtension()
        {
            Fixture("VendorExport");
            OpenPushPlumbingAnswers.SetServiceExtension(_export.Settings, OpenPushIosExtensionMode.Inject, (OpenPushXcodeExtension)null);
            UnityEngine.Application.isBatchMode = true;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("IosExtensionMode = Inject", e.Message);
            StringAssert.Contains(VendorTarget, e.Message);
            Assert.IsNull(Guid(OpenPushIOSIdentifiers.ServiceExtensionTarget), "no second extension");
        }

        [Test]
        public void AnInteractiveBuildAsksOnceThenTakesTheVendorExtensionOver()
        {
            Fixture("VendorExport");
            EditorUtility.DialogAnswer = 0;
            _export.Export();

            Assert.AreEqual(1, EditorUtility.Dialogs.Count);
            StringAssert.Contains("your previous push SDK", EditorUtility.Dialogs[0]);
            Assert.AreEqual(OpenPushIosExtensionMode.TakeOver, _export.Settings.IosExtensionMode);
            Assert.AreEqual(VendorTarget, _export.Settings.IosExtensionTarget);
            Assert.AreEqual(App + "." + VendorTarget, _export.Settings.ServiceExtensionBundleIdentifier, "Reuse fills the identifier");
            StringAssert.Contains("\"take_over\"", File.ReadAllText(OpenPushPlumbingAnswers.SetupFilePath));
            Assert.AreEqual(VendorGroup, _export.Settings.AppGroupIdentifier, "the shared group is reused too");

            var source = _export.Read(VendorTarget + "/NotificationService.swift");
            StringAssert.Contains(OpenPushExistingExtensionFiles.TakeOverMarker, source);
            StringAssert.Contains("final class NotificationService: UNNotificationServiceExtension", source);
            StringAssert.DoesNotContain(IosExportFixture.Vendor + "Extension", source);
            StringAssert.Contains("import " + IosExportFixture.Vendor + "Extension",
                                  _export.Read(".openpush-backup/" + VendorTarget + "/NotificationService.swift"), "original backed up");

            var guid = Guid(VendorTarget);
            Assert.AreEqual("16.0", _export.Project.GetBuildPropertyForAnyConfig(guid, "IPHONEOS_DEPLOYMENT_TARGET"));
            Assert.AreEqual(App + "." + VendorTarget, _export.Setting(guid, "Release", "PRODUCT_BUNDLE_IDENTIFIER"), "bundle id kept");
            Assert.IsTrue(_export.Project.BuildSectionFiles.Contains(guid + ":file-Frameworks/OpenPushNative/OpenPushNSE.xcframework"));
            Assert.IsNull(Guid(OpenPushIOSIdentifiers.ServiceExtensionTarget));

            Assert.IsFalse(UnityEngine.Debug.Logged.Any(l => l.Contains("Podfile")), "no Podfile, nothing to say");

            // Second build: the answer is stored, nothing asks, the take-over is regenerated idempotently.
            EditorUtility.Dialogs.Clear();
            _export.Export();
            Assert.IsEmpty(EditorUtility.Dialogs);
            Assert.AreEqual(source, _export.Read(VendorTarget + "/NotificationService.swift"));
        }

        [Test]
        public void TakeOverPrintsThePodfileLinesLeftForTheTarget()
        {
            Fixture("VendorExport");
            File.WriteAllText(Path.Combine(_export.Root, "Podfile"), "target '" + VendorTarget + "' do\n  pod 'X'\nend\n");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            _export.Export();
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.StartsWith("warning:") && l.Contains("target '" + VendorTarget + "' do\n  pod 'X'\nend")));
        }

        [Test]
        public void TakeOverWithTheVendorsGroupReusesItOnBothEnds()
        {
            Fixture("VendorExport");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            _export.Settings.AppGroupSource = OpenPushPlumbingSource.Reused;
            _export.Export();
            StringAssert.Contains("options.appGroup = \"" + VendorGroup + "\"", _export.Read(VendorTarget + "/NotificationService.swift"));
            CollectionAssert.AreEqual(new[] { VendorGroup }, _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"));
            CollectionAssert.AreEqual(new[] { VendorGroup }, _export.Groups(VendorTarget + "/" + VendorTarget + ".entitlements"));
            Assert.IsEmpty(ProjectCapabilityManager.GroupWrites, "both files already carry the group");
        }

        [Test]
        public void TakeOverOnManualSigningKeepsEveryProfileAndStyle()
        {
            Fixture("ManualSigning");
            PlayerSettings.iOS.appleEnableAutomaticSigning = false;
            UnityEngine.Application.isBatchMode = true;
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            _export.Settings.AppGroupIdentifier = VendorGroup;
            _export.Settings.ServiceExtensionProvisioningProfile = "Some Other Profile";
            _export.Export();

            var guid = Guid(VendorTarget);
            Assert.AreEqual("Game NSE Development", _export.Setting(guid, "Debug", "PROVISIONING_PROFILE_SPECIFIER"));
            Assert.AreEqual("Game NSE AppStore", _export.Setting(guid, "Release", "PROVISIONING_PROFILE_SPECIFIER"));
            Assert.AreEqual("Manual", _export.Setting(guid, "Release", "CODE_SIGN_STYLE"));
            Assert.AreEqual("Apple Distribution", _export.Setting(guid, "Release", "CODE_SIGN_IDENTITY"));
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.Contains("kept") && l.Contains("Game NSE AppStore")));
            // The two configurations the fixture leaves unsigned get the profile from the setting.
            Assert.AreEqual("Some Other Profile", _export.Setting(guid, "ReleaseForRunning", "PROVISIONING_PROFILE_SPECIFIER"));
            Assert.AreEqual("Game NSE AppStore", OpenPushXcodeExport.Scan(_export.Root).Extension(VendorTarget).Profile, "Release first");
        }

        [Test]
        public void ReuseCarriesALegacyProfileUuidWhenThereIsNoSpecifier()
        {
            Fixture("GroupWithoutExtension");
            var raw = new OpenPushPbxproj.Target { Name = "NSE" };
            raw.Configurations.Add(new OpenPushPbxproj.Configuration { Name = "Release" });
            raw.Configurations[0].Settings["PROVISIONING_PROFILE"] = "0f3c2a1b-uuid";
            var host = new OpenPushXcodeExtension { Target = "NSE", BundleId = App + ".NSE", Raw = raw };
            OpenPushPlumbingAnswers.SetServiceExtension(_export.Settings, OpenPushIosExtensionMode.Share, host);
            Assert.AreEqual("0f3c2a1b-uuid", _export.Settings.ServiceExtensionProvisioningProfile);
        }

        [Test]
        public void ShareKeepsTheStudiosCodeByteForByteOutsideTheInsertedLines()
        {
            Fixture("CustomExtension");
            var original = _export.Read("RichPush/RichPushService.swift");
            EditorUtility.DialogAnswer = 0;
            _export.Export();

            Assert.AreEqual(OpenPushIosExtensionMode.Share, _export.Settings.IosExtensionMode);
            var patched = _export.Read("RichPush/RichPushService.swift");
            // Reuse in the dialog also reuses the group the app and the extension share, when never answered.
            Assert.AreEqual(OpenPushPlumbingSource.Reused, _export.Settings.AppGroupSource);
            var result = new OpenPushSharePatcher("RichPushService", Shared).Patch(original);
            Assert.AreEqual(result.Text, patched);
            var kept = patched.Split('\n').Where((_, i) => !result.InsertedLines.Contains(i));
            CollectionAssert.AreEqual(original.Split('\n'), kept);
            StringAssert.Contains("if openPush.handleIfOpenPush(request, withContentHandler: contentHandler) { return }", patched);
            StringAssert.Contains("super.serviceExtensionTimeWillExpire()", patched, "an inserted expiry override passes on to super");
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("RichPush/RichPush.entitlements"), "the extension keeps its own groups");
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"));
            Assert.IsFalse(ProjectCapabilityManager.GroupWrites.Any(w => w.StartsWith("RichPush/")), "a file that has the group is not rewritten");
        }

        [Test]
        public void ShareStopsWithAPatchFileAtAnIfBlock()
        {
            Fixture("CustomExtension");
            var path = Path.Combine(_export.Root, "RichPush/RichPushService.swift");
            var withIf = File.ReadAllText(path).Replace("    private var content:", "    #if DEBUG\n    private var debug = true\n    #endif\n    private var content:");
            File.WriteAllText(path, withIf);
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("#if blocks", e.Message);
            Assert.AreEqual(withIf, File.ReadAllText(path), "untouched");
            StringAssert.Contains("handleIfOpenPush", _export.Read(OpenPushExistingExtensionFiles.PatchFile));
        }

        [Test]
        public void ObjectiveCIsNeverEdited()
        {
            Fixture("CustomExtension");
            var swift = Path.Combine(_export.Root, "RichPush/RichPushService.swift");
            File.Delete(swift);
            var objc = Path.Combine(_export.Root, "RichPush/RichPushService.m");
            File.WriteAllText(objc, "#import \"RichPushService.h\"\n@implementation RichPushService\n@end\n");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            Assert.Throws<BuildFailedException>(() => _export.Export());
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("Objective-C", e.Message);
            Assert.AreEqual("#import \"RichPushService.h\"\n@implementation RichPushService\n@end\n", File.ReadAllText(objc));
            StringAssert.Contains("final class OpenPushNotificationService: RichPushService", _export.Read(OpenPushExistingExtensionFiles.PatchFile));
        }

        [Test]
        public void OffChangesNothing()
        {
            Fixture("VendorExport");
            var before = _export.Read(VendorTarget + "/NotificationService.swift");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Off;
            UnityEngine.Application.isBatchMode = true;
            _export.Export();
            Assert.AreEqual(before, _export.Read(VendorTarget + "/NotificationService.swift"));
            Assert.IsNull(Guid(OpenPushIOSIdentifiers.ServiceExtensionTarget));
            Assert.IsFalse(PBXProject.Recorded.Any(r => r.StartsWith(Guid(VendorTarget))));
        }

        [Test]
        public void AnAnsweredGroupIsNotChangedByTheExtensionDialog()
        {
            Fixture("CustomExtension");
            OpenPushPlumbingAnswers.SetAppGroup(_export.Settings, OpenPushPlumbingSource.New, null);
            EditorUtility.DialogAnswer = 0;
            _export.Export();
            Assert.AreEqual(OpenPushPlumbingSource.New, _export.Settings.AppGroupSource);
            StringAssert.Contains("group.com.example.game.openpush", _export.Read("RichPush/RichPushService.swift"));
            // Added beside the extension's own group, never instead of it, with a warning about its profile.
            CollectionAssert.AreEqual(new[] { Shared, "group.com.example.game.openpush" }, _export.Groups("RichPush/RichPush.entitlements"));
            CollectionAssert.AreEqual(new[] { Shared, "group.com.example.game.openpush" }, _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"));
            Assert.IsTrue(UnityEngine.Debug.Logged.Any(l => l.StartsWith("warning:") && l.Contains("provisioning profile need")));
        }

        [Test]
        public void AnUnansweredGroupUsesTheExtensionsOwnForShareAndTakeOver()
        {
            Fixture("CustomExtension");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            _export.Export();
            StringAssert.Contains("options.appGroup = \"" + Shared + "\"", _export.Read("RichPush/RichPushService.swift"));
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("RichPush/RichPush.entitlements"));
            Assert.IsEmpty(ProjectCapabilityManager.GroupWrites);
        }

        [Test]
        public void AnotherExtensionThanTheStoredOneFailsABatchBuild()
        {
            Fixture("VendorExport");
            UnityEngine.Application.isBatchMode = true;
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            _export.Settings.IosExtensionTarget = "RichPush";
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("Share RichPush", e.Message);
            StringAssert.Contains(VendorTarget, e.Message);
        }

        [Test]
        public void TakeOverOfAnExtensionAClearExportNoLongerHasInjectsOpenPushsUnderItsIds()
        {
            Fixture("GroupWithoutExtension");
            UnityEngine.Application.isBatchMode = true;
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            _export.Settings.IosExtensionTarget = VendorTarget;
            _export.Settings.ServiceExtensionBundleIdentifier = App + "." + VendorTarget;
            _export.Export();
            var guid = Guid(OpenPushIOSIdentifiers.ServiceExtensionTarget);
            Assert.IsNotNull(guid);
            Assert.AreEqual(App + "." + VendorTarget, _export.Setting(guid, "Release", "PRODUCT_BUNDLE_IDENTIFIER"));
        }

        [Test]
        public void OpenPushsOwnExtensionBesideAHostOneStopsTheBuild()
        {
            Fixture("VendorExport");
            _export.Project.Targets[OpenPushIOSIdentifiers.ServiceExtensionTarget] = "guid-own";
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.TakeOver;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("two notification service extensions", e.Message);
        }

        // ---- App Groups

        [Test]
        public void AGroupWithoutAnExtensionIsReusedForOpenPushsExtension()
        {
            Fixture("GroupWithoutExtension");
            _export.Settings.AppGroupSource = OpenPushPlumbingSource.Reused;
            _export.Export();
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"));
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("OpenPushNotificationService/OpenPushNotificationService.entitlements"));
            StringAssert.Contains(Shared, _export.Read("OpenPushNotificationService/Info.plist"));
        }

        [Test]
        public void OfTwoGroupsTheOneOnBothTheAppAndTheExtensionIsReused()
        {
            Fixture("TwoGroups");
            _export.Settings.AppGroupSource = OpenPushPlumbingSource.Reused;
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            _export.Export();
            StringAssert.Contains("options.appGroup = \"" + Shared + "\"", _export.Read("RichPush/RichPushService.swift"));
            CollectionAssert.AreEqual(new[] { Shared, "group.com.example.game.analytics" }, _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"),
                                      "the app keeps its other group");
        }

        [Test]
        public void ANamedGroupIsAddedBesideEveryGroupTheAppAlreadyHas()
        {
            Fixture("TwoGroups");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Off;
            _export.Settings.AppGroupIdentifier = "group.com.example.game.openpush";
            _export.Export();
            CollectionAssert.AreEqual(new[] { Shared, "group.com.example.game.analytics", "group.com.example.game.openpush" },
                                      _export.Groups("Unity-iPhone/Unity-iPhone.entitlements"));
            CollectionAssert.AreEqual(new[] { Shared }, _export.Groups("RichPush/RichPush.entitlements"), "Off leaves the extension alone");
        }

        [Test]
        public void AnAmbiguousGroupStopsTheBuild()
        {
            Fixture("TwoGroups");
            var entitlements = Path.Combine(_export.Root, "RichPush/RichPush.entitlements");
            File.WriteAllText(entitlements, File.ReadAllText(entitlements).Replace("<string>" + Shared + "</string>",
                "<string>" + Shared + "</string><string>group.com.example.game.analytics</string>"));
            _export.Settings.AppGroupSource = OpenPushPlumbingSource.Reused;
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Share;
            var e = Assert.Throws<BuildFailedException>(() => _export.Export());
            StringAssert.Contains("more than one App Group", e.Message);
            StringAssert.Contains("group.com.example.game.analytics", e.Message);
        }

        // ---- the answer file

        [Test]
        public void TheExportIsRecordedAsFoundAndChoicesAndUnknownFieldsSurvive()
        {
            Fixture("VendorExport");
            Directory.CreateDirectory(Path.GetDirectoryName(OpenPushPlumbingAnswers.SetupFilePath));
            File.WriteAllText(OpenPushPlumbingAnswers.SetupFilePath,
                "{\"schema\":\"openpush-setup/1\",\"note\":\"keep me\",\"found\":[{\"kind\":\"stale\"}],\"choices\":{\"icons\":{\"mode\":\"reuse\"}}}");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Off;
            _export.Export();

            var file = OpenPushSetupFile.Read(OpenPushPlumbingAnswers.SetupFilePath);
            StringAssert.Contains("\"note\": \"keep me\"", file.ToJson());
            Assert.IsTrue(file.HasChoice("icons"));
            Assert.IsFalse(file.Found.Any(f => f.Kind == "stale"), "found is regenerated");
            var nse = file.Found.Single(f => f.Kind == OpenPushSetupFinding.ServiceExtension);
            Assert.AreEqual(VendorTarget, nse.Id);
            Assert.AreEqual(App + "." + VendorTarget, nse.Detail("bundle_id"));
            Assert.AreEqual(IosExportFixture.VendorLower, nse.Vendor);
            Assert.IsFalse(nse.Details.ContainsKey("configurations"), "no per-configuration signing in the committed file");

            // An unchanged second export does not rewrite the file.
            var written = File.GetLastWriteTimeUtc(OpenPushPlumbingAnswers.SetupFilePath);
            File.SetLastWriteTimeUtc(OpenPushPlumbingAnswers.SetupFilePath, written.AddMinutes(-5));
            _export.Export();
            Assert.AreEqual(written.AddMinutes(-5), File.GetLastWriteTimeUtc(OpenPushPlumbingAnswers.SetupFilePath));
        }

        [Test]
        public void AnUnparseableAnswerFileIsNeverOverwritten()
        {
            Fixture("VendorExport");
            Directory.CreateDirectory(Path.GetDirectoryName(OpenPushPlumbingAnswers.SetupFilePath));
            File.WriteAllText(OpenPushPlumbingAnswers.SetupFilePath, "{ not json");
            _export.Settings.IosExtensionMode = OpenPushIosExtensionMode.Off;
            _export.Export();
            Assert.AreEqual("{ not json", File.ReadAllText(OpenPushPlumbingAnswers.SetupFilePath));
        }
    }
}
