using System;
using System.IO;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushVersionBridgeTests
    {
        private static string Manifest() => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "package.json"));

        [Test]
        public void PackageManifestAndSdkConstantAgreeOnTheVersion()
        {
            Assert.AreEqual("1.2.2", OpenPushVersion.Current);
            Assert.AreEqual(OpenPushVersion.Current, OpenPushJson.TryGetStringField(Manifest(), "version"));
        }

        [Test]
        public void UnityFloorIs20223()
            => Assert.AreEqual("2022.3", OpenPushJson.TryGetStringField(Manifest(), "unity"));

        [Test]
        public void InitOptionsCarryTheWrapperString()
        {
            Assert.AreEqual("unity/1.2.2", OpenPushVersion.Wrapper);
            Assert.AreEqual("unity/1.2.2", new OpenPushInitOptions().Wrapper);
        }

        [Test]
        public void FakePlatformImplementsTheWholeContract()
        {
            IOpenPushPlatform platform = new FakePlatform();
            Assert.NotNull(platform.Core); Assert.NotNull(platform.User); Assert.NotNull(platform.PushSubscription);
            Assert.NotNull(platform.Notifications); Assert.NotNull(platform.InAppMessages);
            Assert.NotNull(platform.LiveActivities); Assert.NotNull(platform.LiveUpdates);
        }
    }
}
