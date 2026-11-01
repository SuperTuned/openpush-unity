using System;
using System.Collections.Generic;
using System.Threading;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Pairs an async native call with its result across a C ABI. Completion may arrive on any
    /// thread; each id completes at most once and a throwing callback cannot corrupt the table.
    /// </summary>
    internal sealed class OpenPushPendingCalls
    {
        private readonly object _gate = new object();
        private readonly Dictionary<long, Action<Dictionary<string, object>>> _pending =
            new Dictionary<long, Action<Dictionary<string, object>>>();
        private long _next;

        public int Count { get { lock (_gate) return _pending.Count; } }

        public long Register(Action<Dictionary<string, object>> onResult)
        {
            var id = Interlocked.Increment(ref _next);
            lock (_gate) _pending[id] = onResult ?? (_ => { });
            return id;
        }

        public bool Complete(long id, Dictionary<string, object> payload)
        {
            Action<Dictionary<string, object>> callback;
            lock (_gate)
            {
                if (!_pending.TryGetValue(id, out callback)) return false;
                _pending.Remove(id);
            }
            try { callback(payload ?? new Dictionary<string, object>()); } catch (Exception) { }
            return true;
        }

        /// <summary>
        /// The payload for calls a new Initialize superseded: reads as a failure for every result
        /// shape (identity, permission, receipt).
        /// </summary>
        public static Dictionary<string, object> Superseded() => new Dictionary<string, object>
        {
            ["success"] = false, ["granted"] = false,
            ["error"] = "superseded — OpenPush.Initialize ran again before the native call completed",
        };

        public void FailAll(Dictionary<string, object> payload)
        {
            long[] ids;
            lock (_gate) { ids = new long[_pending.Count]; _pending.Keys.CopyTo(ids, 0); }
            foreach (var id in ids) Complete(id, payload);
        }
    }
}
