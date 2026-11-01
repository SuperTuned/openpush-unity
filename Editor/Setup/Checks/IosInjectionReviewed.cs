namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Surfaces the three switches that change what an iOS export contains.
    /// There is no right answer — it depends on what the host app already owns —
    /// so the row always passes and simply reports the current choices before a
    /// build makes them visible.
    /// </summary>
    public sealed class IosInjectionReviewed : OpenPushSetupCheck
    {
        public override int Order => 50;
        public override string Title => "iOS export switches";

        // Informational: a project that never ships iOS is not misconfigured.
        public override bool Passes() => true;

        public override string Detail
        {
            get
            {
                var settings = OpenPushSetupAssets.Loadable();
                var widget = settings == null ? "off (no settings asset)"
                    : settings.InjectLiveActivityWidget ? "on" : "off";
                var service = settings == null ? "unavailable (no settings asset)"
                    : settings.InjectServiceExtension ? "on, " + settings.IosExtensionMode : "off";
                var group = settings == null || string.IsNullOrWhiteSpace(settings.AppGroupIdentifier)
                    ? "none" : settings.AppGroupIdentifier.Trim();

                return "Live Activity widget " + widget + " (the Live Activity widget row turns it on; "
                     + "leave it off if the app already owns a widget extension). Notification service extension "
                     + service + " (on by default in new settings assets; it adds an App Group "
                     + "the App ID has to "
                     + "carry, and iOS runs only one such extension per app). App Group: "
                     + group + " (the service extension falls back to "
                     + "group.<bundle id>.openpush).";
            }
        }

        public override bool CanFix => true;
        public override string FixLabel => "Select";
        public override void Fix() => OpenPushSetupAssets.Reveal();
    }
}
