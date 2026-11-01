using System;
using System.Linq;
using NUnit.Framework;

namespace OpenPushSDK.Tests
{
    /// The native SDKs do the client work, so no managed client-engine type (transport, session store,
    /// scheduler, in-app engine) belongs in the assembly. This fails if one appears.
    public class OpenPushRemovedApiTests
    {
        private static readonly string[] Removed =
        {
            "OpenPushClient", "IOpenPushTransport", "OpenPushRequest", "OpenPushResponse", "IOpenPushTokenCache",
            "IOpenPushScheduler", "IOpenPushPermissionBridge", "IOpenPushIamPresenter", "IOpenPushHtmlIamPresenter",
            "OpenPushIamPresentationContext", "OpenPushInAppMessages", "OpenPushPayload", "OpenPushSessionStore",
            "OpenPushSessionState", "OpenPushEnvironment",
            "InAppActionParser", "InAppTriggerEngine", "OpenPushInAppState", "OpenPushImmediateScheduler",
        };

        [Test]
        public void EngineTypesAreGone()
        {
            var names = typeof(OpenPush).Assembly.GetTypes().Select(t => t.Name).ToHashSet();
            var survivors = Removed.Where(names.Contains).ToArray();
            Assert.IsEmpty(survivors, "still present: " + string.Join(", ", survivors));
        }

        [Test]
        public void AttachIsGone()
            => Assert.IsNull(typeof(OpenPush).GetMethod("Attach"));
    }
}
