using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// The result payload both bridges send for a phone-code call, read into the typed result.
    public class OpenPushVerifyWireTests
    {
        private static OpenPushVerifyResult Read(string json) => OpenPushVerifyWire.Read(OpenPushWire.Parse(json));

        [Test]
        public void AVerificationReadsEveryField()
        {
            var result = Read("{\"verification\":{\"id\":\"vf_9\",\"masked_number\":\"+1415•••0123\",\"channel\":\"whatsapp\","
                            + "\"status\":\"max_attempts\",\"attempts\":5,\"expires_at\":1792737000000}}");
            Assert.IsNull(result.Error);
            var v = result.Verification;
            Assert.AreEqual("vf_9", v.Id);
            Assert.AreEqual("+1415•••0123", v.MaskedNumber);
            Assert.AreEqual(OpenPushVerifyChannel.WhatsApp, v.Channel);
            Assert.AreEqual(OpenPushVerificationStatus.MaxAttempts, v.Status);
            Assert.AreEqual(5, v.Attempts);
            Assert.AreEqual(DateTimeOffset.FromUnixTimeMilliseconds(1792737000000), v.ExpiresAt);
        }

        [Test]
        public void ANullExpiryAndAnUnknownChannelAreTolerated()
        {
            var v = Read("{\"verification\":{\"id\":\"vf_1\",\"masked_number\":\"x\",\"channel\":\"pigeon\",\"status\":\"pending\",\"expires_at\":null}}").Verification;
            Assert.IsNull(v.ExpiresAt);
            Assert.AreEqual(OpenPushVerifyChannel.Auto, v.Channel);
            Assert.AreEqual(0, v.Attempts);
        }

        [TestCase("rate_limited", OpenPushVerifyErrorCode.RateLimited)]
        [TestCase("too_soon", OpenPushVerifyErrorCode.TooSoon)]
        [TestCase("not_pending", OpenPushVerifyErrorCode.NotPending)]
        [TestCase("provider", OpenPushVerifyErrorCode.Provider)]
        [TestCase("refused", OpenPushVerifyErrorCode.Refused)]
        [TestCase("invalid_number", OpenPushVerifyErrorCode.InvalidNumber)]
        [TestCase("network", OpenPushVerifyErrorCode.Network)]
        [TestCase("opted_out", OpenPushVerifyErrorCode.OptedOut)]
        [TestCase("credit", OpenPushVerifyErrorCode.NoCredit)]
        [TestCase("no_rate", OpenPushVerifyErrorCode.NoRate)]
        [TestCase("something_newer", OpenPushVerifyErrorCode.Refused)]
        [TestCase("no_credit", OpenPushVerifyErrorCode.Refused)]
        public void AnErrorKeepsItsCodeAndMessage(string wire, OpenPushVerifyErrorCode code)
        {
            var result = Read("{\"error\":{\"code\":\"" + wire + "\",\"message\":\"why\"}}");
            Assert.IsNull(result.Verification);
            Assert.AreEqual(code, result.Error.Code);
            Assert.AreEqual("why", result.Error.Message);
        }

        [Test]
        public void ASupersededCallReadsAsNetwork()
        {
            var result = OpenPushVerifyWire.Read(OpenPushPendingCalls.Superseded());
            Assert.AreEqual(OpenPushVerifyErrorCode.Network, result.Error.Code);
            StringAssert.Contains("superseded", result.Error.Message);
        }

        [TestCase(null)]
        [TestCase("{}")]
        [TestCase("not json")]
        [TestCase("{\"verification\":{\"id\":\"vf_1\",\"status\":\"unheard_of\"}}")]
        [TestCase("{\"verification\":{\"status\":\"pending\"}}")]
        public void AnythingUnreadableIsANetworkError(string json)
            => Assert.AreEqual(OpenPushVerifyErrorCode.Network, Read(json).Error.Code);

        [Test]
        public void TheWritersRoundTrip()
        {
            var expires = DateTimeOffset.FromUnixTimeMilliseconds(1792737000123);
            var original = new OpenPushVerification("vf_2", "+4477•••0123", OpenPushVerifyChannel.Call,
                                                    OpenPushVerificationStatus.Approved, 2, expires);
            var copy = OpenPushVerifyWire.Read(OpenPushWire.Parse(OpenPushWire.ToJson(OpenPushVerifyWire.ToMap(original)))).Verification;
            Assert.AreEqual(original.ToString(), copy.ToString());

            var error = OpenPushVerifyWire.Read(OpenPushVerifyWire.ToMap(new OpenPushVerifyException(OpenPushVerifyErrorCode.TooSoon, "wait"))).Error;
            Assert.AreEqual(OpenPushVerifyErrorCode.TooSoon, error.Code);
            Assert.AreEqual("wait", error.Message);
        }

        [Test]
        public void EveryErrorCodeRoundTrips()
        {
            foreach (OpenPushVerifyErrorCode code in Enum.GetValues(typeof(OpenPushVerifyErrorCode)))
                Assert.AreEqual(code, OpenPushVerifyWire.Read(OpenPushVerifyWire.ToMap(new OpenPushVerifyException(code, "x"))).Error.Code);
        }

        [Test]
        public void TheNewCodesAreAppendedSoOlderValuesKeepTheirNumbers()
        {
            Assert.AreEqual(6, (int)OpenPushVerifyErrorCode.Network);
            Assert.AreEqual(7, (int)OpenPushVerifyErrorCode.OptedOut);
            Assert.AreEqual(8, (int)OpenPushVerifyErrorCode.NoCredit);
            Assert.AreEqual(9, (int)OpenPushVerifyErrorCode.NoRate);
        }

        [Test]
        public void ChannelsTravelAsTheServersWords()
            => CollectionAssert.AreEqual(new[] { "auto", "sms", "whatsapp", "call" }, new List<string>
            {
                OpenPushVerifyWire.Channel(OpenPushVerifyChannel.Auto), OpenPushVerifyWire.Channel(OpenPushVerifyChannel.Sms),
                OpenPushVerifyWire.Channel(OpenPushVerifyChannel.WhatsApp), OpenPushVerifyWire.Channel(OpenPushVerifyChannel.Call),
            });
    }
}
