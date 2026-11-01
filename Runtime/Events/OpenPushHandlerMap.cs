using System;
using System.Collections.Generic;

namespace OpenPushSDK.Platform
{
    /// <summary>Maps a host delegate to the wrapper subscribed on a channel, so -= finds it again.</summary>
    internal sealed class OpenPushHandlerMap<T>
    {
        private readonly object _gate = new object();
        private readonly Dictionary<Delegate, Action<T>> _map = new Dictionary<Delegate, Action<T>>();

        public Action<T> Add(Delegate key, Action<T> wrapper)
        {
            if (key == null) return null;
            lock (_gate) { if (_map.ContainsKey(key)) return null; _map[key] = wrapper; }
            return wrapper;
        }

        public Action<T> Remove(Delegate key)
        {
            if (key == null) return null;
            lock (_gate) { if (!_map.TryGetValue(key, out var wrapper)) return null; _map.Remove(key); return wrapper; }
        }

        public void Clear() { lock (_gate) _map.Clear(); }
    }
}
