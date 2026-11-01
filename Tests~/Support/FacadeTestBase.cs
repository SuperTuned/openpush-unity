using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public abstract class FacadeTestBase
    {
        internal FakePlatform Platform;
        internal QueueDispatcher Main;

        [SetUp]
        public void SetUpFacade()
        {
            OpenPush.ResetForTests();
            Platform = new FakePlatform();
            OpenPushPlatformRegistry.Set(Platform);
            Main = new QueueDispatcher();
            OpenPushHost.Dispatcher = Main;
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk_asset" };
        }

        [TearDown]
        public void TearDownFacade()
        {
            OpenPush.ResetForTests();
            OpenPushPlatformRegistry.ResetForTests();
        }

        internal void InitializeAndPump() { OpenPush.Initialize("app-1"); Main.Pump(); }
    }
}
