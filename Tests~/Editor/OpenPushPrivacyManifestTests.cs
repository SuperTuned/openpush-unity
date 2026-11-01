using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    /// The native privacy manifests reach the built app: static frameworks are never embedded, so each
    /// manifest travels in a resource bundle in Copy Bundle Resources of the target that links it.
    public class OpenPushPrivacyManifestTests
    {
        private const string App = "com.example.game";
        private IosExportFixture _export;

        [SetUp]
        public void SetUp()
        {
            _export = new IosExportFixture(App);
            // The real vendored frameworks: the manifests under test are the ones the package ships.
            OpenPushNativeFrameworks.SourceOverride = Path.Combine(IosExportFixture.PackageRoot(), "Runtime", "Plugins", "iOS", "Native~");
        }

        [TearDown]
        public void TearDown() => _export.Dispose();

        private void Run() => new OpenPushPrivacyManifest().OnPostprocessBuild(_export.Report());

        private string Copied(string module)
            => Path.Combine(_export.Root, OpenPushPrivacyManifest.BundlePath(module), OpenPushPrivacyManifest.FileName);

        private bool InResources(string target, string module)
            => _export.Project.PhaseFiles.Contains("resources-" + target + ":file-" + OpenPushPrivacyManifest.BundlePath(module));

        private static string[] Strings(string plist, string key)
            => XDocument.Load(plist).Descendants("key").Where(k => k.Value == key)
                        .SelectMany(k => k.ElementsAfterSelf().First().Descendants("string").Select(s => s.Value)
                                          .DefaultIfEmpty(k.ElementsAfterSelf().First().Value))
                        .ToArray();

        [Test]
        public void TheAppAndTheServiceExtensionCarryTheManifestsOfWhatTheyLink()
        {
            _export.Settings.InjectLiveActivityWidget = true;
            _export.Export();
            Run();

            foreach (var module in new[] { "OpenPush", "OpenPushCore", "OpenPushLiveActivities" })
                Assert.IsTrue(InResources("main", module), module + " in the app's Copy Bundle Resources");
            foreach (var module in new[] { "OpenPushCore", "OpenPushNSE" })
                Assert.IsTrue(InResources("guid-OpenPushNotificationService", module), module + " in the service extension");
            Assert.IsTrue(InResources("guid-OpenPushLiveActivityWidget", "OpenPushLiveActivities"), "the widget's manifest");
            Assert.IsFalse(InResources("main", "OpenPushNSE"), "the app does not link OpenPushNSE");

            foreach (var module in new[] { "OpenPush", "OpenPushCore", "OpenPushNSE", "OpenPushLiveActivities" })
            {
                Assert.AreEqual(File.ReadAllText(OpenPushPrivacyManifest.VendoredManifest(module)), File.ReadAllText(Copied(module)),
                                module + " is copied unchanged");
                var info = Path.Combine(Path.GetDirectoryName(Copied(module)), "Info.plist");
                CollectionAssert.AreEqual(new[] { "BNDL" }, Strings(info, "CFBundlePackageType"));
            }
        }

        [Test]
        public void TheShippedManifestsDeclareWhatTheNativeSdkCollectsAndReads()
        {
            _export.Export();
            Run();
            var collected = Strings(Copied("OpenPush"), "NSPrivacyCollectedDataType");
            foreach (var type in new[] { "NSPrivacyCollectedDataTypeDeviceID", "NSPrivacyCollectedDataTypeUserID",
                                         "NSPrivacyCollectedDataTypeEmailAddress", "NSPrivacyCollectedDataTypePhoneNumber",
                                         "NSPrivacyCollectedDataTypeProductInteraction" })
                CollectionAssert.Contains(collected, type);
            foreach (var module in new[] { "OpenPushCore", "OpenPushNSE" })
            {
                var reasons = Strings(Copied(module), "NSPrivacyAccessedAPITypeReasons");
                CollectionAssert.Contains(reasons, "CA92.1", module);
                CollectionAssert.Contains(reasons, "1C8F.1", module + " declares App Group defaults");
            }
        }

        [Test]
        public void AReExportAddsEachBundleOnce()
        {
            _export.Export();
            Run();
            Run();
            var path = "resources-main:file-" + OpenPushPrivacyManifest.BundlePath("OpenPushCore");
            Assert.AreEqual(1, _export.Project.PhaseFiles.Count(f => f == path));
        }

        [Test]
        public void AnExportWithoutOpenPushsExtensionsStillGivesTheAppItsManifests()
        {
            _export.Settings.InjectServiceExtension = false;
            _export.Export();
            Run();
            Assert.IsTrue(InResources("main", "OpenPushCore"));
            Assert.IsFalse(_export.Project.PhaseFiles.Any(f => f.Contains("OpenPushNSE.bundle")), "no extension, no NSE manifest");
        }

        [Test]
        public void ATakenOverExtensionGetsTheExtensionManifests()
        {
            _export.Dispose();
            _export = new IosExportFixture(App, "VendorExport");
            OpenPushNativeFrameworks.SourceOverride = Path.Combine(IosExportFixture.PackageRoot(), "Runtime", "Plugins", "iOS", "Native~");
            _export.Settings.IosExtensionMode = OpenPushSDK.Core.OpenPushIosExtensionMode.TakeOver;
            _export.Settings.IosExtensionTarget = IosExportFixture.Vendor + "NotificationServiceExtension";
            Run();
            var guid = _export.Project.TargetGuidByName(_export.Settings.IosExtensionTarget);
            Assert.IsNotNull(guid);
            Assert.IsTrue(InResources(guid, "OpenPushNSE"));
            Assert.IsTrue(InResources(guid, "OpenPushCore"));
        }
    }
}
