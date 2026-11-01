using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    /// Initialize(appId) resolves its key through this rule; blank counts as absent and keys are trimmed.
    public class OpenPushSdkKeyResolutionTests
    {
        [Test]
        public void AnExplicitKeyWinsOverTheBundledOne()
        {
            Assert.AreEqual("explicit",
                OpenPushConfiguration.ResolveSdkKey("explicit", "from_asset"));
        }

        [Test]
        public void TheBundledKeyIsUsedWhenNoneIsPassed()
        {
            Assert.AreEqual("from_asset",
                OpenPushConfiguration.ResolveSdkKey(null, "from_asset"));
        }

        [Test]
        public void KeysAreTrimmed()
        {
            Assert.AreEqual("from_asset",
                OpenPushConfiguration.ResolveSdkKey(null, "  from_asset  "));
            Assert.AreEqual("explicit",
                OpenPushConfiguration.ResolveSdkKey("  explicit  ", null));
        }

        [Test]
        public void ABlankKeyCountsAsAbsentOnEitherSide()
        {
            // A blank explicit argument must fall through to the asset rather than
            // shadowing it: an empty string is "not configured", not "configured
            // with nothing", and the two must not be confused.
            Assert.AreEqual("from_asset",
                OpenPushConfiguration.ResolveSdkKey("   ", "from_asset"));
            Assert.IsNull(OpenPushConfiguration.ResolveSdkKey("", ""));
            Assert.IsNull(OpenPushConfiguration.ResolveSdkKey(null, "   "));
        }

        [Test]
        public void NothingAnywhereResolvesToNullSoTheCallerCanRefuse()
        {
            Assert.IsNull(OpenPushConfiguration.ResolveSdkKey(null, null));
        }
    }
}
