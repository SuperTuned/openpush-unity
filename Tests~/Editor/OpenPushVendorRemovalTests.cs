using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    /// Removing the previous push SDK, on fixture manifest.json files and a temp project.
    public class OpenPushVendorRemovalTests
    {
        private static readonly string V = IosExportFixture.Vendor;
        private static readonly string v = IosExportFixture.VendorLower;
        private string _root;

        private string Manifest => "{\n  \"dependencies\": {\n    \"com.unity.ugui\": \"1.0.0\",\n"
            + "    \"com." + v + ".unity.core\": \"5.3.3\",\n    \"com." + v + ".unity.ios\": \"5.3.3\",\n    \"com." + v + ".unity.android\": \"5.3.3\",\n"
            + "    \"com.google.external-dependency-manager\": \"1.2.189\"\n  },\n"
            + "  \"scopedRegistries\": [\n"
            + "    { \"name\": \"npmjs\", \"url\": \"https://registry.npmjs.org\", \"scopes\": [\"com." + v + "\"] },\n"
            + "    { \"name\": \"package.openupm.com\", \"url\": \"https://package.openupm.com\", \"scopes\": [\"com.google.external-dependency-manager\", \"com." + v + "\"] }\n"
            + "  ]\n}\n";

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "op-removal-" + Path.GetRandomFileName());
            Write("Packages/manifest.json", Manifest);
            Write("Assets/" + V + "/VERSION", "5.3.3");
            Write("Assets/" + V + ".meta", "meta");
            Write("Assets/" + V + "/Editor/" + V + "AndroidDependencies.xml", "<dependencies/>");
            Write("Assets/Plugins/Editor/" + V + "iOSDependencies.xml", "<dependencies/>");
            Write("ProjectSettings/" + V + "Settings.json", "{}");
            Write("ProjectSettings/GoogleDependency" + V + ".xml", "<x/>");
            Write("Assets/Plugins/Android/" + V + "Config.androidlib/res/drawable-mdpi/ic_stat_" + v + "_default.png", "png");
            Write("Assets/Game/Push.cs", "using " + V + "SDK;\nclass Push { void A() { " + V + ".Login(\"x\"); } }\n");
            Write("Assets/Game/Game.asmdef", "{ \"name\": \"Game\", \"references\": [\"GUID:abc\"] }");
            Write("Assets/Other/Plain.cs", "class Plain {}");
            Write("Assets/Samples/OpenPush/1.2.2/Feature Lab/Lab.cs", "// " + V + "SDK sample text\n");
            Write("Assets/Ext/Hooks.cs", "using " + V + "SDK;\n");
            Write("Assets/Ext/Ext.asmref", "{ \"reference\": \"GUID:abc\" }");
        }

        [TearDown]
        public void TearDown() => Directory.Delete(_root, true);

        private void Write(string relative, string text)
        {
            var path = Path.Combine(_root, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text);
        }

        private static IOpenPushVendorRemoval Removal => OpenPushVendorRemovals.All.Single();

        [Test]
        public void ThePlanListsEveryPackagePathAndScriptAndKeepsReusedIcons()
        {
            var plan = Removal.Plan(_root, reuseIcons: true);
            CollectionAssert.AreEquivalent(new[] { "com." + v + ".unity.core", "com." + v + ".unity.ios", "com." + v + ".unity.android" }, plan.Packages);
            CollectionAssert.AreEqual(new[] { "com." + v }, plan.Scopes);
            CollectionAssert.AreEqual(new[]
            {
                "Assets/Plugins/Editor/" + V + "iOSDependencies.xml", "Assets/" + V,
                "ProjectSettings/GoogleDependency" + V + ".xml", "ProjectSettings/" + V + "Settings.json",
            }.OrderBy(p => p, System.StringComparer.Ordinal), plan.Paths, "the dependency file inside Assets/<SDK> goes with its folder");
            StringAssert.StartsWith("Assets/Plugins/Android/" + V + "Config.androidlib", plan.Kept.Single());
            CollectionAssert.AreEqual(new[] { "Assets/Ext/Hooks.cs", "Assets/Game/Push.cs" }, plan.Scripts, "OpenPush's own sample is not game code");
            CollectionAssert.AreEquivalent(new[] { "Assets/Game/Game.asmdef", "Assets/Ext/Ext.asmref" }, plan.Asmdefs);

            var replaced = Removal.Plan(_root, reuseIcons: false);
            CollectionAssert.Contains(replaced.Paths, "Assets/Plugins/Android/" + V + "Config.androidlib");
            Assert.IsEmpty(replaced.Kept);
        }

        [Test]
        public void ApplyEditsTheManifestAndDeletesEachPathWithItsMeta()
        {
            var plan = Removal.Plan(_root, reuseIcons: false);
            var done = OpenPushVendorRemover.Apply(_root, plan, p => OpenPushVendorRemover.DeleteFromDisk(_root, p));

            Assert.AreEqual(plan.Paths.Count + 1, done.Count);
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "Assets/" + V)));
            Assert.IsFalse(File.Exists(Path.Combine(_root, "Assets/" + V + ".meta")));
            Assert.IsFalse(Directory.Exists(Path.Combine(_root, "Assets/Plugins/Android/" + V + "Config.androidlib")));
            Assert.IsTrue(File.Exists(Path.Combine(_root, "Assets/Game/Push.cs")), "game code is never edited");
            var manifest = File.ReadAllText(Path.Combine(_root, "Packages/manifest.json"));
            StringAssert.DoesNotContain(v, manifest.ToLowerInvariant());
            StringAssert.Contains("com.unity.ugui", manifest);
            StringAssert.Contains("https://package.openupm.com", manifest, "a registry with other scopes stays");
            StringAssert.DoesNotContain("registry.npmjs.org", manifest, "the registry the removal emptied goes");
            Assert.IsTrue(Removal.Plan(_root, false).Empty);
            Assert.IsEmpty(Removal.Plan(_root, false).Scripts, "game code is only read while the SDK is installed");
        }

        [Test]
        public void AnUnparseableManifestStopsTheRemovalBeforeAnythingIsDeleted()
        {
            Write("Packages/manifest.json", "{ \"dependencies\": { \"com." + v + ".unity.core\": ");
            var plan = Removal.Plan(_root, false);
            Assert.Throws<System.InvalidOperationException>(() => OpenPushVendorRemover.Apply(_root, plan, p => OpenPushVendorRemover.DeleteFromDisk(_root, p)));
            Assert.IsTrue(Directory.Exists(Path.Combine(_root, "Assets/" + V)));
            Assert.IsFalse(OpenPushVendorRemover.TryRemovePackages("[1]", new string[0], new string[0], out _));
        }

        [Test]
        public void TheCompatAssemblyIsReferencedOnceInTheAsmdefsOwnStyle()
        {
            Assert.IsTrue(OpenPushVendorRemover.TryAddReference("{ \"name\": \"Game\", \"references\": [\"GUID:abc\"] }",
                Removal.CompatAssembly, "GUID:compat", out var updated));
            StringAssert.Contains("\"GUID:compat\"", updated);
            Assert.IsTrue(OpenPushVendorRemover.TryAddReference(updated, Removal.CompatAssembly, "GUID:compat", out var again));
            Assert.AreEqual(updated, again);
            Assert.IsTrue(OpenPushVendorRemover.TryAddReference("{ \"name\": \"Game\" }", Removal.CompatAssembly, null, out var byName));
            StringAssert.Contains("\"" + Removal.CompatAssembly + "\"", byName);
            Assert.IsFalse(OpenPushVendorRemover.TryAddReference("{ broken", "x", null, out _));
        }

        [Test]
        public void ReusedIconsReachTheManifestOnlyInAutomaticDelivery()
        {
            var input = new OpenPushAndroidManifestInput { Delivery = OpenPushAndroidPushDelivery.Automatic, NotificationIcon = "ic_stat_game" };
            StringAssert.Contains("<meta-data android:name=\"ai.openpush.notification_icon\" android:resource=\"@drawable/ic_stat_game\" />",
                                  OpenPushAndroidManifestBuilder.Build(input));
            input.NotificationIcon = "Not A Name\"";
            StringAssert.DoesNotContain("notification_icon", OpenPushAndroidManifestBuilder.Build(input));
            input.NotificationIcon = "ic_stat_game";
            input.Delivery = OpenPushAndroidPushDelivery.Forward;
            StringAssert.DoesNotContain("notification_icon", OpenPushAndroidManifestBuilder.Build(input));

            var report = OpenPushNotificationIcons.Inspect(OpenPushAndroidPushDelivery.Automatic,
                new[] { "Assets/Plugins/Android/Game.androidlib/res/drawable-mdpi/ic_stat_game.png" }, "ic_stat_game");
            Assert.AreEqual(OpenPushIconSource.Reused, report.Small);
            Assert.IsTrue(report.Passes);
        }

        [Test]
        public void ThePodfileLinesForTheTakenOverTargetArePrinted()
        {
            var podfile = "platform :ios, '13.0'\ntarget 'UnityFramework' do\n  pod 'Firebase/Core'\nend\n"
                        + "target '" + V + "NotificationServiceExtension' do\n  pod '" + V + "XCFramework/" + V + "Extension', '5.5.6'\nend\n";
            var block = OpenPushExistingExtensionFiles.PodfileTargetBlock(podfile, V + "NotificationServiceExtension");
            Assert.AreEqual("target '" + V + "NotificationServiceExtension' do\n  pod '" + V + "XCFramework/" + V + "Extension', '5.5.6'\nend", block);
            Assert.IsNull(OpenPushExistingExtensionFiles.PodfileTargetBlock(podfile, "Other"));
        }
    }
}
