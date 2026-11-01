using System;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// OpenPush.Verify over the recorded fake: what reaches the platform, and how its answer becomes a
    /// task completed on the main thread (a verification) or faulted (an OpenPushVerifyException).
    public class OpenPushFacadeVerifyTests : FacadeTestBase
    {
        private static OpenPushVerification Pending(string id = "vf_1", OpenPushVerifyChannel channel = OpenPushVerifyChannel.Sms)
            => new OpenPushVerification(id, "+1415•••0123", channel, OpenPushVerificationStatus.Pending, 0, null);

        [Test]
        public void StartSendsTheNormalizedNumberChannelAndLocaleAndCompletesOnTheMainThread()
        {
            InitializeAndPump();
            var task = OpenPush.Verify.StartAsync(" 0014155550123 ", OpenPushVerifyChannel.WhatsApp, " pt-BR ");
            CollectionAssert.Contains(Platform.Calls, "VerifyStart +14155550123 WhatsApp pt-BR");
            Platform.VerifyFake.Pending(OpenPushVerifyResult.Ok(Pending(channel: OpenPushVerifyChannel.WhatsApp)));
            Assert.IsFalse(task.IsCompleted, "the answer must wait for the main thread");
            Main.Pump();
            Assert.AreEqual(TaskStatus.RanToCompletion, task.Status);
            Assert.AreEqual("vf_1", task.Result.Id);
            Assert.AreEqual(OpenPushVerifyChannel.WhatsApp, task.Result.Channel);
        }

        [Test]
        public void StartDefaultsToAutoWithNoLocale()
        {
            InitializeAndPump();
            OpenPush.Verify.StartAsync("+14155550123");
            OpenPush.Verify.StartAsync("+14155550123", locale: "  ");
            CollectionAssert.AreEqual(new[] { "VerifyStart +14155550123 Auto ", "VerifyStart +14155550123 Auto " },
                                      Platform.Calls.FindAll(c => c.StartsWith("VerifyStart")));
        }

        [Test]
        public void AnInvalidNumberFaultsBeforeAnythingIsSent()
        {
            InitializeAndPump();
            var task = OpenPush.Verify.StartAsync("415-555-0123");
            Assert.IsTrue(task.IsFaulted);
            var error = (OpenPushVerifyException)task.Exception.InnerException;
            Assert.AreEqual(OpenPushVerifyErrorCode.InvalidNumber, error.Code);
            StringAssert.Contains("E.164", error.Message);
            Assert.IsFalse(Platform.Calls.Exists(c => c.StartsWith("VerifyStart")));
        }

        [Test]
        public void ARefusalFaultsTheTaskWithItsCodeAndMessage()
        {
            InitializeAndPump();
            var task = OpenPush.Verify.StartAsync("+14155550123");
            Platform.VerifyFake.Pending(OpenPushVerifyResult.Failed(OpenPushVerifyErrorCode.RateLimited, "slow down"));
            Main.Pump();
            Assert.IsTrue(task.IsFaulted);
            var error = (OpenPushVerifyException)task.Exception.InnerException;
            Assert.AreEqual(OpenPushVerifyErrorCode.RateLimited, error.Code);
            Assert.AreEqual("slow down", error.Message);
        }

        [Test]
        public void AnEmptyAnswerFaultsAsNetwork()
        {
            InitializeAndPump();
            var task = OpenPush.Verify.StartAsync("+14155550123");
            Platform.VerifyFake.Pending(null);
            Main.Pump();
            Assert.AreEqual(OpenPushVerifyErrorCode.Network, ((OpenPushVerifyException)task.Exception.InnerException).Code);
        }

        [Test]
        public void CheckNamesTheVerificationByIdWithTheTrimmedCode()
        {
            InitializeAndPump();
            var task = OpenPush.Verify.CheckAsync(Pending(), " 482913 ");
            CollectionAssert.Contains(Platform.Calls, "VerifyCheck vf_1 482913");
            Platform.VerifyFake.Pending(OpenPushVerifyResult.Ok(
                new OpenPushVerification("vf_1", "+1415•••0123", OpenPushVerifyChannel.Sms, OpenPushVerificationStatus.Approved, 1, null)));
            Main.Pump();
            Assert.AreEqual(OpenPushVerificationStatus.Approved, task.Result.Status);
            Assert.AreEqual(1, task.Result.Attempts);
        }

        [Test]
        public void ResendUsesTheGivenChannelOrTheOneTheCodeLastWentOutOn()
        {
            InitializeAndPump();
            OpenPush.Verify.ResendAsync(Pending(channel: OpenPushVerifyChannel.WhatsApp));
            OpenPush.Verify.ResendAsync(Pending(), OpenPushVerifyChannel.Call);
            CollectionAssert.AreEqual(new[] { "VerifyResend vf_1 WhatsApp", "VerifyResend vf_1 Call" },
                                      Platform.Calls.FindAll(c => c.StartsWith("VerifyResend")));
        }

        [Test]
        public void CheckAndResendNeedAVerification()
        {
            InitializeAndPump();
            Assert.Throws<ArgumentNullException>(() => OpenPush.Verify.CheckAsync(null, "1"));
            Assert.Throws<ArgumentNullException>(() => OpenPush.Verify.ResendAsync(null));
        }

        [Test]
        public void VerifyRequiresInitialize()
        {
            Assert.Throws<InvalidOperationException>(() => OpenPush.Verify.StartAsync("+14155550123"));
            Assert.Throws<InvalidOperationException>(() => OpenPush.Verify.CheckAsync(Pending(), "1"));
            Assert.Throws<InvalidOperationException>(() => OpenPush.Verify.ResendAsync(Pending()));
            Assert.IsEmpty(Platform.Calls);
        }
    }
}
