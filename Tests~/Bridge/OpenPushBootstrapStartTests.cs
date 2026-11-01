using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// OpenPushBootstrap.Start delegates to StartFromBootstrap; a refused Initialize must not throw.
    public class OpenPushBootstrapStartTests : FacadeTestBase
    {
        [Test]
        public void ARefusedInitializeSkipsThePromptInsteadOfThrowing()
        {
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = null };
            var started = true;
            Assert.DoesNotThrow(() => started = OpenPush.StartFromBootstrap("app-1", requestPermission: true));
            Assert.IsFalse(started);
            CollectionAssert.DoesNotContain(Platform.Calls, "RequestPermission False");
        }

        [Test]
        public void AnInitializedStartRequestsPermissionWhenAsked()
        {
            Assert.IsTrue(OpenPush.StartFromBootstrap("app-1", requestPermission: true));
            CollectionAssert.Contains(Platform.Calls, "Initialize app-1");
            CollectionAssert.Contains(Platform.Calls, "RequestPermission False");
        }

        [Test]
        public void NoPromptUnlessAsked()
        {
            Assert.IsTrue(OpenPush.StartFromBootstrap("app-1", requestPermission: false));
            CollectionAssert.DoesNotContain(Platform.Calls, "RequestPermission False");
        }
    }
}
