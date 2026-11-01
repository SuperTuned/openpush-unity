using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// SMS and WhatsApp numbers: normalized to E.164 before the platform sees them, refused (false,
    /// one log line without the number, no platform call) otherwise.
    public class OpenPushFacadePhoneTests : FacadeTestBase
    {
        [Test]
        public void TheFourWritesReachThePlatformNormalized()
        {
            InitializeAndPump();
            Assert.IsTrue(OpenPush.User.AddSms(" +14155550123 "));
            Assert.IsTrue(OpenPush.User.RemoveSms("001 (415) 555-0123"));
            Assert.IsTrue(OpenPush.User.AddWhatsApp("+44 7700 900123"));
            Assert.IsTrue(OpenPush.User.RemoveWhatsApp("00447700900123"));
            CollectionAssert.AreEqual(new[]
            {
                "Initialize app-1", "AddSms +14155550123", "RemoveSms +14155550123",
                "AddWhatsApp +447700900123", "RemoveWhatsApp +447700900123",
            }, Platform.Calls);
        }

        [Test]
        public void ANumberThatIsNotE164IsRefusedLocallyAndSaysWhy()
        {
            var lines = new List<string>();
            OpenPush.OnLog = lines.Add;
            InitializeAndPump();
            Assert.IsFalse(OpenPush.User.AddSms("987 654 3210"));
            Assert.IsFalse(OpenPush.User.RemoveSms(null));
            Assert.IsFalse(OpenPush.User.AddWhatsApp("+1234567"));
            Assert.IsFalse(OpenPush.User.RemoveWhatsApp("+0987654321"));
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "Initialize app-1" }, Platform.Calls);
            var refusals = lines.FindAll(l => l.Contains("not an E.164 phone number"));
            Assert.AreEqual(4, refusals.Count);
            StringAssert.Contains("User.AddSms ignored", refusals[0]);
            Assert.IsFalse(refusals.Exists(l => l.Contains("654")), "a refusal must not echo the number");
        }

        [Test]
        public void PhoneWritesRequireInitialize()
        {
            var thrown = Assert.Throws<InvalidOperationException>(() => OpenPush.User.AddSms("+14155550123"));
            StringAssert.Contains("OpenPush.User.AddSms was called before OpenPush.Initialize", thrown.Message);
            Assert.Throws<InvalidOperationException>(() => OpenPush.User.AddWhatsApp("+14155550123"));
            Assert.IsEmpty(Platform.Calls);
        }
    }
}
