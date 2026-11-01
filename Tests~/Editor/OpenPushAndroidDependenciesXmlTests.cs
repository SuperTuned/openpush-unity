using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Editor;

namespace OpenPushSDK.Tests
{
    public class OpenPushAndroidDependenciesXmlTests
    {
        private const string Expected =
            "<dependencies>\n" +
            "  <androidPackages>\n" +
            "    <repositories>\n" +
            "      <repository>Packages/ai.openpush.unity/Runtime/Plugins/Android/maven~</repository>\n" +
            "      <repository>https://repo.maven.apache.org/maven2</repository>\n" +
            "      <repository>https://maven.google.com</repository>\n" +
            "    </repositories>\n" +
            "    <androidPackage spec=\"ai.openpush:openpush-android:1.2.2\" />\n" +
            "    <androidPackage spec=\"ai.openpush:openpush-android-in-app-messages:1.2.2\" />\n" +
            "    <androidPackage spec=\"ai.openpush:openpush-android-live-updates:1.2.2\" />\n" +
            "    <androidPackage spec=\"ai.openpush:openpush-android-fcm:1.2.2\" />\n" +
            "  </androidPackages>\n" +
            "</dependencies>\n";

        [Test]
        public void AutomaticModeMatchesTheSpikeVerifiedXml()
            => Assert.AreEqual(Expected, OpenPushAndroidDependenciesXml.Build(OpenPushAndroidPushDelivery.Automatic));

        [Test]
        public void HostOwnedModeOmitsOnlyTheFcmArtifact()
        {
            var xml = OpenPushAndroidDependenciesXml.Build(OpenPushAndroidPushDelivery.HostOwned);
            StringAssert.DoesNotContain("openpush-android-fcm", xml);
            Assert.AreEqual(Expected.Replace("    <androidPackage spec=\"ai.openpush:openpush-android-fcm:1.2.2\" />\n", ""), xml);
        }
    }
}
