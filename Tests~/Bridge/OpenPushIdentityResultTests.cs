using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushIdentityResultTests
    {
        [Test]
        public void OkCarriesNoNoteAndNoError()
        {
            var result = OpenPushIdentityResult.Ok();
            Assert.IsTrue(result.Success);
            Assert.IsNull(result.IdentityRejectedNote);
            Assert.IsNull(result.Error);
        }

        [Test]
        public void RejectedIsNotSuccessAndAlwaysCarriesANote()
        {
            var result = OpenPushIdentityResult.Rejected("auth hash did not verify");
            Assert.IsFalse(result.Success);
            Assert.AreEqual("auth hash did not verify", result.IdentityRejectedNote);
            Assert.IsNull(result.Error);
            // A rejection with no server note must still be distinguishable from
            // a transport failure, so the note is never left null.
            Assert.IsNotNull(OpenPushIdentityResult.Rejected(null).IdentityRejectedNote);
        }

        [Test]
        public void FailedCarriesAnErrorAndNoRejectionNote()
        {
            var result = OpenPushIdentityResult.Failed("OpenPush could not register (500).");
            Assert.IsFalse(result.Success);
            Assert.IsNull(result.IdentityRejectedNote);
            StringAssert.Contains("(500)", result.Error);
        }
    }
}
