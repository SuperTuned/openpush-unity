using System.Collections.Generic;
using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushEventChannelTests
    {
        [Test]
        public void ValueInFlightAtSubscribeIsDeliveredAfterBufferedValues()
        {
            var main = new QueueDispatcher();
            var channel = new OpenPushEventChannel<string>(() => main, _ => { });
            channel.Raise("A"); main.Pump();                 // no subscriber: buffered
            channel.Raise("B");                              // posted, not yet dispatched
            var seen = new List<string>();
            channel.Subscribe(seen.Add);
            main.Pump();
            CollectionAssert.AreEqual(new[] { "A", "B" }, seen);
        }
    }
}
