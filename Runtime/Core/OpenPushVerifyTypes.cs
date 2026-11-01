namespace OpenPushSDK.Core
{
    /// <summary>How a phone code reaches the user. <see cref="Auto"/> lets the server pick among the app's enabled channels.</summary>
    public enum OpenPushVerifyChannel
    {
        Auto,
        Sms,
        WhatsApp,
        /// <summary>A voice call that reads the code out.</summary>
        Call,
    }

    /// <summary>Where an <see cref="OpenPushVerification"/> stands. Only <see cref="Pending"/> accepts another check or a resend.</summary>
    public enum OpenPushVerificationStatus
    {
        /// <summary>A code is out and not yet matched. A wrong code also leaves the verification pending.</summary>
        Pending,
        /// <summary>The code matched. The server has marked this number's SMS and WhatsApp subscriptions subscribed.</summary>
        Approved,
        /// <summary>The code was not entered in time. Start a new verification.</summary>
        Expired,
        /// <summary>Too many wrong codes. Start a new verification.</summary>
        MaxAttempts,
    }

    /// <summary>Why a phone-code call did not produce an <see cref="OpenPushVerification"/>.</summary>
    public enum OpenPushVerifyErrorCode
    {
        /// <summary>Too many codes for this number or this app; wait before starting again.</summary>
        RateLimited,
        /// <summary>A resend came less than 30 seconds after the last send.</summary>
        TooSoon,
        /// <summary>The verification is no longer pending: approved, expired or out of attempts.</summary>
        NotPending,
        /// <summary>The SMS, WhatsApp or voice provider could not deliver the code.</summary>
        Provider,
        /// <summary>The server refused the request: an unknown verification, a disabled channel, a bad body, or a refusal code this SDK does not know.</summary>
        Refused,
        /// <summary>The number is not E.164. Raised on the device; nothing was sent.</summary>
        InvalidNumber,
        /// <summary>No usable answer: the request did not reach the server, or the server failed without saying why.</summary>
        Network,
        /// <summary>The number replied STOP or has opted out of texts (HTTP 409). Never retry.</summary>
        OptedOut,
        /// <summary>The workspace has no text credit left (HTTP 402).</summary>
        NoCredit,
        /// <summary>Texts to this number's country are not priced or not allowed (HTTP 409).</summary>
        NoRate,
    }
}
