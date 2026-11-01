using System.Linq;
using NUnit.Framework;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushSharePatcherTests
    {
        private const string WithExpiry =
            "import UserNotifications\n" +
            "\n" +
            "class NotificationService: UNNotificationServiceExtension {\n" +
            "    var openPush = 1\n" +
            "    override func didReceive(_ req: UNNotificationRequest, withContentHandler done: @escaping (UNNotificationContent) -> Void) {\n" +
            "        done(req.content)\n" +
            "    }\n" +
            "\n" +
            "    override func serviceExtensionTimeWillExpire() {\n" +
            "        // nothing\n" +
            "    }\n" +
            "}\n";

        [Test]
        public void UsesTheMethodsOwnParameterNamesAndAvoidsAnExistingOpenPushName()
        {
            var result = new OpenPushSharePatcher("NotificationService", "group.a").Patch(WithExpiry);
            Assert.AreEqual(OpenPushSharePatcher.Outcome.Patched, result.Outcome);
            StringAssert.Contains("        if openPushHandler.handleIfOpenPush(req, withContentHandler: done) { return }\n        done(req.content)", result.Text);
            StringAssert.Contains("    override func serviceExtensionTimeWillExpire() {\n        if openPushHandler.serviceExtensionTimeWillExpireIfHandling() { return }\n        // nothing", result.Text);
            StringAssert.Contains("import UserNotifications\nimport OpenPushNSE\n", result.Text);
            StringAssert.Contains("    private let openPushHandler = OpenPushNotificationServiceHandler(options: {", result.Text);
            Assert.AreEqual(1, result.Text.Split('\n').Count(l => l.Contains("func serviceExtensionTimeWillExpire")), "no second override");
            var lines = result.Text.Split('\n');
            CollectionAssert.AreEqual(WithExpiry.Split('\n'), lines.Where((_, i) => !result.InsertedLines.Contains(i)));
        }

        [Test]
        public void AFileAlreadyUsingOpenPushIsLeftAlone()
        {
            var shared = new OpenPushSharePatcher("NotificationService", "group.a").Patch(WithExpiry).Text;
            var again = new OpenPushSharePatcher("NotificationService", "group.a").Patch(shared);
            Assert.AreEqual(OpenPushSharePatcher.Outcome.AlreadyShared, again.Outcome);
            Assert.AreEqual(shared, again.Text);
        }

        [Test]
        public void AMissingClassOrMethodChangesNothing()
        {
            var noClass = new OpenPushSharePatcher("Other", "group.a").Patch(WithExpiry);
            Assert.AreEqual(OpenPushSharePatcher.Outcome.AnchorsMissing, noClass.Outcome);
            var noReceive = new OpenPushSharePatcher("NotificationService", "group.a")
                .Patch("import UserNotifications\nclass NotificationService: UNNotificationServiceExtension {\n}\n");
            Assert.AreEqual(OpenPushSharePatcher.Outcome.AnchorsMissing, noReceive.Outcome);
            StringAssert.Contains("didReceive", noReceive.Missing.Single());
        }

        [Test]
        public void CrlfFilesStayCrlfAndTheGroupIsAnEscapedSwiftString()
        {
            var result = new OpenPushSharePatcher("NotificationService", "group.\"x\"").Patch(WithExpiry.Replace("\n", "\r\n"));
            Assert.IsFalse(result.Text.Replace("\r\n", "").Contains("\n"));
            StringAssert.Contains("options.appGroup = \"group.\\\"x\\\"\"", result.Text);
        }
    }
}

namespace OpenPushSDK.Tests
{
    /// The Swift that TakeOver and Share write, pinned to files Tests~/Native/verify-swift-sources-compile.sh
    /// type-checks against the vendored OpenPushNSE. Set OPENPUSH_UPDATE_GOLDEN=1 to rewrite them.
    public class OpenPushExtensionSwiftGoldenTests
    {
        private static string Golden(string name)
            => System.IO.Path.Combine(IosExportFixture.PackageRoot(), "Tests~", "Fixtures", "ios", "expected", name);

        private static void Check(string name, string actual)
        {
            var path = Golden(name);
            if (System.Environment.GetEnvironmentVariable("OPENPUSH_UPDATE_GOLDEN") == "1")
            {
                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
                System.IO.File.WriteAllText(path, actual);
            }
            Assert.AreEqual(System.IO.File.ReadAllText(path), actual, name);
        }

        [Test]
        public void TakeOver() => Check("TakeOverNotificationService.swift",
            OpenPushExistingExtensionFiles.TakeOverSource("NotificationService", "group.com.example.game.shared", objcName: false));

        [Test]
        public void TakeOverOfAnUnprefixedPrincipalClass() => Check("TakeOverObjcNamed.swift",
            OpenPushExistingExtensionFiles.TakeOverSource("NotificationService", "group.com.example.game.shared", objcName: true));

        [Test]
        public void Share()
        {
            var source = System.IO.File.ReadAllText(System.IO.Path.Combine(IosExportFixture.PackageRoot(), "Tests~", "Fixtures", "ios",
                                                                           "CustomExtension", "RichPush", "RichPushService.swift"));
            Check("SharedRichPushService.swift", new OpenPushSharePatcher("RichPushService", "group.com.example.game.shared").Patch(source).Text);
        }
    }
}
