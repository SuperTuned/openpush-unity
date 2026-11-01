using System;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// Initialize trims trailing slashes from the server url and refuses a null key, as OpenPushConfiguration does.
    public class OpenPushInitializeArgumentTests : FacadeTestBase
    {
        [Test]
        public void TrailingSlashesAreTrimmedFromTheServerUrl()
        {
            OpenPush.Initialize("app-1", "pk", "https://self.example///");
            Assert.AreEqual("https://self.example", Platform.CoreFake.InitOptions.ServerUrl);
        }

        [Test]
        public void ANullKeyThrowsBeforeThePlatformIsTouched()
        {
            Assert.Throws<ArgumentNullException>(() => OpenPush.Initialize("app-1", null));
            Assert.IsEmpty(Platform.Calls);
            Assert.IsFalse(OpenPush.IsInitialized);
        }

        [TestCase("")]
        [TestCase("   ")]
        public void ABlankKeyThrowsBeforeThePlatformIsTouched(string key)
        {
            var e = Assert.Throws<ArgumentException>(() => OpenPush.Initialize("app-1", key));
            Assert.AreEqual("sdkKey", e.ParamName);
            Assert.IsEmpty(Platform.Calls);
        }
    }
}
