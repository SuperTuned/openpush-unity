using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushSettingsSnapshotTests
    {
        [Test]
        public void AppGroupFollowsTheInjectorRule()
        {
            Assert.AreEqual("group.com.acme.game.openpush",
                OpenPushSettingsSnapshotReader.From("pk", "app", null, true, "com.acme.game", OpenPushAndroidPushDelivery.Automatic, true).AppGroup);
            Assert.AreEqual("group.custom",
                OpenPushSettingsSnapshotReader.From("pk", "app", "  group.custom ", true, "com.acme.game", OpenPushAndroidPushDelivery.Automatic, true).AppGroup);
            Assert.IsNull(OpenPushSettingsSnapshotReader.From("pk", "app", "group.custom", false, "com.acme.game", OpenPushAndroidPushDelivery.Automatic, true).AppGroup);
        }

        [Test]
        public void CarriesDeliveryModeAndReloginFlag()
        {
            var s = OpenPushSettingsSnapshotReader.From("pk", "app", null, false, "b", OpenPushAndroidPushDelivery.HostOwned, false);
            Assert.AreEqual(OpenPushAndroidPushDelivery.HostOwned, s.AndroidPushDelivery);
            Assert.IsFalse(s.ReloginWithoutAuthHash);
            Assert.AreEqual("pk", s.SdkKey);
        }
    }
}
