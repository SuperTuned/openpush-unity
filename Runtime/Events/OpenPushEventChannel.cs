using System;
using System.Collections.Generic;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// One event type. Values raised before anyone subscribes are buffered (newest ten) and replayed
    /// once to the first subscriber, so a late += after Initialize still sees cold-start clicks and
    /// the first subscription state. A replay is flushed at the head of the next delivery (or by
    /// its own post, whichever runs first), so a value raised before Subscribe but not yet
    /// dispatched can never overtake the buffered values: delivery order equals raise order.
    /// An unbuffered channel (foreground) drops a value nobody is listening for: its afterDeliver
    /// still runs, so the decision is answered at once, and it is never replayed later.
    /// </summary>
    internal sealed class OpenPushEventChannel<T>
    {
        public const int BufferCap = 10;

        private readonly object _gate = new object();
        private readonly List<Action<T>> _handlers = new List<Action<T>>();
        private readonly Queue<T> _buffer = new Queue<T>();
        private readonly Queue<KeyValuePair<Action<T>, T[]>> _replays = new Queue<KeyValuePair<Action<T>, T[]>>();
        private readonly Func<IOpenPushDispatcher> _dispatcher;
        private readonly Action<string> _onHandlerError;
        private readonly Action<T> _afterDeliver;
        private readonly bool _buffered;

        internal OpenPushEventChannel(Func<IOpenPushDispatcher> dispatcher, Action<string> onHandlerError,
                                      Action<T> afterDeliver = null, bool buffered = true)
        {
            _buffered = buffered;
            _dispatcher = dispatcher;
            _onHandlerError = onHandlerError ?? (_ => { });
            _afterDeliver = afterDeliver;
        }

        public bool HasSubscribers { get { lock (_gate) return _handlers.Count > 0; } }
        public int BufferedCount { get { lock (_gate) return _buffer.Count; } }

        public void Subscribe(Action<T> handler)
        {
            if (handler == null) return;
            lock (_gate)
            {
                _handlers.Add(handler);
                if (_buffer.Count == 0) return;
                _replays.Enqueue(new KeyValuePair<Action<T>, T[]>(handler, _buffer.ToArray()));
                _buffer.Clear();
            }
            _dispatcher().Post(FlushReplays);
        }

        public void Unsubscribe(Action<T> handler)
        {
            if (handler == null) return;
            lock (_gate) _handlers.Remove(handler);
        }

        public void Raise(T value) => _dispatcher().Post(() => DeliverNow(value));

        public void DeliverNow(T value)
        {
            FlushReplays();
            Action<T>[] snapshot;
            lock (_gate)
            {
                if (_handlers.Count > 0) snapshot = _handlers.ToArray();
                else
                {
                    snapshot = null;
                    if (_buffered)
                    {
                        if (_buffer.Count == BufferCap) _buffer.Dequeue();
                        _buffer.Enqueue(value);
                    }
                }
            }
            if (snapshot != null) foreach (var handler in snapshot) Invoke(handler, value);
            if (_afterDeliver != null)
            {
                try { _afterDeliver(value); }
                catch (Exception e) { _onHandlerError("OpenPush: event completion threw: " + e.Message); }
            }
        }

        public void Reset() { lock (_gate) { _handlers.Clear(); _buffer.Clear(); _replays.Clear(); } }

        /// <summary>
        /// Main thread only. Runs pending replays, oldest first; each runs at most once, and not at
        /// all for a handler that unsubscribed before its turn came.
        /// </summary>
        private void FlushReplays()
        {
            while (true)
            {
                KeyValuePair<Action<T>, T[]> replay;
                lock (_gate)
                {
                    if (_replays.Count == 0) return;
                    replay = _replays.Dequeue();
                }
                foreach (var value in replay.Value)
                {
                    bool subscribed;
                    lock (_gate) subscribed = _handlers.Contains(replay.Key);
                    if (!subscribed) break;
                    Invoke(replay.Key, value);
                }
            }
        }

        private void Invoke(Action<T> handler, T value)
        {
            try { handler(value); }
            catch (Exception e) { _onHandlerError("✗ an OpenPush event handler threw: " + e.GetType().Name + ": " + e.Message); }
        }
    }
}
