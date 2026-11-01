using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// The email-address rule, as both native SDKs apply it: trimmed and lower-cased, then at most 254
    /// characters with one @, something before it and a dot in the domain.
    public class OpenPushEmailAddressTests
    {
        [TestCase("a@b.co", "a@b.co")]
        [TestCase("  Player.One@Example.COM \n", "player.one@example.com")]
        [TestCase("a+tag@mail.example.org", "a+tag@mail.example.org")]
        public void AcceptsAnAddressAndCleansIt(string raw, string expected)
            => Assert.AreEqual(expected, OpenPushEmailAddress.Normalize(raw));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("no-at-sign.example.com")]
        [TestCase("@example.com")]
        [TestCase("a@@example.com")]
        [TestCase("a@b@example.com")]
        [TestCase("a@localhost")]
        [TestCase("a@")]
        [TestCase("a.b@localhost")]
        public void RefusesAnythingElse(string raw) => Assert.IsNull(OpenPushEmailAddress.Normalize(raw));

        [Test]
        public void TheLengthLimitIs254Characters()
        {
            var domain = "@example.com";
            var atLimit = new string('a', 254 - domain.Length) + domain;
            Assert.AreEqual(254, atLimit.Length);
            Assert.AreEqual(atLimit, OpenPushEmailAddress.Normalize(atLimit));
            Assert.IsNull(OpenPushEmailAddress.Normalize("a" + atLimit));
        }
    }
}
