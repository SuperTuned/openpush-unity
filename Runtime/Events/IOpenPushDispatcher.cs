using System;

namespace OpenPushSDK.Platform
{
    /// <summary>Posts work to the Unity main thread. Never blocks the caller.</summary>
    internal interface IOpenPushDispatcher
    {
        void Post(Action action);
    }

    /// <summary>Runs work inline. The default before the Unity host installs OpenPushMainThread, and in tests.</summary>
    internal sealed class ImmediateDispatcher : IOpenPushDispatcher
    {
        internal static readonly ImmediateDispatcher Instance = new ImmediateDispatcher();
        public void Post(Action action) => action?.Invoke();
    }
}
