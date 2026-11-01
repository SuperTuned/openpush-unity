using System.Collections.Generic;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    public class OpenPushDemoFixtureTests
    {
        [Test]
        public void FixtureParsesIntoBlocksTheEditorRendererReads()
        {
            var message = OpenPushDemoInAppFixture.Message();
            Assert.AreEqual("demo-fixture", message.Id);
            Assert.AreEqual("center", message.Layout);
            var blocks = (Dictionary<string, object>)message.Blocks;
            Assert.IsTrue(blocks.ContainsKey("cards"));
            Assert.IsTrue(blocks.ContainsKey("background"));
        }
    }
}
