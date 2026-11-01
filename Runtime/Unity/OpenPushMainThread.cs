using System;
using System.Threading;
using OpenPushSDK.Platform;
using UnityEngine;

namespace OpenPushSDK
{
    /// <summary>
    /// Posts to Unity's main-thread SynchronizationContext, captured before any scene loads. Native
    /// callbacks arrive on the Android UI thread, coroutine workers or iOS queues; nothing that
    /// touches Unity or host code runs until it has come through here. Never blocks the caller.
    /// </summary>
    internal sealed class OpenPushMainThread : IOpenPushDispatcher
    {
        internal static OpenPushMainThread Instance { get; private set; }
        private readonly SynchronizationContext _context;
        private readonly int _threadId;

        private OpenPushMainThread(SynchronizationContext context, int threadId) { _context = context; _threadId = threadId; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Capture()
        {
            Instance = new OpenPushMainThread(SynchronizationContext.Current, Thread.CurrentThread.ManagedThreadId);
            OpenPushHost.Dispatcher = Instance;
        }

        public void Post(Action action)
        {
            if (action == null) return;
            if (_context == null || Thread.CurrentThread.ManagedThreadId == _threadId) { Run(action); return; }
            _context.Post(_ => Run(action), null);
        }

        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception e) { Debug.LogException(e); }
        }
    }
}
