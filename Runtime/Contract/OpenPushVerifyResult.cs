using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>What one phone-code call answered: exactly one of a verification or an error.</summary>
    internal sealed class OpenPushVerifyResult
    {
        private OpenPushVerifyResult(OpenPushVerification verification, OpenPushVerifyException error)
        {
            Verification = verification;
            Error = error;
        }

        public OpenPushVerification Verification { get; }
        public OpenPushVerifyException Error { get; }

        public static OpenPushVerifyResult Ok(OpenPushVerification verification)
            => verification == null ? Failed(OpenPushVerifyErrorCode.Network, null) : new OpenPushVerifyResult(verification, null);

        public static OpenPushVerifyResult Failed(OpenPushVerifyErrorCode code, string message)
            => new OpenPushVerifyResult(null, new OpenPushVerifyException(code, message));
    }
}
