using System.IO;
using System.Linq;
using System.Xml.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    /// Apple rejects an upload whose extension Info.plist has no CFBundleDisplayName (90360), and one whose
    /// extension versions differ from the app's; the order-45 post-build fixes both on every export.
    public class OpenPushExtensionPlistTests
    {
        private const string Service = "OpenPushNotificationService";
        private const string Widget = "OpenPushLiveActivityWidget";
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "op-plist-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        private static string PackageRoot()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "package root not found above the test directory");
            return dir.FullName;
        }

        private static readonly OpenPushExtensionValues Versions = new OpenPushExtensionValues("2.4.1", "318", "TEAM123");

        private string CopyTemplate(string target, string template)
        {
            var dir = Path.Combine(_root, target);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "Info.plist");
            File.Copy(Path.Combine(PackageRoot(), "Editor", template), path);
            return path;
        }

        private static string[] Values(string plist, string key)
        {
            var keys = XDocument.Load(plist).Root.Element("dict").Elements("key").Where(k => k.Value == key);
            return keys.Select(k => k.ElementsAfterSelf().First().Value).ToArray();
        }

        private static PBXProject Project(params string[] targets)
        {
            var project = new PBXProject();
            foreach (var t in targets) project.Targets[t] = "guid-" + t;
            return project;
        }

        [Test]
        public void FreshExportCarriesALiteralDisplayNameAndSyncsVersions()
        {
            var service = CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var widget = CopyTemplate(Widget, "WidgetTemplate/Widget-Info.plist.txt");
            var project = Project(Service, Widget);

            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions);

            foreach (var (target, plist) in new[] { (Service, service), (Widget, widget) })
            {
                // A literal, like the extension templates Apple and other SDKs ship: a build variable
                // resolves to empty whenever the setting does not reach the target, and App Store
                // Connect then rejects the upload (90360).
                CollectionAssert.AreEqual(new[] { target }, Values(plist, "CFBundleDisplayName"));
                // Literal versions too: the extension's must match the app's, and a build variable
                // is empty whenever the setting does not reach this target.
                CollectionAssert.AreEqual(new[] { "2.4.1" }, Values(plist, "CFBundleShortVersionString"));
                CollectionAssert.AreEqual(new[] { "318" }, Values(plist, "CFBundleVersion"));
                Assert.AreEqual("TEAM123", project.GetBuildPropertyForAnyConfig("guid-" + target, "DEVELOPMENT_TEAM"));
            }
        }

        [Test]
        public void TheTemplatesNeedNoBuildVariableForTheDisplayNameOrVersions()
        {
            foreach (var template in new[] { "ServiceTemplate/Service-Info.plist.txt", "WidgetTemplate/Widget-Info.plist.txt" })
            {
                var text = File.ReadAllText(Path.Combine(PackageRoot(), "Editor", template));
                StringAssert.DoesNotContain("OPENPUSH_HOST_DISPLAY_NAME", text, template);
                StringAssert.DoesNotContain("$(MARKETING_VERSION)", text, template);
                StringAssert.DoesNotContain("$(CURRENT_PROJECT_VERSION)", text, template);
            }
        }

        [Test]
        public void InjectedExtensionsCarryTheAppsVersionsFromTheMomentTheyAreCreated()
        {
            // As earlier builds did: never the template's own 1.0 / 1, even before the order-45 sync runs.
            using (var export = new IosExportFixture("com.example.game"))
            {
                File.WriteAllText(Path.Combine(export.Root, "Info.plist"),
                    "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                    "<plist version=\"1.0\"><dict><key>CFBundleShortVersionString</key><string>2.4.1</string>" +
                    "<key>CFBundleVersion</key><string>318</string></dict></plist>");
                export.Settings.InjectLiveActivityWidget = true;
                export.Export();
                foreach (var target in new[] { Service, Widget })
                {
                    var plist = Path.Combine(export.Root, target, "Info.plist");
                    CollectionAssert.AreEqual(new[] { "2.4.1" }, Values(plist, "CFBundleShortVersionString"), target);
                    CollectionAssert.AreEqual(new[] { "318" }, Values(plist, "CFBundleVersion"), target);
                    StringAssert.DoesNotContain("__OPENPUSH_", File.ReadAllText(plist), target);
                }
            }
        }

        [Test]
        public void TheTemplatesHardCodeNoVersion()
        {
            foreach (var template in new[] { "ServiceTemplate/Service-Info.plist.txt", "WidgetTemplate/Widget-Info.plist.txt" })
            {
                var plist = Path.Combine(PackageRoot(), "Editor", template);
                CollectionAssert.AreEqual(new[] { OpenPushIOSBuildSettings.MarketingVersionPlaceholder }, Values(plist, "CFBundleShortVersionString"), template);
                CollectionAssert.AreEqual(new[] { OpenPushIOSBuildSettings.BuildNumberPlaceholder }, Values(plist, "CFBundleVersion"), template);
            }
        }

        [Test]
        public void TheSyncAlsoGivesEachExtensionTargetTheAppsVersionBuildSettings()
        {
            CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var project = Project(Service, Widget);
            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions);
            foreach (var target in new[] { Service, Widget })
            {
                Assert.AreEqual("2.4.1", project.GetBuildPropertyForAnyConfig("guid-" + target, "MARKETING_VERSION"), target);
                Assert.AreEqual("318", project.GetBuildPropertyForAnyConfig("guid-" + target, "CURRENT_PROJECT_VERSION"), target);
            }
        }

        [Test]
        public void AnAppendExportOfAnOlderTemplateWithVersionVariablesGetsLiteralVersions()
        {
            var plist = CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var text = File.ReadAllText(plist);
            text = System.Text.RegularExpressions.Regex.Replace(text,
                "<key>CFBundleShortVersionString</key><string>[^<]*</string>", "<key>CFBundleShortVersionString</key><string>$(MARKETING_VERSION)</string>");
            File.WriteAllText(plist, text);

            OpenPushIOSBuildSettings.SyncExtensions(Project(Service), _root, "guid-main", Versions);

            CollectionAssert.AreEqual(new[] { "2.4.1" }, Values(plist, "CFBundleShortVersionString"));
        }

        [Test]
        public void ValuesComeFromTheAppsInfoPlistAndTeamFromTheAppTarget()
        {
            File.WriteAllText(Path.Combine(_root, "Info.plist"),
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\"><dict><key>CFBundleShortVersionString</key><string>3.0</string>" +
                "<key>CFBundleVersion</key><string>${BUILD}</string></dict></plist>");
            var project = Project(Service);
            project.SetBuildProperty("guid-main", "DEVELOPMENT_TEAM", "HOSTTEAM");

            var values = OpenPushExtensionValues.From(project, "guid-main", _root);

            Assert.AreEqual("3.0", values.MarketingVersion, "the app's literal wins");
            Assert.AreEqual(PlayerSettings.iOS.buildNumber, values.BuildNumber, "a variable falls back to Player Settings");
            Assert.AreEqual("HOSTTEAM", values.DevelopmentTeam, "the app target's team wins over Player Settings");
        }

        [Test]
        public void AnAppendExportKeepsATeamAlreadyOnTheExtension()
        {
            CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var project = Project(Service);
            project.SetBuildProperty("guid-" + Service, "DEVELOPMENT_TEAM", "OTHERTEAM");
            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions);
            Assert.AreEqual("OTHERTEAM", project.GetBuildPropertyForAnyConfig("guid-" + Service, "DEVELOPMENT_TEAM"));
        }

        [Test]
        public void NoTeamAnywhereLeavesTheExtensionTargetsTeamAlone()
        {
            CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var project = Project(Service);
            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", new OpenPushExtensionValues("1.0", "1", ""));
            Assert.AreEqual(string.Empty, project.GetBuildPropertyForAnyConfig("guid-" + Service, "DEVELOPMENT_TEAM"));
        }

        [Test]
        public void AnAppendExportOfAnOlderTemplateWithTheNameVariableGetsTheLiteralName()
        {
            var plist = CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            File.WriteAllText(plist, File.ReadAllText(plist).Replace(
                "<string>" + Service + "</string>", "<string>$(OPENPUSH_HOST_DISPLAY_NAME)</string>"));
            CollectionAssert.AreEqual(new[] { "$(OPENPUSH_HOST_DISPLAY_NAME)" }, Values(plist, "CFBundleDisplayName"));

            OpenPushIOSBuildSettings.SyncExtensions(Project(Service), _root, "guid-main", Versions);

            CollectionAssert.AreEqual(new[] { Service }, Values(plist, "CFBundleDisplayName"));
            StringAssert.DoesNotContain("[]", File.ReadAllText(plist));
        }

        [Test]
        public void OldTemplatePlistGetsTheLiteralNameOnce()
        {
            var dir = Path.Combine(_root, Service);
            Directory.CreateDirectory(dir);
            var plist = Path.Combine(dir, "Info.plist");
            // An older template: CFBundleName but no CFBundleDisplayName.
            File.WriteAllText(plist, File.ReadAllText(Path.Combine(PackageRoot(), "Editor", "ServiceTemplate", "Service-Info.plist.txt"))
                .Replace("<key>CFBundleDisplayName</key><string>" + Service + "</string>\n", ""));
            Assert.IsEmpty(Values(plist, "CFBundleDisplayName"));
            var project = Project(Service);

            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions);
            OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions);

            CollectionAssert.AreEqual(new[] { Service }, Values(plist, "CFBundleDisplayName"));
            CollectionAssert.AreEqual(new[] { "$(PRODUCT_NAME)" }, Values(plist, "CFBundleName"));
            Assert.AreEqual("com.apple.usernotifications.service",
                XDocument.Load(plist).Descendants("key").First(k => k.Value == "NSExtensionPointIdentifier").ElementsAfterSelf().First().Value);
        }

        [Test]
        public void MissingWidgetIsSkipped()
        {
            CopyTemplate(Service, "ServiceTemplate/Service-Info.plist.txt");
            var project = Project(Service);
            Assert.DoesNotThrow(() => OpenPushIOSBuildSettings.SyncExtensions(project, _root, "guid-main", Versions));
            Assert.AreEqual(string.Empty, project.GetBuildPropertyForAnyConfig("guid-" + Widget, "DEVELOPMENT_TEAM"));
        }
    }
}
