using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// The server's normalise_e164, as both native SDKs apply it: keep only ASCII digits and +, read a
    /// leading 00 as +, then + and 8–15 digits whose first is not 0.
    public class OpenPushPhoneNumberTests
    {
        [TestCase("+14155550123", "+14155550123")]
        [TestCase("  +14155550123\n", "+14155550123")]
        [TestCase("004915123456789", "+4915123456789")]
        [TestCase("+12345678", "+12345678")]
        [TestCase("+123456789012345", "+123456789012345")]
        [TestCase("+1 (415) 555-0123", "+14155550123")]
        [TestCase("+1-415-555-0123", "+14155550123")]
        [TestCase("0014155550123", "+14155550123")]
        [TestCase("00 44 7700 900123", "+447700900123")]
        [TestCase("+1.415.555.0123 ext", "+14155550123")]
        public void AcceptsE164AndNormalizes(string raw, string expected)
            => Assert.AreEqual(expected, OpenPushPhoneNumber.Normalize(raw));

        [TestCase(null)]
        [TestCase("")]
        [TestCase("14155550123")]
        [TestCase("415")]
        [TestCase("+0123456789")]
        [TestCase("000123456789")]
        [TestCase("+1234567")]
        [TestCase("+1234567890123456")]
        [TestCase("++14155550123")]
        [TestCase("+1415+5550123")]
        [TestCase("+١٤١٥٥٥٥٠١٢٣")]
        [TestCase("+")]
        public void RefusesAnythingElse(string raw) => Assert.IsNull(OpenPushPhoneNumber.Normalize(raw));
    }
}
