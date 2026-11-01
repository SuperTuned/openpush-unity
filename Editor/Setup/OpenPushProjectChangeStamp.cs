using System;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// A counter that moves whenever the project's assets change (any import, delete or move, see
    /// <see cref="OpenPushProjectChangeWatcher"/>), Package Manager registers packages, or someone asks for a
    /// re-check. Setup rows that scan the project cache their answer against it instead of a timer, so an
    /// idle Setup window costs nothing. Free of UnityEditor types, so the tests drive it directly.
    /// </summary>
    internal static class OpenPushProjectChangeStamp
    {
        internal static int Stamp { get; private set; }

        internal static void Bump() => Stamp++;

        /// A file's last write time, or 0 when it is missing: part of a cache key for a file that is not an
        /// asset (Packages/manifest.json, ProjectSettings and Library files), whose writes move no stamp.
        internal static long WriteTicks(string path)
            => System.IO.File.Exists(path) ? System.IO.File.GetLastWriteTimeUtc(path).Ticks : 0L;
    }

    /// <summary>A value recomputed only when the change stamp (and an optional extra key) moved.</summary>
    internal sealed class OpenPushStampedCache<T>
    {
        private int _stamp = -1;
        private object _key;
        private T _value;

        internal T Get(Func<T> compute, object extraKey = null)
        {
            if (_stamp == OpenPushProjectChangeStamp.Stamp && Equals(_key, extraKey)) return _value;
            _value = compute();
            _stamp = OpenPushProjectChangeStamp.Stamp;
            _key = extraKey;
            return _value;
        }
    }
}
