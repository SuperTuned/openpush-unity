using System.IO;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    public class OpenPushIOSEntitlementsTests
    {
        private string _root;

        [SetUp]
        public void CreateExportRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "openpush-entitlements-" + Path.GetRandomFileName());
            Directory.CreateDirectory(Path.Combine(_root, "Unity-iPhone"));
        }

        [TearDown]
        public void DeleteExportRoot() => Directory.Delete(_root, true);

        private void Touch(string relative) => File.WriteAllText(Path.Combine(_root, relative), "<plist/>");

        [Test]
        public void FreshExportUsesTheTargetNamedFile()
        {
            // The name Unity's own capability code and host post-processors use, so a host
            // that later adds this same path to CODE_SIGN_ENTITLEMENTS keeps one value.
            var project = new PBXProject();
            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void ReusesTheFileTheTargetAlreadySignsWith()
        {
            Touch("Unity-iPhone/Game.entitlements");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/Game.entitlements");
            Assert.AreEqual("Unity-iPhone/Game.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void ConfiguredFileThatIsMissingFallsBackToTheTargetNamedFile()
        {
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/Gone.entitlements");
            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void SrcRootPrefixAndBackslashesAreNormalised()
        {
            Touch("Unity-iPhone/Game.entitlements");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "$(SRCROOT)/Unity-iPhone\\Game.entitlements");
            Assert.AreEqual("Unity-iPhone/Game.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void AlreadyDoubledSettingFallsBackToTheTargetNamedFile()
        {
            Touch("Unity-iPhone/OpenPush.entitlements");
            Touch("Unity-iPhone/Unity-iPhone.entitlements");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS",
                                     "Unity-iPhone/OpenPush.entitlements Unity-iPhone/Unity-iPhone.entitlements");
            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void AnAppendExportOfTheLegacyEntitlementsFileMovesOntoTheTargetNamedFile()
        {
            File.WriteAllText(Path.Combine(_root, "Unity-iPhone/OpenPush.entitlements"), "<plist>push</plist>");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/OpenPush.entitlements");

            Assert.IsTrue(OpenPushIOSCapabilities.MigrateLegacyEntitlements(project, "main", _root));

            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            project.GetBuildPropertyForAnyConfig("main", "CODE_SIGN_ENTITLEMENTS"));
            Assert.AreEqual("<plist>push</plist>",
                            File.ReadAllText(Path.Combine(_root, "Unity-iPhone/Unity-iPhone.entitlements")));
            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            OpenPushIOSCapabilities.MainTargetEntitlements(project, "main", _root));
        }

        [Test]
        public void AnExistingTargetNamedFileIsNeverOverwrittenByTheLegacyFile()
        {
            File.WriteAllText(Path.Combine(_root, "Unity-iPhone/OpenPush.entitlements"), "<plist>old</plist>");
            File.WriteAllText(Path.Combine(_root, "Unity-iPhone/Unity-iPhone.entitlements"), "<plist>host</plist>");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/OpenPush.entitlements");

            Assert.IsTrue(OpenPushIOSCapabilities.MigrateLegacyEntitlements(project, "main", _root));

            Assert.AreEqual("<plist>host</plist>",
                            File.ReadAllText(Path.Combine(_root, "Unity-iPhone/Unity-iPhone.entitlements")));
        }

        [Test]
        public void AnyOtherConfiguredFileIsLeftAlone()
        {
            var project = new PBXProject();
            Assert.IsFalse(OpenPushIOSCapabilities.MigrateLegacyEntitlements(project, "main", _root));
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS", "Unity-iPhone/Game.entitlements");
            Assert.IsFalse(OpenPushIOSCapabilities.MigrateLegacyEntitlements(project, "main", _root));
            Assert.AreEqual("Unity-iPhone/Game.entitlements",
                            project.GetBuildPropertyForAnyConfig("main", "CODE_SIGN_ENTITLEMENTS"));
        }

        [Test]
        public void AValueAFailedExportAlreadyDoubledIsRepaired()
        {
            Touch("Unity-iPhone/OpenPush.entitlements");
            var project = new PBXProject();
            project.SetBuildProperty("main", "CODE_SIGN_ENTITLEMENTS",
                                     "Unity-iPhone/OpenPush.entitlements Unity-iPhone/Unity-iPhone.entitlements");
            Assert.IsTrue(OpenPushIOSCapabilities.MigrateLegacyEntitlements(project, "main", _root));
            Assert.AreEqual("Unity-iPhone/Unity-iPhone.entitlements",
                            project.GetBuildPropertyForAnyConfig("main", "CODE_SIGN_ENTITLEMENTS"));
        }
    }
}
