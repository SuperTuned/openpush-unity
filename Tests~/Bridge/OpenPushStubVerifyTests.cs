using System.Threading.Tasks;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// The Editor's phone codes: nothing sent, 123456 matches, otherwise the server's rules.
    public class OpenPushStubVerifyTests
    {
        private OpenPushStubVerify _verify;

        [SetUp] public void SetUp() => _verify = new OpenPushStubVerify(_ => { });

        private OpenPushVerifyResult Start(OpenPushVerifyChannel channel = OpenPushVerifyChannel.Auto)
        {
            OpenPushVerifyResult result = null;
            _verify.Start("+14155550123", channel, null, r => result = r);
            return result;
        }

        private OpenPushVerifyResult Check(string id, string code)
        {
            OpenPushVerifyResult result = null;
            _verify.Check(id, code, r => result = r);
            return result;
        }

        [Test]
        public void StartIsPendingOnSmsWithTheNumberMasked()
        {
            var v = Start().Verification;
            Assert.AreEqual(OpenPushVerificationStatus.Pending, v.Status);
            Assert.AreEqual(OpenPushVerifyChannel.Sms, v.Channel);
            Assert.AreEqual("+1415•••0123", v.MaskedNumber);
            Assert.AreEqual(0, v.Attempts);
            Assert.IsNotNull(v.ExpiresAt);
            Assert.AreEqual(OpenPushVerifyChannel.WhatsApp, Start(OpenPushVerifyChannel.WhatsApp).Verification.Channel);
        }

        [Test]
        public void AWrongCodeStaysPendingThenTheRightOneApproves()
        {
            var id = Start().Verification.Id;
            var wrong = Check(id, "000000").Verification;
            Assert.AreEqual(OpenPushVerificationStatus.Pending, wrong.Status);
            Assert.AreEqual(1, wrong.Attempts);
            var right = Check(id, OpenPushStubVerify.Code).Verification;
            Assert.AreEqual(OpenPushVerificationStatus.Approved, right.Status);
            Assert.AreEqual(OpenPushVerifyErrorCode.NotPending, Check(id, OpenPushStubVerify.Code).Error.Code);
        }

        [Test]
        public void TheFifthWrongCodeEndsIt()
        {
            var id = Start().Verification.Id;
            OpenPushVerification last = null;
            for (var i = 0; i < OpenPushStubVerify.MaxAttempts; i++) last = Check(id, "1").Verification;
            Assert.AreEqual(OpenPushVerificationStatus.MaxAttempts, last.Status);
            Assert.AreEqual(OpenPushVerifyErrorCode.NotPending, Check(id, OpenPushStubVerify.Code).Error.Code);
        }

        [Test]
        public void ResendSwitchesTheChannelAndAnUnknownIdIsRefused()
        {
            var id = Start().Verification.Id;
            OpenPushVerifyResult resent = null;
            _verify.Resend(id, OpenPushVerifyChannel.Call, r => resent = r);
            Assert.AreEqual(OpenPushVerifyChannel.Call, resent.Verification.Channel);
            Assert.AreEqual(OpenPushVerifyErrorCode.Refused, Check("vf_missing", "1").Error.Code);
        }

        [Test]
        public void TheFacadeRunsEndToEndOverTheStub()
        {
            OpenPush.ResetForTests();
            OpenPushPlatformRegistry.Set(new OpenPushStubPlatform());
            try
            {
                OpenPush.Initialize("app-1", "pk_test");
                Assert.IsTrue(OpenPush.User.AddSms("+14155550123"));
                var started = OpenPush.Verify.StartAsync("+14155550123");
                Assert.AreEqual(TaskStatus.RanToCompletion, started.Status);
                var approved = OpenPush.Verify.CheckAsync(started.Result, "123456");
                Assert.AreEqual(OpenPushVerificationStatus.Approved, approved.Result.Status);
                var tooLate = OpenPush.Verify.ResendAsync(approved.Result);
                Assert.AreEqual(OpenPushVerifyErrorCode.NotPending, ((OpenPushVerifyException)tooLate.Exception.InnerException).Code);
            }
            finally
            {
                OpenPush.ResetForTests();
                OpenPushPlatformRegistry.ResetForTests();
            }
        }
    }
}
