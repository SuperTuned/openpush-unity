using System;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    internal sealed class OpenPushInitOptions
    {
        public string AppId; public string SdkKey; public string ServerUrl; public string AppGroup;
        public Func<string> Credential; public bool Provisional;
        public string Wrapper = OpenPushVersion.Wrapper; public OpenPushLogLevel LogLevel = OpenPushLogLevel.Verbose;
    }
}
