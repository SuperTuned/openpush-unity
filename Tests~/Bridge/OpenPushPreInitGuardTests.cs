using System;
using System.IO;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// <summary>The pre-initialize guard runs before any argument check, so an empty key still throws.</summary>
    public class OpenPushPreInitGuardTests : FacadeTestBase
    {
        [Test]
        public void AddTagWithAnEmptyKeyThrowsBeforeInitialize()
            => Assert.Throws<InvalidOperationException>(() => OpenPush.User.AddTag("", "v"));

        [Test]
        public void AddAliasWithAnEmptyLabelThrowsBeforeInitialize()
            => Assert.Throws<InvalidOperationException>(() => OpenPush.User.AddAlias(null, "id"));

        [Test]
        public void AddTriggerWithAnEmptyKeyThrowsBeforeInitialize()
            => Assert.Throws<InvalidOperationException>(() => OpenPush.InAppMessages.AddTrigger("", 1));

        [Test]
        public void OpenPromotionSettingsThrowsBeforeInitialize()
            => Assert.Throws<InvalidOperationException>(() => OpenPush.LiveUpdates.OpenPromotionSettings());

        [Test]
        public void EmptyKeysAreStillIgnoredAfterInitialize()
        {
            InitializeAndPump();
            Assert.DoesNotThrow(() => OpenPush.User.AddTag("", "v"));
            Assert.DoesNotThrow(() => OpenPush.User.AddAlias("", "id"));
            Assert.DoesNotThrow(() => OpenPush.InAppMessages.AddTrigger("", 1));
            Assert.DoesNotThrow(() => OpenPush.LiveUpdates.OpenPromotionSettings());
        }
    }

    /// <summary>
    /// A re-Initialize starts a new native session: correlation ids still pending from the previous
    /// one can never complete, so the iOS platform fails them instead of leaking their callbacks.
    /// </summary>
    public class OpenPushPendingCallsReinitializeTests
    {
        private static string RepoFile(string relative)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "package.json")) || dir != null && !Directory.Exists(Path.Combine(dir.FullName, "Runtime")))
                dir = dir.Parent;
            Assert.IsNotNull(dir, "repository root not found above " + AppContext.BaseDirectory);
            return File.ReadAllText(Path.Combine(dir.FullName, relative));
        }

        [Test]
        public void SupersededPayloadReadsAsAFailure()
        {
            var calls = new OpenPushSDK.Platform.OpenPushPendingCalls();
            System.Collections.Generic.Dictionary<string, object> seen = null;
            calls.Register(p => seen = p);
            calls.FailAll(OpenPushSDK.Platform.OpenPushPendingCalls.Superseded());
            Assert.AreEqual(0, calls.Count);
            var identity = OpenPushSDK.Platform.IdentityResultWire.From(seen)?.ToPublic();
            Assert.IsNotNull(identity);
            Assert.IsFalse(identity.Success);
        }

        [Test]
        public void IOSInitializeFailsCallsPendingFromThePreviousSession()
        {
            var source = RepoFile("Runtime/Platforms/iOS/IOSPlatform.cs");
            var start = source.IndexOf("public void Initialize(OpenPushInitOptions o)", StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0);
            var body = source.Substring(start, source.IndexOf("public void UpdateCredential", start, StringComparison.Ordinal) - start);
            StringAssert.Contains("Pending.FailAll(OpenPushPendingCalls.Superseded())", body);
        }
    }
}
