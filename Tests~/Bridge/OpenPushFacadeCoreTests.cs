using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public class OpenPushFacadeCoreTests : FacadeTestBase
    {
        [Test]
        public void InitializeWithoutAnyKeyRefusesAndSaysWhy()
        {
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot();
            string failure = null;
            OpenPush.OnRegistrationFailed = m => failure = m;
            OpenPush.Initialize("app-1");
            StringAssert.Contains("no SDK key", failure);
            CollectionAssert.DoesNotContain(Platform.Calls, "Initialize app-1");
        }

        [Test]
        public void InitializePassesKeyWrapperAppGroupAndServer()
        {
            OpenPushHost.Settings = () => new OpenPushSettingsSnapshot { SdkKey = "pk_asset", AppGroup = "group.a.openpush" };
            OpenPush.Initialize("app-1");
            var options = Platform.CoreFake.InitOptions;
            Assert.AreEqual("app-1", options.AppId);
            Assert.AreEqual("pk_asset", options.SdkKey);
            Assert.AreEqual("unity/1.2.2", options.Wrapper);
            Assert.AreEqual("group.a.openpush", options.AppGroup);
            Assert.AreEqual(OpenPush.DefaultServerUrl, options.ServerUrl);
        }

        [Test]
        public void ExplicitKeyWinsOverTheAsset()
        {
            OpenPush.Initialize("app-1", "pk_code", "https://self.example");
            Assert.AreEqual("pk_code", Platform.CoreFake.InitOptions.SdkKey);
            Assert.AreEqual("https://self.example", Platform.CoreFake.InitOptions.ServerUrl);
        }

        [Test]
        public void CredentialIsReadAtInitializeAndPushedOnRefresh()
        {
            var value = "cred-1";
            OpenPush.Initialize("app-1", "pk", credential: () => value);
            Assert.AreEqual("cred-1", Platform.CoreFake.Credential);
            value = "cred-2";
            OpenPush.RefreshCredential();
            Assert.AreEqual("cred-2", Platform.CoreFake.Credential);
        }

        [Test]
        public void SideEffectCallsBeforeInitializeThrowWithAClearMessage()
        {
            var e = Assert.Throws<InvalidOperationException>(() => OpenPush.Register("tok"));
            StringAssert.Contains("before OpenPush.Initialize", e.Message);
            Assert.Throws<InvalidOperationException>(() => OpenPush.Login("p"));
            Assert.Throws<InvalidOperationException>(() => OpenPush.TrackEvent("x"));
        }

        [Test]
        public void ConfigurationAndSubscriptionsBeforeInitializeNeverThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                OpenPush.LogLevel = OpenPushLogLevel.Info;
                OpenPush.OnLog = _ => { };
                OpenPush.OnIdentityVerificationRequired += _ => { };
                var _ = OpenPush.IsInitialized;
                var __ = OpenPush.SdkVersion;
            });
        }

        [Test]
        public void NativeLogIsLevelFilteredAndDeliveredOnTheMainThread()
        {
            var lines = new List<string>();
            OpenPush.OnLog = lines.Add;
            OpenPush.LogLevel = OpenPushLogLevel.Info;
            OpenPush.Initialize("app-1");
            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Verbose, "• bookkeeping");
            Platform.CoreFake.RaiseLog(OpenPushLogLevel.Error, "✗ failed");
            Assert.IsEmpty(lines);
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "✗ failed" }, lines);
        }

        [Test]
        public void RegisteredAndFailedCallbacksRunOnPump()
        {
            var seen = new List<string>();
            OpenPush.OnRegistered = () => seen.Add("ok");
            OpenPush.OnRegistrationFailed = m => seen.Add("fail " + m);
            InitializeAndPump();
            Platform.CoreFake.RaiseRegistered();
            Platform.CoreFake.RaiseRegistrationFailed("-1");
            Main.Pump();
            CollectionAssert.AreEqual(new[] { "ok", "fail -1" }, seen);
        }

        [Test]
        public void LoginResultIsMarshalledToTheMainThread()
        {
            InitializeAndPump();
            OpenPushIdentityResult result = null;
            OpenPush.Login("player-9", "hash", r => result = r);
            CollectionAssert.Contains(Platform.Calls, "Login player-9 hash");
            Platform.CoreFake.PendingIdentity(OpenPushIdentityResult.Ok());
            Assert.IsNull(result);
            Main.Pump();
            Assert.IsTrue(result.Success);
        }

        [Test]
        public void TrackEventSerializesProperties()
        {
            InitializeAndPump();
            OpenPush.TrackEvent("checkout", new Dictionary<string, object> { ["value"] = 3, ["vip"] = true });
            CollectionAssert.Contains(Platform.Calls, "TrackEvent checkout {\"value\":3,\"vip\":true}");
            OpenPush.TrackEvent("plain");
            CollectionAssert.Contains(Platform.Calls, "TrackEvent plain {}");
        }

        [Test]
        public void RegisterAndReceiptForward()
        {
            InitializeAndPump();
            var then = false;
            OpenPush.Register("tok");
            OpenPush.Receipt("clicked", "m1", () => then = true);
            Main.Pump();
            CollectionAssert.Contains(Platform.Calls, "Register tok ");
            CollectionAssert.Contains(Platform.Calls, "Receipt clicked m1");
            Assert.IsTrue(then);
        }

