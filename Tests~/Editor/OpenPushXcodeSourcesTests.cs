using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    /// Unity turns an absolute AddFile path into a broken Xcode reference on Windows
    /// (OpenPushNotificationService.swift became OpenPushNotificationService.s), so injected sources
    /// are registered by their project-relative path, source-tree relative.
    public class OpenPushXcodeSourcesTests
    {
        [Test]
        public void RegistersTheProjectRelativePathAgainstTheSourceTree()
        {
            var project = new PBXProject();
            var guid = OpenPushXcodeSources.Add(project, "OpenPushNotificationService/OpenPushNotificationService.swift");

            CollectionAssert.AreEqual(new[]
            {
                "OpenPushNotificationService/OpenPushNotificationService.swift|"
                + "OpenPushNotificationService/OpenPushNotificationService.swift|Source",
            }, project.AddedFiles);
            Assert.AreEqual(guid, project.FindFileGuidByProjectPath("OpenPushNotificationService/OpenPushNotificationService.swift"));
        }

        [Test]
        public void AReExportReusesTheExistingReference()
        {
            var project = new PBXProject();
            var first = OpenPushXcodeSources.Add(project, "OpenPushLiveActivityWidget/OpenPushDefaultWidget.swift");
            var second = OpenPushXcodeSources.Add(project, "OpenPushLiveActivityWidget/OpenPushDefaultWidget.swift");

            Assert.AreEqual(first, second);
            Assert.AreEqual(1, project.AddedFiles.Count);
        }

        [Test]
        public void NoInjectorPassesAFilesystemPathToAddFile()
        {
            var dir = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json"))) dir = dir.Parent;
            Assert.IsNotNull(dir, "package root not found above the test directory");

            // Only these call AddFile, and each passes the project-relative path twice.
            var callers = Directory.GetFiles(Path.Combine(dir.FullName, "Editor"), "*.cs", SearchOption.AllDirectories)
                .Where(f => File.ReadAllText(f).Contains(".AddFile("))
                .Select(Path.GetFileName).OrderBy(n => n).ToArray();
            CollectionAssert.AreEqual(new[] { "OpenPushNativeFrameworks.cs", "OpenPushPrivacyManifest.cs", "OpenPushXcodeSources.cs" }, callers);
            foreach (var file in callers)
            {
                var text = File.ReadAllText(Directory.GetFiles(Path.Combine(dir.FullName, "Editor"), file, SearchOption.AllDirectories).Single());
                Assert.IsTrue(Regex.IsMatch(text, @"\.AddFile\((\w+), \1, PBXSourceTree\.Source\)"), file);
            }
        }
    }
}
