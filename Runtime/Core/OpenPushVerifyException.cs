using System;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// Why a phone-code call did not produce an <see cref="OpenPushVerification"/>. Branch on
    /// <see cref="Code"/>; <see cref="Exception.Message"/> is the server's own sentence when it sent
    /// one and the SDK's otherwise, fit to log but not written for end users.
    /// </summary>
    public sealed class OpenPushVerifyException : Exception
    {
        public OpenPushVerifyException(OpenPushVerifyErrorCode code, string message)
            : base(string.IsNullOrEmpty(message) ? "the phone-code request failed" : message)
        {
            Code = code;
        }

        public OpenPushVerifyErrorCode Code { get; }

        public override string ToString() => "OpenPushVerifyException(" + Code + "): " + Message;
    }
}
