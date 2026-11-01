namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The default notification icon names a project migrating from OneSignal already bundles
    /// (OneSignalConfig.androidlib). The native Android renderer falls back to them after the
    /// OpenPush names, so the Setup check reports them as in use.
    /// </summary>
    internal static class OpenPushMigratedIconNames
    {
        internal const string StatusIcon = "ic_stat_onesignal_default";
        internal const string LargeIcon = "ic_onesignal_large_icon_default";
    }
}
