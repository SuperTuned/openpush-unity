using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    public class OpenPushEventHubTests
    {
        private QueueDispatcher _main;
        private List<string> _errors;

        [SetUp] public void SetUp() { _main = new QueueDispatcher(); _errors = new List<string>(); }

        private OpenPushEventChannel<int> Channel(Action<int> after = null)
            => new OpenPushEventChannel<int>(() => _main, _errors.Add, after);

        [Test]
        public void RaiseFromAnyThreadDeliversOnlyWhenTheMainThreadPumps()
        {
            var channel = Channel(); var seen = new List<int>();
            channel.Subscribe(seen.Add);
            var t = new Thread(() => channel.Raise(7)); t.Start(); t.Join();
            Assert.IsEmpty(seen);
            _main.Pump();
            CollectionAssert.AreEqual(new[] { 7 }, seen);
        }

        // RF2
        [Test]
        public void BufferKeepsTheNewestTenAndReplaysThemOnceToTheFirstSubscriber()
        {
            var channel = Channel();
            for (var i = 1; i <= 12; i++) channel.Raise(i);
            _main.Pump();
            Assert.AreEqual(OpenPushEventChannel<int>.BufferCap, channel.BufferedCount);
            var first = new List<int>(); var second = new List<int>();
            channel.Subscribe(first.Add);
            channel.Subscribe(second.Add);
            _main.Pump();
            CollectionAssert.AreEqual(new[] { 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }, first);
            Assert.IsEmpty(second);
            Assert.AreEqual(0, channel.BufferedCount);
        }

        [Test]
        public void AThrowingHandlerIsReportedAndOthersStillRun()
        {
            var channel = Channel(); var seen = new List<int>();
            channel.Subscribe(_ => throw new InvalidOperationException("boom"));
            channel.Subscribe(seen.Add);
            channel.Raise(1); _main.Pump();
            CollectionAssert.AreEqual(new[] { 1 }, seen);
            Assert.AreEqual(1, _errors.Count);
            StringAssert.Contains("boom", _errors[0]);
        }

        [Test]
        public void UnsubscribeStopsDelivery()
        {
            var channel = Channel(); var seen = new List<int>();
            Action<int> handler = seen.Add;
            channel.Subscribe(handler); channel.Unsubscribe(handler);
            channel.Raise(1); _main.Pump();
            Assert.IsEmpty(seen);
        }

        [Test]
        public void AfterDeliverRunsOncePerValueWithOrWithoutSubscribers()
        {
            var after = new List<int>();
            var channel = Channel(after.Add);
            channel.Raise(1); _main.Pump();                 // buffered
            channel.Subscribe(_ => { });
            _main.Pump();                                  // replay must not run afterDeliver again
            channel.Raise(2); _main.Pump();
            CollectionAssert.AreEqual(new[] { 1, 2 }, after);
        }

        [Test]
        public void HubForegroundChannelFinishesEventsEvenWhenAHandlerThrows()
        {
            var hub = new OpenPushEventHub(() => _main, _errors.Add);
            var completions = new List<bool>();
            hub.Foreground.Subscribe(_ => throw new Exception("handler bug"));
            hub.Foreground.Raise(new OpenPushForegroundEvent { CompletionId = "c", Completer = (_, d) => completions.Add(d) });
            _main.Pump();
            CollectionAssert.AreEqual(new[] { true }, completions);
        }

        [Test]
        public void NullSubscribeIsIgnored() { var c = Channel(); c.Subscribe(null); Assert.IsFalse(c.HasSubscribers); }

        // R1 Minor 2: a foreground decision cannot wait, so it is answered at once and never replayed.
        [Test]
        public void ForegroundWithoutSubscriberDisplaysAtOnceAndIsNeverReplayed()
        {
            var hub = new OpenPushEventHub(() => _main, _errors.Add);
            var completions = new List<bool>();
            hub.Foreground.Raise(new OpenPushForegroundEvent { CompletionId = "c", Completer = (_, d) => completions.Add(d) });
            _main.Pump();
            CollectionAssert.AreEqual(new[] { true }, completions);
            Assert.AreEqual(0, hub.Foreground.BufferedCount);
            var late = 0;
            hub.Foreground.Subscribe(_ => late++);
            _main.Pump();
            Assert.AreEqual(0, late);
        }

        [Test]
        public void HostForwardedForegroundWithoutSubscriberIsNotBuffered()
        {
            var hub = new OpenPushEventHub(() => _main, _errors.Add);
            hub.Foreground.DeliverNow(new OpenPushForegroundEvent());
            Assert.AreEqual(0, hub.Foreground.BufferedCount);
        }

        // R1 Minor 1
        [Test]
        public void AReplayIsSkippedForAHandlerThatUnsubscribedBeforeItRan()
        {
            var channel = Channel();
            channel.Raise(1); _main.Pump();
            var seen = new List<int>();
            Action<int> handler = seen.Add;
            channel.Subscribe(handler);
            channel.Unsubscribe(handler);
            _main.Pump();
            Assert.IsEmpty(seen);
        }
    }
}
