using System;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushConfigurationTests
    {
        [Test]
        public void TrimsTrailingSlashesFromServerUrl()
        {
            var config = new OpenPushConfiguration("app-1", "key-1", "https://push.example///");
            Assert.AreEqual("https://push.example", config.ServerURL);
        }

        [Test]
        public void UrlAppendsPathToTrimmedServer()
        {
            var config = new OpenPushConfiguration("app-1", "key-1", "https://push.example/");
            Assert.AreEqual("https://push.example/v1/ingest", config.Url("/v1/ingest"));
        }

        [Test]
        public void TokenKeyMatchesSiblingSdks()
        {
            var config = new OpenPushConfiguration("app-1", "key-1", "https://push.example");
            Assert.AreEqual("openpush.deviceToken", config.TokenKey);
        }

        [Test]
        public void PlatformDefaultsToRuntimeResolution()
        {
            var config = new OpenPushConfiguration("app-1", "key-1", "https://push.example");
            Assert.IsNull(config.Platform);
        }

        [Test]
        public void KeepsExplicitPlatformAndTokenKey()
        {
            var config = new OpenPushConfiguration("app-1", "key-1", "https://push.example",
                                                   platform: "android", tokenKey: "custom.key");
            Assert.AreEqual("android", config.Platform);
            Assert.AreEqual("custom.key", config.TokenKey);
        }

        [Test]
        public void AnAppAndKeyAloneUseTheHostedServer()
        {
            var config = new OpenPushConfiguration("app-1", "key-1");
            Assert.AreEqual("https://app.openpush.ai", config.ServerURL);
            Assert.AreEqual("app-1", config.AppID);
            Assert.AreEqual("key-1", config.SdkKey);
        }

        [Test]
        public void TheDefaultServerUrlIsTheHostedServer()
        {
            Assert.AreEqual("https://app.openpush.ai", OpenPushConfiguration.DefaultServerUrl);
        }

        [Test]
        public void ThrowsWhenServerUrlIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenPushConfiguration("app-1", "key-1", null));
        }

        [Test]
        public void ThrowsWhenAppIdIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenPushConfiguration(null, "key-1"));
        }

        [Test]
        public void ThrowsWhenSdkKeyIsNull()
        {
            Assert.Throws<ArgumentNullException>(
                () => new OpenPushConfiguration("app-1", null));
        }
    }
}
