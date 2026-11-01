namespace OpenPushSDK.Core
{
    /// <summary>
    /// Outcome of a Login or Logout. A rejected identity is deliberately not a
    /// success and not a failure either: the device *is* registered, but
    /// anonymously, and the note says why the external id was refused.
    /// Collapsing that into success would hide a broken identity-verification
    /// setup until a campaign silently missed its audience.
    /// </summary>
    public sealed class OpenPushIdentityResult
    {
        public bool Success { get; }
        /// <summary>Non-null means the device registered anonymously.</summary>
        public string IdentityRejectedNote { get; }
        public string Error { get; }

        private OpenPushIdentityResult(bool success, string note, string error)
        {
            Success = success;
            IdentityRejectedNote = note;
            Error = error;
        }

        public static OpenPushIdentityResult Ok()
            => new OpenPushIdentityResult(true, null, null);

        public static OpenPushIdentityResult Rejected(string note)
            => new OpenPushIdentityResult(false,
                string.IsNullOrEmpty(note) ? "the server refused this external id" : note,
                null);

        public static OpenPushIdentityResult Failed(string message)
            => new OpenPushIdentityResult(false, null,
                string.IsNullOrEmpty(message)
                    ? "OpenPush could not complete this request."
                    : message);
    }
}
