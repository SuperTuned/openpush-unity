namespace OpenPushSDK.Core
{
    /// <summary>What an iOS export does about the notification service extension (iOS runs one per app).</summary>
    public enum OpenPushIosExtensionMode
    {
        /// <summary>Add OpenPush's own extension. The default, for a project with no extension of its own.</summary>
        Inject = 0,
        /// <summary>
        /// Reuse an extension a previous push SDK created: its principal class becomes OpenPush's handler, and its
        /// bundle id, signing and entitlements are kept. The original source is backed up.
        /// </summary>
        TakeOver = 1,
        /// <summary>
        /// Keep your own extension's code and add OpenPush's lines to it: OpenPush handles its own pushes and
        /// passes every other one to your code.
        /// </summary>
        Share = 2,
        /// <summary>Change nothing. OpenPush pushes then show no images or buttons and report no delivery receipts.</summary>
        Off = 3,
    }

    /// <summary>Whether something a previous push SDK left in the project is reused or replaced.</summary>
    public enum OpenPushPlumbingSource
    {
        /// <summary>OpenPush's own (the default).</summary>
        New = 0,
        /// <summary>The one already in the project.</summary>
        Reused = 1,
    }
}
