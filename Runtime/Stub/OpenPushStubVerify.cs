using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// Phone codes in the Editor and desktop players: nothing is sent, and <see cref="Code"/> is the
    /// one code that matches. Otherwise it follows the server's rules closely enough to build a code
    /// screen against: a wrong code stays pending and counts an attempt, the fifth wrong code ends
    /// the verification, and a finished one refuses check and resend as not pending. Answers
    /// synchronously, on the calling thread.
    /// </summary>
    internal sealed class OpenPushStubVerify : IOpenPushVerify
    {
        internal const string Code = "123456";
        internal const int MaxAttempts = 5;
        internal static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

        private readonly object _gate = new object();
        private readonly Dictionary<string, OpenPushVerification> _held = new Dictionary<string, OpenPushVerification>(StringComparer.Ordinal);
        private readonly Action<string> _trace;
        private int _next;

        internal OpenPushStubVerify(Action<string> trace) { _trace = trace ?? (_ => { }); }

        public void Start(string phone, OpenPushVerifyChannel channel, string locale, Action<OpenPushVerifyResult> done)
        {
            _trace("verify.start (stub: nothing is sent; the code is " + Code + ")");
            var sentOn = channel == OpenPushVerifyChannel.Auto ? OpenPushVerifyChannel.Sms : channel;
            OpenPushVerification started;
            lock (_gate)
            {
                started = new OpenPushVerification("vf_stub_" + ++_next, Masked(phone), sentOn,
                    OpenPushVerificationStatus.Pending, 0, DateTimeOffset.UtcNow + Lifetime);
                _held[started.Id] = started;
            }
            done?.Invoke(OpenPushVerifyResult.Ok(started));
        }

        public void Check(string verificationId, string code, Action<OpenPushVerifyResult> done)
        {
            _trace("verify.check");
            done?.Invoke(Advance(verificationId, held =>
            {
                if (code == Code) return With(held, OpenPushVerificationStatus.Approved, held.Attempts + 1, held.Channel);
                var attempts = held.Attempts + 1;
                return With(held, attempts >= MaxAttempts ? OpenPushVerificationStatus.MaxAttempts : OpenPushVerificationStatus.Pending,
                            attempts, held.Channel);
            }));
        }

        public void Resend(string verificationId, OpenPushVerifyChannel channel, Action<OpenPushVerifyResult> done)
        {
            _trace("verify.resend");
            var sentOn = channel == OpenPushVerifyChannel.Auto ? OpenPushVerifyChannel.Sms : channel;
            done?.Invoke(Advance(verificationId, held => With(held, OpenPushVerificationStatus.Pending, held.Attempts, sentOn)));
        }

        /// <summary>Applies <paramref name="next"/> to a held, still pending, unexpired verification.</summary>
        private OpenPushVerifyResult Advance(string verificationId, Func<OpenPushVerification, OpenPushVerification> next)
        {
            lock (_gate)
            {
                if (verificationId == null || !_held.TryGetValue(verificationId, out var held))
                    return OpenPushVerifyResult.Failed(OpenPushVerifyErrorCode.Refused, "unknown verification — start a new one");
                if (held.Status == OpenPushVerificationStatus.Pending && held.ExpiresAt <= DateTimeOffset.UtcNow)
                    _held[held.Id] = held = With(held, OpenPushVerificationStatus.Expired, held.Attempts, held.Channel);
                if (held.Status != OpenPushVerificationStatus.Pending)
                    return OpenPushVerifyResult.Failed(OpenPushVerifyErrorCode.NotPending, "this verification is " + held.Status + " — start a new one");
                var advanced = next(held);
                _held[advanced.Id] = advanced;
                return OpenPushVerifyResult.Ok(advanced);
            }
        }

        private static OpenPushVerification With(OpenPushVerification v, OpenPushVerificationStatus status, int attempts,
                                                  OpenPushVerifyChannel channel)
            => new OpenPushVerification(v.Id, v.MaskedNumber, channel, status, attempts, v.ExpiresAt);

        /// <summary>The server's mask: the first five characters, then the last four.</summary>
        internal static string Masked(string number)
            => number == null ? "" : number.Length <= 8 ? number.Substring(0, Math.Min(2, number.Length)) + "•••"
             : number.Substring(0, 5) + "•••" + number.Substring(number.Length - 4);
    }
}
