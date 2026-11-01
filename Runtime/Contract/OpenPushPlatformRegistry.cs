namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Holds the one platform. Each platform assembly registers itself at SubsystemRegistration
    /// (no reflection discovery, nothing for the linker to miss); with none registered by first use
    /// — the Editor, desktop players — the stub is created and kept.
    /// </summary>
    internal static class OpenPushPlatformRegistry
    {
        private static readonly object Gate = new object();
        private static IOpenPushPlatform _current;

        internal static void Set(IOpenPushPlatform platform) { lock (Gate) _current = platform; }

        internal static IOpenPushPlatform Current
        {
            get { lock (Gate) return _current ??= new OpenPushStubPlatform(); }
        }

        internal static void ResetForTests() { lock (Gate) _current = null; }
    }
}
