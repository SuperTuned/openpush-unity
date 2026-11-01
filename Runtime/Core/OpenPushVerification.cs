using System;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// One phone code, as the server last described it. Pass it back to
    /// <c>OpenPush.Verify.CheckAsync</c> and <c>ResendAsync</c>; each returns a new value and leaves
    /// this one unchanged. The number is only ever shown masked: the native SDK keeps the full number
    /// in memory for check and resend, keyed by <see cref="Id"/>, and never persists it, so a
    /// verification does not survive the app process. Start a new one after a relaunch.
    /// </summary>
    public sealed class OpenPushVerification
    {
        internal OpenPushVerification(string id, string maskedNumber, OpenPushVerifyChannel channel,
                                      OpenPushVerificationStatus status, int attempts, DateTimeOffset? expiresAt)
        {
            Id = id;
            MaskedNumber = maskedNumber ?? "";
            Channel = channel;
            Status = status;
            Attempts = attempts;
            ExpiresAt = expiresAt;
        }

        /// <summary>The server's verification id (<c>vf_…</c>).</summary>
        public string Id { get; }

        /// <summary>The number with its middle digits hidden, for example <c>+1415•••0123</c>.</summary>
        public string MaskedNumber { get; }

        /// <summary>The channel the code last went out on.</summary>
        public OpenPushVerifyChannel Channel { get; }

        public OpenPushVerificationStatus Status { get; }

        /// <summary>Codes checked so far.</summary>
        public int Attempts { get; }

        /// <summary>When the code stops being accepted; null when the server did not say.</summary>
        public DateTimeOffset? ExpiresAt { get; }

        public override string ToString()
            => "OpenPushVerification(Id=" + Id + ", MaskedNumber=" + MaskedNumber + ", Channel=" + Channel
             + ", Status=" + Status + ", Attempts=" + Attempts + ", ExpiresAt=" + ExpiresAt?.ToString("o") + ")";
    }
}
