using System;
using System.Runtime.InteropServices;
using AOT;
using OpenPushSDK.Platform;
using UnityEngine.Scripting;

namespace OpenPushSDK.iOS
{
    /// <summary>The one reverse-P/Invoke entry: static, MonoPInvokeCallback, kept alive by a static field.</summary>
    internal static class IOSEventCallback
    {
        internal static OpenPushNativeEventRouter Router;
        internal static readonly OpenPushNativeEventCallback Callback = OnNativeEvent;

        [Preserve]
        [MonoPInvokeCallback(typeof(OpenPushNativeEventCallback))]
        private static void OnNativeEvent(IntPtr kind, IntPtr json)
        {
            try { Router?.Route(Marshal.PtrToStringUTF8(kind), Marshal.PtrToStringUTF8(json)); }
            catch (Exception) { /* never unwind into Swift */ }
        }
    }
}
