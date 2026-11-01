using System;
using System.Threading.Tasks;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    public static partial class OpenPush
    {
        /// <summary>
        /// Phone codes: send a one-time code to a number, then check what the user typed. An approved
        /// code is what makes the number's SMS and WhatsApp subscriptions subscribed on the server.
        /// Each call is one request with no retry, because a person is waiting on it. A failure faults
        /// the task with an <see cref="OpenPushVerifyException"/>; every task completes on the Unity
        /// main thread. In the Editor and desktop players nothing is sent: the in-memory stub accepts
        /// the code <c>123456</c>.
        /// </summary>
        public static class Verify
        {
            /// <summary>
            /// Sends a code to <paramref name="phone"/> over <paramref name="channel"/>, normalised as
            /// <see cref="User.AddSms"/> normalises it; a number that is not E.164 after that faults with <see cref="OpenPushVerifyErrorCode.InvalidNumber"/> before anything is
            /// sent. <paramref name="locale"/> (<c>"en"</c>, <c>"pt-BR"</c>) picks the message language;
            /// null leaves it to the server.
            /// </summary>
            public static Task<OpenPushVerification> StartAsync(string phone,
                                                                OpenPushVerifyChannel channel = OpenPushVerifyChannel.Auto,
                                                                string locale = null)
            {
                RequireInitialized("Verify.StartAsync");
                var number = OpenPushPhoneNumber.Normalize(phone);
                if (number == null)
                    return Task.FromException<OpenPushVerification>(
                        new OpenPushVerifyException(OpenPushVerifyErrorCode.InvalidNumber, OpenPushPhoneNumber.InvalidMessage));
                var trimmedLocale = string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();
                return Answer(done => Platform.Verify.Start(number, channel, trimmedLocale, done));
            }

            /// <summary>
            /// Checks <paramref name="code"/> against <paramref name="verification"/>. A wrong code is not
            /// an error: it comes back still <see cref="OpenPushVerificationStatus.Pending"/> with
            /// <see cref="OpenPushVerification.Attempts"/> counted up, so read the returned status.
            /// </summary>
            public static Task<OpenPushVerification> CheckAsync(OpenPushVerification verification, string code)
            {
                RequireInitialized("Verify.CheckAsync");
                if (verification == null) throw new ArgumentNullException(nameof(verification));
                var trimmedCode = code?.Trim() ?? "";
                return Answer(done => Platform.Verify.Check(verification.Id, trimmedCode, done));
            }

            /// <summary>
            /// Sends the code again over <paramref name="channel"/>, or over the channel it last went out
            /// on when null; pass <see cref="OpenPushVerifyChannel.Call"/> for a user whose texts are not
            /// arriving. The server allows a resend 30 seconds after the last send; sooner faults with
            /// <see cref="OpenPushVerifyErrorCode.TooSoon"/>.
            /// </summary>
            public static Task<OpenPushVerification> ResendAsync(OpenPushVerification verification,
                                                                 OpenPushVerifyChannel? channel = null)
            {
                RequireInitialized("Verify.ResendAsync");
                if (verification == null) throw new ArgumentNullException(nameof(verification));
                var resolved = channel ?? verification.Channel;
                return Answer(done => Platform.Verify.Resend(verification.Id, resolved, done));
            }

            /// <summary>Turns one platform answer into a task completed on the main thread.</summary>
            private static Task<OpenPushVerification> Answer(Action<Action<OpenPushVerifyResult>> call)
            {
                var completion = new TaskCompletionSource<OpenPushVerification>();
                call(result => OpenPushHost.Dispatcher.Post(() =>
                {
                    if (result?.Verification != null) completion.TrySetResult(result.Verification);
                    else completion.TrySetException(result?.Error
                        ?? new OpenPushVerifyException(OpenPushVerifyErrorCode.Network, "the native SDK returned no verification"));
                }));
                return completion.Task;
            }
        }
    }
}
