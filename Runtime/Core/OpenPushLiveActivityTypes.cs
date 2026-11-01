namespace OpenPushSDK.Core
{
    /// <summary>Options for <c>LiveActivities.SetupDefault</c>; the name matches the compatibility API's options type, so migrating apps compile unchanged.</summary>
    public sealed class LiveActivitySetupOptions
    {
        public bool EnablePushToStart { get; set; } = true;
        public bool EnablePushToUpdate { get; set; } = true;
    }

    public static class OpenPushLiveActivityNotes
    {
        public const string IOSOnly =
            "Live Activities are iOS-only (ActivityKit). On Android use "
            + "OpenPush.LiveUpdates instead; it is a different feature, not an alias.";
    }
}
