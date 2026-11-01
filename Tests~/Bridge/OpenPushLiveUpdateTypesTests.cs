using NUnit.Framework;
using OpenPushSDK.Core;

namespace OpenPushSDK.Tests
{
    public sealed class OpenPushLiveUpdateTypesTests
    {
        [TestCase(OpenPushLiveUpdateResult.Displayed)]
        [TestCase(OpenPushLiveUpdateResult.Ended)]
        [TestCase(OpenPushLiveUpdateResult.Dismissed)]
        [TestCase(OpenPushLiveUpdateResult.Invalid)]
        [TestCase(OpenPushLiveUpdateResult.Stale)]
        public void NativeConsumedResultsAreHandled(OpenPushLiveUpdateResult result)
            => Assert.That(result.IsHandled(), Is.True);

        [TestCase(OpenPushLiveUpdateResult.Unsupported)]
        [TestCase(OpenPushLiveUpdateResult.NotLiveUpdate)]
        public void NonConsumedResultsAreNotHandled(OpenPushLiveUpdateResult result)
            => Assert.That(result.IsHandled(), Is.False);
    }
}
