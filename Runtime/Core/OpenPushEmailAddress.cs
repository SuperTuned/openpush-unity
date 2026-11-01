namespace OpenPushSDK.Core
{
    /// <summary>
    /// The one email-address rule the Unity layer applies before an address reaches the platform, the
    /// same one both native SDKs apply: trimmed and lower-cased, then at most 254 characters with
    /// exactly one <c>@</c>, something before it and a dot in the domain. Anything else is refused.
    /// </summary>
    internal static class OpenPushEmailAddress
    {
        internal const int MaximumLength = 254;

        /// <summary>Says what a refused address must look like. It never echoes the address itself, since it reaches logs.</summary>
        internal const string InvalidMessage =
            "not an email address — use name@domain.tld, at most 254 characters";

        /// <summary>The cleaned form of <paramref name="raw"/>, or null when it is not an address OpenPush sends.</summary>
        internal static string Normalize(string raw)
        {
            if (raw == null) return null;
            var address = raw.Trim().ToLowerInvariant();
            if (address.Length == 0 || address.Length > MaximumLength) return null;
            var at = address.IndexOf('@');
            if (at < 1 || address.IndexOf('@', at + 1) >= 0) return null;
            return address.IndexOf('.', at + 1) < 0 ? null : address;
        }
    }
}
