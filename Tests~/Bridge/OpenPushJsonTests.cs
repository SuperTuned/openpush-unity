using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushJsonTests
    {
        [Test]
        public void EscapesQuotesBackslashesAndControlCharacters()
        {
            Assert.AreEqual("a\\\"b", OpenPushJson.Escape("a\"b"));
            Assert.AreEqual("a\\\\b", OpenPushJson.Escape("a\\b"));
            Assert.AreEqual("line\\nnext", OpenPushJson.Escape("line\nnext"));
            Assert.AreEqual("tab\\there", OpenPushJson.Escape("tab\there"));
            Assert.AreEqual("bell\\u0007", OpenPushJson.Escape("bell\a"));
        }

        [Test]
        public void PassesPlainTextThrough()
        {
            Assert.AreEqual("plain text 123", OpenPushJson.Escape("plain text 123"));
        }

        [Test]
        public void FindsStringFieldWithAndWithoutWhitespace()
        {
            Assert.AreEqual("scope_conflict",
                OpenPushJson.TryGetStringField("{\"code\":\"scope_conflict\"}", "code"));
            Assert.AreEqual("scope_conflict",
                OpenPushJson.TryGetStringField("{ \"code\" :  \"scope_conflict\" }", "code"));
        }

        [Test]
        public void ReturnsNullForMissingOrNonStringField()
        {
            Assert.IsNull(OpenPushJson.TryGetStringField("{\"other\":\"x\"}", "code"));
            Assert.IsNull(OpenPushJson.TryGetStringField("{\"code\":42}", "code"));
            Assert.IsNull(OpenPushJson.TryGetStringField(null, "code"));
            Assert.IsNull(OpenPushJson.TryGetStringField("", "code"));
        }

        [Test]
        public void HasTruthyFieldSeesStringsAndTrue()
        {
            Assert.IsTrue(OpenPushJson.HasTruthyField(
                "{\"identity_rejected\":\"auth hash did not verify\"}", "identity_rejected"));
            Assert.IsTrue(OpenPushJson.HasTruthyField(
                "{ \"identity_rejected\" : true }", "identity_rejected"));
        }

        [Test]
        public void HasTruthyFieldIgnoresMissingNullAndFalse()
        {
            Assert.IsFalse(OpenPushJson.HasTruthyField("{\"id\":\"sub-1\"}", "identity_rejected"));
            Assert.IsFalse(OpenPushJson.HasTruthyField(
                "{\"identity_rejected\":null}", "identity_rejected"));
            Assert.IsFalse(OpenPushJson.HasTruthyField(
                "{\"identity_rejected\": false}", "identity_rejected"));
            Assert.IsFalse(OpenPushJson.HasTruthyField(null, "identity_rejected"));
        }

        [Test]
        public void ReadsIdAlongsideALongerKeyWithTheSamePrefix()
        {
            // "id" must not be found inside "identity_rejected", and a long note
            // must survive the scan intact.
            var body = "{\"identity_rejected\":\"external_id_auth_hash did not verify for " +
                       "this external id; the device was registered anonymously\"," +
                       "\"id\":\"sub-1\"}";
            Assert.AreEqual("sub-1", OpenPushJson.TryGetStringField(body, "id"));
            StringAssert.Contains("did not verify",
                OpenPushJson.TryGetStringField(body, "identity_rejected"));
        }

        [Test]
        public void StringMapRoundTripsThroughWriteAndParse()
        {
            var source = new System.Collections.Generic.Dictionary<string, string>
            { ["plain"] = "v", ["quote\"key"] = "line\nbreak", ["ctrl"] = "bell\a", ["unicode"] = "caf\u00e9" };
            Assert.IsTrue(OpenPushJson.TryParseStringMap(OpenPushJson.WriteStringMap(source), out var back));
            CollectionAssert.AreEquivalent(source, back);
        }
    }
}