#pragma warning disable CS0618 // The deprecated two-argument Register: games still call it, so it must keep working.
        [Test]
        public void TheTwoArgumentRegisterStillWorksAndCarriesTheIdThroughLogin()
        {
            InitializeAndPump();
            OpenPushUserState changed = null;
            OpenPush.User.Changed += state => changed = state;
            // The native SDK applies the id as a login, so it reports it as the current identity.
            Platform.UserFake.ExternalId = "player-1";
            OpenPush.Register("tok", "player-1");
            Main.Pump();
            CollectionAssert.Contains(Platform.Calls, "Register tok player-1");
            Assert.AreEqual("player-1", changed?.ExternalId, "the id is reported the way a Login result is");
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        public void TheTwoArgumentRegisterWithoutAnIdKeepsTheCurrentLogin(string externalId)
        {
            InitializeAndPump();
            OpenPush.Register("tok", externalId);
            Main.Pump();
            CollectionAssert.Contains(Platform.Calls, "Register tok ", "no id reaches the native SDK");
        }

        [Test]
        public void TheTwoArgumentRegisterStillThrowsBeforeInitialize()
        {
            var e = Assert.Throws<InvalidOperationException>(() => OpenPush.Register("tok", "player-1"));
            StringAssert.Contains("before OpenPush.Initialize", e.Message);
        }
#pragma warning restore CS0618

        [Test]
        public void BothRegisterSignaturesStayInTheBinary()
        {
            // A game built against an earlier package calls Register(string, string); it must still bind.
            var two = typeof(OpenPush).GetMethod(nameof(OpenPush.Register), new[] { typeof(string), typeof(string) });
            Assert.IsNotNull(two);
            var obsolete = (ObsoleteAttribute)Attribute.GetCustomAttribute(two, typeof(ObsoleteAttribute));
            Assert.IsNotNull(obsolete, "deprecated");
            Assert.IsFalse(obsolete.IsError, "a warning, never a compile error");
            Assert.IsNotNull(typeof(OpenPush).GetMethod(nameof(OpenPush.Register), new[] { typeof(string) }));
        }

        [Test]
        public void IdentityVerificationRequiredIsBufferedUntilSubscribed()
        {
            InitializeAndPump();
            Platform.CoreFake.RaiseIdentityVerificationRequired("player-9");
            Main.Pump();
            string seen = null;
            OpenPush.OnIdentityVerificationRequired += id => seen = id;
            Main.Pump();
            Assert.AreEqual("player-9", seen);
        }

        [Test]
        public void SdkVersionIsNativeAndWrapperVersionIsTheUnityPackage()
        {
            Assert.AreEqual("9.9.9", OpenPush.SdkVersion);
            Assert.AreEqual("1.2.2", OpenPush.WrapperVersion);
        }

#pragma warning disable CS0618
        [Test]
        public void FlushIsANoOpThatLogs()
        {
            var lines = new List<string>();
            OpenPush.OnLog = lines.Add;
            InitializeAndPump();
            OpenPush.Flush();
            Main.Pump();
            Assert.IsTrue(lines.Exists(l => l.Contains("Flush() is a no-op")));
        }
#pragma warning restore CS0618

        [Test]
        public void MessageIdAndTokenHelpersFormatTheirInput()
        {
            Assert.AreEqual("m1", OpenPush.MessageId(new Dictionary<string, string> { ["op_message_id"] = "m1" }));
            Assert.IsNull(OpenPush.MessageId(null));
            Assert.AreEqual("0a0b", OpenPush.HexToken(new byte[] { 10, 11 }));
            Assert.AreEqual("", OpenPush.HexToken(null));
            Assert.AreEqual("abcdefghijkl…(26 chars)", OpenPush.ShortToken("abcdefghijklmnopqrstuvwxyz"));
            Assert.AreEqual("short", OpenPush.ShortToken("short"));
        }
    }
}
