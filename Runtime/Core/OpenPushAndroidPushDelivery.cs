namespace OpenPushSDK.Core
{
    /// <summary>Who owns FCM on Android.</summary>
    public enum OpenPushAndroidPushDelivery
    {
        /// <summary>The OpenPush FCM module fetches the token, renders and handles taps. No host code.</summary>
        Automatic = 0,
        /// <summary>
        /// The host's own FirebaseMessagingService (for example Firebase Unity Messaging) receives every push
        /// and forwards tokens and payloads to OpenPush. OpenPush's messaging service is removed, and the
        /// native ai.openpush.fcm.delivery value is "forward", so it can never receive a push as well.
        /// </summary>
        Forward = 1,
        /// <summary>Another name for <see cref="Forward"/>; the same value, so settings and code that use it keep working.</summary>
        HostOwned = Forward,
    }
}
