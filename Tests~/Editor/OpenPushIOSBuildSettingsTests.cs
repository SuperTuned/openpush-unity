using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;
using UnityEditor.iOS.Xcode;

namespace OpenPushSDK.Tests
{
    public class OpenPushIOSBuildSettingsTests
    {
        [Test]
        public void AppliesSpecSettingsToBothTargets()
        {
            PBXProject.Recorded.Clear();
            var project = new PBXProject();
            OpenPushIOSBuildSettings.Apply(project, "framework", "main");
            CollectionAssert.IsSubsetOf(new[]
            {
                "framework:IPHONEOS_DEPLOYMENT_TARGET=16.0", "main:IPHONEOS_DEPLOYMENT_TARGET=16.0",
                "framework:SWIFT_VERSION=5.0", "framework:DEFINES_MODULE=YES", "framework:CLANG_ENABLE_MODULES=YES",
                "main:ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES=YES", "framework:ALWAYS_EMBED_SWIFT_STANDARD_LIBRARIES=NO",
                "framework:link WebKit.framework weak=False", "framework:link SafariServices.framework weak=False",
                "framework:link UserNotifications.framework weak=False",
                "framework:link ActivityKit.framework weak=True", "framework:link WidgetKit.framework weak=True",
            }, PBXProject.Recorded);
            CollectionAssert.DoesNotContain(PBXProject.Recorded, "framework:SWIFT_OBJC_INTERFACE_HEADER_NAME=UnityFramework-Swift.h");
            // The app target's versions are Unity's and the host's, never rewritten here.
            Assert.IsFalse(PBXProject.Recorded.Any(r => r.StartsWith("main:CURRENT_PROJECT_VERSION") || r.StartsWith("main:MARKETING_VERSION")));
        }

        [Test]
        public void DeploymentTargetIsOnlyRaised()
        {
            PBXProject.Recorded.Clear();
            var project = new PBXProject();
            project.SetBuildProperty("main", "IPHONEOS_DEPLOYMENT_TARGET", "17.2");
            project.SetBuildProperty("framework", "IPHONEOS_DEPLOYMENT_TARGET", "15.0");
            OpenPushIOSBuildSettings.Apply(project, "framework", "main");
            Assert.AreEqual("17.2", project.GetBuildPropertyForAnyConfig("main", "IPHONEOS_DEPLOYMENT_TARGET"));
            Assert.AreEqual("16.0", project.GetBuildPropertyForAnyConfig("framework", "IPHONEOS_DEPLOYMENT_TARGET"));
        }

        [TestCase("", "16.1", "16.1")]
        [TestCase("16", "16.1", "16.1")]
        [TestCase("16.4", "16.1", "16.4")]
        [TestCase("18.0", "16.0", "18.0")]
        [TestCase("garbage", "16.0", "16.0")]
        public void RaiseDeploymentTarget(string current, string minimum, string expected)
        {
            var project = new PBXProject();
            if (current.Length > 0) project.SetBuildProperty("t", "IPHONEOS_DEPLOYMENT_TARGET", current);
            OpenPushIOSBuildSettings.RaiseDeploymentTarget(project, "t", minimum);
            Assert.AreEqual(expected, project.GetBuildPropertyForAnyConfig("t", "IPHONEOS_DEPLOYMENT_TARGET"));
        }

        [Test]
        public void SuppressingLaunchUrlsWritesTheInfoPlistKeyAndOpeningThemRemovesIt()
        {
            var root = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "openpush-plist-" + System.IO.Path.GetRandomFileName());
            System.IO.Directory.CreateDirectory(root);
            var plist = System.IO.Path.Combine(root, "Info.plist");
            System.IO.File.WriteAllText(plist,
                "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                "<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                "<plist version=\"1.0\"><dict>" +
                "<key>CFBundleName</key><string>Game</string></dict></plist>");
            try
            {
                OpenPushIOSBuildSettings.ApplyLaunchUrlSetting(root, openLaunchUrls: false);
                var suppressed = System.IO.File.ReadAllText(plist);
                StringAssert.Contains("<key>OpenPushSuppressLaunchURLs</key>", suppressed);
                StringAssert.Contains("<true />", suppressed.Replace("<true/>", "<true />"));
                StringAssert.Contains("<key>CFBundleName</key>", suppressed);
                // An empty internal subset ("dtd"[]>) is valid XML but Xcode's plist parser rejects it.
                StringAssert.DoesNotContain("[]", suppressed);
                AssertPlistLints(plist);

                OpenPushIOSBuildSettings.ApplyLaunchUrlSetting(root, openLaunchUrls: true);
                StringAssert.DoesNotContain("OpenPushSuppressLaunchURLs", System.IO.File.ReadAllText(plist));
                StringAssert.DoesNotContain("[]", System.IO.File.ReadAllText(plist));
                AssertPlistLints(plist);
            }
            finally { System.IO.Directory.Delete(root, true); }
        }

        /// <summary>Runs Apple's own parser where it exists (macOS); elsewhere the string checks stand.</summary>
        private static void AssertPlistLints(string path)
        {
            if (!System.IO.File.Exists("/usr/bin/plutil")) return;
            using var lint = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("/usr/bin/plutil", "-lint \"" + path + "\"")
            {
                RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false,
            });
            var output = lint.StandardOutput.ReadToEnd() + lint.StandardError.ReadToEnd();
            lint.WaitForExit();
            Assert.AreEqual(0, lint.ExitCode, output);
        }

        [Test]
        public void RunsAtOrder45() => Assert.AreEqual(45, new OpenPushIOSBuildSettings().callbackOrder);
    }
}
