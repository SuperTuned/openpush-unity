using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// Email addresses: trimmed and lower-cased before the platform sees them, refused (false, one log
    /// line without the address, no platform call) otherwise.
    public class OpenPushFacadeEmailTests : FacadeTestBase
    {
        [Test]
        public void BothWritesReachThePlatformCleaned()
        {
            InitializeAndPump();
            Assert.IsTrue(OpenPush.User.AddEmail("  Player@Example.COM "));
            Assert.IsTrue(OpenPush.User.RemoveEmail("PLAYER@example.com"));
            CollectionAssert.AreEqual(new[]
            {
                "Initialize app-1", "AddEmail player@example.com", "RemoveEmail player@example.com",
            }, Platform.Calls);
        }

        [Test]
        public void AnInvalidAddressIsRefusedLocallyAndSaysWhy()
        {
            var lines = new List<string>();
            OpenPush.OnLog = lines.Add;
            InitializeAndPump();
            Assert.IsFalse(OpenPush.User.AddEmail("secret-person.example.com"));
            Assert.IsFalse(OpenPush.User.RemoveEmail(null));
            Assert.IsFalse(OpenPush.User.AddEmail("a@localhost"));
            Assert.IsFalse(OpenPush.User.AddEmail(new string('a', 250) + "@b.co"));
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "Initialize app-1" }, Platform.Calls);
            var refusals = lines.FindAll(l => l.Contains("not an email address"));
            Assert.AreEqual(4, refusals.Count);
            StringAssert.Contains("User.AddEmail ignored", refusals[0]);
            Assert.IsFalse(refusals.Exists(l => l.Contains("secret")), "a refusal must not echo the address");
        }

        [Test]
        public void EmailWritesRequireInitialize()
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => OpenPush.User.AddEmail("a@b.co"));
            StringAssert.Contains("OpenPush.User.AddEmail was called before OpenPush.Initialize", thrown.Message);
            Assert.Throws<InvalidOperationException>(() => OpenPush.User.RemoveEmail("a@b.co"));
            Assert.IsEmpty(Platform.Calls);
        }
    }
}
