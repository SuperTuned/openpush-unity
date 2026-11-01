using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushPendingCallsTests
    {
        [Test]
        public void IdsAreUniqueUnderConcurrency()
        {
            var calls = new OpenPushPendingCalls();
            var ids = new long[1000];
            Parallel.For(0, ids.Length, i => ids[i] = calls.Register(_ => { }));
            Assert.AreEqual(ids.Length, ids.Distinct().Count());
            Assert.AreEqual(ids.Length, calls.Count);
        }

        [Test]
        public void EachIdCompletesOnceAndUnknownIdsAreIgnored()
        {
            var calls = new OpenPushPendingCalls(); var hits = 0;
            var id = calls.Register(_ => hits++);
            Assert.IsTrue(calls.Complete(id, new Dictionary<string, object>()));
            Assert.IsFalse(calls.Complete(id, new Dictionary<string, object>()));
            Assert.IsFalse(calls.Complete(424242, null));
            Assert.AreEqual(1, hits);
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void ACallbackThatThrowsDoesNotPoisonTheTable()
        {
            var calls = new OpenPushPendingCalls();
            var id = calls.Register(_ => throw new System.Exception("host bug"));
            Assert.DoesNotThrow(() => calls.Complete(id, null));
            Assert.AreEqual(0, calls.Count);
        }

        [Test]
        public void FailAllCompletesEveryPendingCall()
        {
            var calls = new OpenPushPendingCalls(); var seen = new List<object>();
            calls.Register(p => seen.Add(p["ok"])); calls.Register(p => seen.Add(p["ok"]));
            calls.FailAll(new Dictionary<string, object> { ["ok"] = false });
            CollectionAssert.AreEqual(new object[] { false, false }, seen);
        }
    }
}
