using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// The phone-code result both native bridges send back for one call:
    /// <c>{"verification": {"id", "masked_number", "channel", "status", "attempts", "expires_at"}}</c>
    /// on success, <c>{"error": {"code", "message"}}</c> otherwise. Enum values travel as the server's
    /// snake_case words (an error code as the server sends it, so <see cref="OpenPushVerifyErrorCode.NoCredit"/>
    /// is <c>credit</c>); <c>expires_at</c> is Unix milliseconds or null.
    /// </summary>
    internal static class OpenPushVerifyWire
    {
        private static readonly string[] Channels = { "auto", "sms", "whatsapp", "call" };
        private static readonly string[] Statuses = { "pending", "approved", "expired", "max_attempts" };
        private static readonly string[] Codes =
            { "rate_limited", "too_soon", "not_pending", "provider", "refused", "invalid_number", "network",
              "opted_out", "credit", "no_rate" };

        internal static string Channel(OpenPushVerifyChannel channel) => Channels[(int)channel];

        internal static string Status(OpenPushVerificationStatus status) => Statuses[(int)status];

        internal static string Code(OpenPushVerifyErrorCode code) => Codes[(int)code];

        /// <summary>
        /// The typed result of one native answer. Anything unreadable, including the superseded
        /// payload a re-Initialize fails pending calls with (a plain string <c>error</c>), is a
        /// network error: no usable answer came back.
        /// </summary>
        internal static OpenPushVerifyResult Read(Dictionary<string, object> payload)
        {
            var verification = OpenPushWire.Obj(payload, "verification");
            if (verification != null)
            {
                var parsed = Verification(verification);
                return parsed != null
                    ? OpenPushVerifyResult.Ok(parsed)
                    : OpenPushVerifyResult.Failed(OpenPushVerifyErrorCode.Network, "the native SDK returned an unreadable verification");
            }
            var error = OpenPushWire.Obj(payload, "error");
            if (error != null)
                return OpenPushVerifyResult.Failed(CodeFrom(OpenPushWire.Str(error, "code")), OpenPushWire.Str(error, "message"));
            return OpenPushVerifyResult.Failed(OpenPushVerifyErrorCode.Network,
                OpenPushWire.Str(payload, "error") ?? "the native SDK returned no verification");
        }

        /// <summary>The payloads <see cref="Read"/> takes, as the bridges write them; for round-trip tests.</summary>
        internal static Dictionary<string, object> ToMap(OpenPushVerification v) => new Dictionary<string, object>
        {
            ["verification"] = new Dictionary<string, object>
            {
                ["id"] = v.Id, ["masked_number"] = v.MaskedNumber, ["channel"] = Channel(v.Channel),
                ["status"] = Status(v.Status), ["attempts"] = (double)v.Attempts,
                ["expires_at"] = v.ExpiresAt.HasValue ? (object)(double)v.ExpiresAt.Value.ToUnixTimeMilliseconds() : null,
            },
        };

        internal static Dictionary<string, object> ToMap(OpenPushVerifyException e) => new Dictionary<string, object>
        {
            ["error"] = new Dictionary<string, object> { ["code"] = Code(e.Code), ["message"] = e.Message },
        };

        private static OpenPushVerification Verification(Dictionary<string, object> o)
        {
            var id = OpenPushWire.Str(o, "id");
            var status = IndexOf(Statuses, OpenPushWire.Str(o, "status"));
            if (string.IsNullOrEmpty(id) || status < 0) return null;
            var channel = IndexOf(Channels, OpenPushWire.Str(o, "channel"));
            DateTimeOffset? expiresAt = null;
            if (o.TryGetValue("expires_at", out var raw) && raw != null)
                expiresAt = DateTimeOffset.FromUnixTimeMilliseconds(OpenPushWire.Long(o, "expires_at"));
            return new OpenPushVerification(id, OpenPushWire.Str(o, "masked_number"),
                channel < 0 ? OpenPushVerifyChannel.Auto : (OpenPushVerifyChannel)channel,
                (OpenPushVerificationStatus)status, OpenPushWire.Int(o, "attempts") ?? 0, expiresAt);
        }

        /// <summary>A code this SDK does not know is still a refusal, so a newer server cannot make it read as a transport failure.</summary>
        private static OpenPushVerifyErrorCode CodeFrom(string wire)
        {
            var index = IndexOf(Codes, wire);
            return index < 0 ? OpenPushVerifyErrorCode.Refused : (OpenPushVerifyErrorCode)index;
        }

        private static int IndexOf(string[] values, string wire) => wire == null ? -1 : Array.IndexOf(values, wire);
    }
}
