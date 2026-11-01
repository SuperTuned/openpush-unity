using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushPlatformRegistryTests
    {
        [TearDown] public void TearDown() => OpenPushPlatformRegistry.ResetForTests();

        [Test]
        public void FallsBackToOneStubWhenNothingRegistered()
        {
            OpenPushPlatformRegistry.ResetForTests();
            var first = OpenPushPlatformRegistry.Current;
            Assert.AreEqual("stub", first.Name);
            Assert.AreSame(first, OpenPushPlatformRegistry.Current);
        }

        [Test]
        public void ARegisteredPlatformWins()
        {
            var fake = new FakePlatform();
            OpenPushPlatformRegistry.Set(fake);
            Assert.AreSame(fake, OpenPushPlatformRegistry.Current);
        }
    }
}
