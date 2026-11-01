using System.IO;
using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushNativeFrameworksTests
    {
        private string _root, _source;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "op-export-" + Path.GetRandomFileName());
            _source = Path.Combine(Path.GetTempPath(), "op-native-" + Path.GetRandomFileName());
            foreach (var m in new[] { "OpenPushCore", "OpenPushNSE", "OpenPush", "OpenPushLiveActivities" })
            {
                var dir = Path.Combine(_source, m + ".xcframework", "ios-arm64", m + ".framework");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, m), "binary");
                File.WriteAllText(Path.Combine(_source, m + ".xcframework", "Info.plist"), "<plist/>");
            }
            OpenPushNativeFrameworks.SourceOverride = _source;
        }

        [TearDown]
        public void TearDown()
        {
            OpenPushNativeFrameworks.SourceOverride = null;
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
            Directory.Delete(_source, true);
        }

        [Test]
        public void CopyIsIdempotentAndSkipsMetaFiles()
        {
            File.WriteAllText(Path.Combine(_source, "OpenPushCore.xcframework", "Info.plist.meta"), "meta");
            var first = OpenPushNativeFrameworks.CopyInto(_root, OpenPushNativeFrameworks.ServiceExtensionModules);
            var second = OpenPushNativeFrameworks.CopyInto(_root, OpenPushNativeFrameworks.ServiceExtensionModules);
            CollectionAssert.AreEqual(new[] { "Frameworks/OpenPushNative/OpenPushCore.xcframework", "Frameworks/OpenPushNative/OpenPushNSE.xcframework" }, first);
            CollectionAssert.AreEqual(first, second);
            Assert.IsTrue(File.Exists(Path.Combine(_root, "Frameworks/OpenPushNative/OpenPushNSE.xcframework/ios-arm64/OpenPushNSE.framework/OpenPushNSE")));
            Assert.IsFalse(Directory.GetFiles(_root, "*.meta", SearchOption.AllDirectories).Any());
        }

        [Test]
        public void ModuleSetsMatchTheSpec()
        {
            CollectionAssert.AreEqual(new[] { "OpenPushCore", "OpenPushLiveActivities", "OpenPush" }, OpenPushNativeFrameworks.UnityFrameworkModules);
            CollectionAssert.AreEqual(new[] { "OpenPushCore", "OpenPushNSE" }, OpenPushNativeFrameworks.ServiceExtensionModules);
            CollectionAssert.AreEqual(new[] { "OpenPushLiveActivities" }, OpenPushNativeFrameworks.WidgetModules);
        }

        [Test]
        public void LinkAddsEachFrameworkOnceAcrossReExports()
        {
            var project = new UnityEditor.iOS.Xcode.PBXProject();
            var paths = new[] { "Frameworks/OpenPushNative/OpenPushCore.xcframework" };
            OpenPushNativeFrameworks.Link(project, "ext", paths);
            OpenPushNativeFrameworks.Link(project, "ext", paths);
            Assert.AreEqual(1, project.BuildSectionFiles.Count(f => f.StartsWith("ext:")));
        }

        [Test]
        public void LinkNeedsNoMarkerBuildSetting()
        {
            // A re-export whose build settings a host tool rewrote still links each framework once:
            // the Frameworks phase itself is the record, not a custom setting.
            var project = new UnityEditor.iOS.Xcode.PBXProject();
            var paths = new[] { "Frameworks/OpenPushNative/OpenPushCore.xcframework" };
            OpenPushNativeFrameworks.Link(project, "ext", paths);
            project.SetBuildProperty("ext", "OPENPUSH_LINKED", "");
            OpenPushNativeFrameworks.Link(project, "ext", paths);
            Assert.AreEqual(1, project.BuildSectionFiles.Count(f => f.StartsWith("ext:")));
            Assert.AreEqual(string.Empty, project.GetBuildPropertyForAnyConfig("ext", "OPENPUSH_LINKED"));
        }
    }
}
