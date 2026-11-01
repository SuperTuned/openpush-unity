using System.Text;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// The one phone-number rule the Unity layer applies before a number reaches the platform. It is
    /// the server's own (<c>normalise_e164</c>), step for step, as both native SDKs apply it, so a
    /// number accepted here is the number the server stores: every character that is not an ASCII
    /// digit or <c>+</c> is removed (<c>+1 (415) 555-0123</c> is <c>+14155550123</c>); a leading
    /// <c>00</c> then becomes <c>+</c>; and what is left must be <c>+</c> followed by 8 to 15
    /// digits, the first of which is not <c>0</c>. A number without its country code is refused
    /// rather than guessed at: only the caller knows which country a local number belongs to.
    /// </summary>
    internal static class OpenPushPhoneNumber
    {
        internal const int MinimumDigits = 8;
        internal const int MaximumDigits = 15;

        /// <summary>Says what a refused number must look like. It never echoes the number itself, since it reaches logs.</summary>
        internal const string InvalidMessage =
            "not an E.164 phone number — use + (or 00), the country code and 8 to 15 digits in all, e.g. +14155550123";

        /// <summary>The E.164 form of <paramref name="raw"/>, or null when it is not a number OpenPush sends.</summary>
        internal static string Normalize(string raw)
        {
            if (raw == null) return null;
            var kept = new StringBuilder(raw.Length);
            foreach (var c in raw)
                if (c == '+' || (c >= '0' && c <= '9')) kept.Append(c);
            var number = kept.ToString();
            if (number.StartsWith("00", System.StringComparison.Ordinal)) number = "+" + number.Substring(2);
            if (number.Length == 0 || number[0] != '+') return null;
            var digits = number.Length - 1;
            if (digits < MinimumDigits || digits > MaximumDigits || number[1] == '0') return null;
            // Only digits may follow the leading +: a second + anywhere fails.
            return number.IndexOf('+', 1) < 0 ? number : null;
        }
    }
}
