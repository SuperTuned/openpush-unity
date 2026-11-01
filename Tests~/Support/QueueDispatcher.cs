using System;
using System.Collections.Generic;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// Stands in for the Unity main thread: nothing runs until the test pumps it.
    internal sealed class QueueDispatcher : IOpenPushDispatcher
    {
        private readonly Queue<Action> _queue = new Queue<Action>();
        public int Pending { get { lock (_queue) return _queue.Count; } }
        public void Post(Action action) { lock (_queue) _queue.Enqueue(action); }
        public void Pump()
        {
            while (true)
            {
                Action next;
                lock (_queue) { if (_queue.Count == 0) return; next = _queue.Dequeue(); }
                next();
            }
        }
    }
}
