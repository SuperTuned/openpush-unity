using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    internal readonly struct OpenPushLogLine
    {
        public readonly OpenPushLogLevel Level; public readonly string Message;
        public OpenPushLogLine(OpenPushLogLevel level, string message) { Level = level; Message = message; }
    }
    internal readonly struct OpenPushSubscriptionChange
    {
        public readonly PushSubscriptionState Previous; public readonly PushSubscriptionState Current;
        public OpenPushSubscriptionChange(PushSubscriptionState previous, PushSubscriptionState current) { Previous = previous; Current = current; }
    }
    internal readonly struct OpenPushInAppLifecycle
    {
        public readonly string MessageId; public readonly string Phase;
        public OpenPushInAppLifecycle(string messageId, string phase) { MessageId = messageId; Phase = phase; }
    }
    internal readonly struct OpenPushInAppClicked
    {
        public readonly string MessageId; public readonly OpenPushInAppClick Click;
        public OpenPushInAppClicked(string messageId, OpenPushInAppClick click) { MessageId = messageId; Click = click; }
    }
}
