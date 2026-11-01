using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushLogClassifierTests
    {
        [TestCase("✗ register failed (-1)", OpenPushLogLevel.Error)]
        [TestCase("⚠ requestPermission() found no resumed Activity", OpenPushLogLevel.Warning)]
        [TestCase("✓ registered", OpenPushLogLevel.Info)]
        [TestCase("→ POST /v1/apps/x/subscriptions", OpenPushLogLevel.Info)]
        [TestCase("• notification_types -18 held", OpenPushLogLevel.Verbose)]
        [TestCase("· queued", OpenPushLogLevel.Verbose)]
        [TestCase("  ✗ leading spaces", OpenPushLogLevel.Error)]
        [TestCase("attach · server http://x · app a", OpenPushLogLevel.Info)]
        [TestCase("", OpenPushLogLevel.Info)]
        [TestCase(null, OpenPushLogLevel.Info)]
        public void ClassifiesByLeadingGlyph(string line, OpenPushLogLevel expected)
            => Assert.AreEqual(expected, OpenPushLogClassifier.Classify(line));

        [Test]
        public void FilterDeliversAtOrBelowTheConfiguredLevel()
        {
            Assert.IsTrue(OpenPushLogClassifier.Passes(OpenPushLogLevel.Error, OpenPushLogLevel.Warning));
            Assert.IsFalse(OpenPushLogClassifier.Passes(OpenPushLogLevel.Verbose, OpenPushLogLevel.Info));
            Assert.IsFalse(OpenPushLogClassifier.Passes(OpenPushLogLevel.Error, OpenPushLogLevel.None));
        }
    }
}
