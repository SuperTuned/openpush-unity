namespace OpenPushSDK.Core
{
    public static class OpenPushNotificationTypes
    {
        public const int Subscribed = 1;
        public const int OptedOut = -2;
        public const int NoPermission = 0;
        public const int NeverPrompted = -18;
        public const int PromptUnanswered = -19;
        /// <summary>
        /// The bridge has not heard from the OS yet. Never sent on the wire and
        /// never stored: the core keeps its last known value instead.
        /// </summary>
        public const int Unknown = int.MinValue;
    }
}
