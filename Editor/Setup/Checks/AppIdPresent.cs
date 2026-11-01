namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The app id in the settings asset. Unlike the SDK key this is genuinely
    /// optional — a host that calls <c>Initialize(appId)</c> in code never needs
    /// it — so the row always passes and the detail explains which case applies.
    /// </summary>
    public sealed class AppIdPresent : OpenPushSetupCheck
    {
        public override int Order => 40;
        public override string Title => "App id (optional)";

        // Pass-with-note: an empty app id is a valid, common project shape.
        public override bool Passes() => true;

        public override string Detail
        {
            get
            {
                var settings = OpenPushSetupAssets.Loadable();
                if (settings == null)
                    return "Optional. Not read yet — there is no loadable settings asset. "
                         + "It is only needed by hosts that would rather not repeat the app id "
                         + "in code.";
                if (!string.IsNullOrWhiteSpace(settings.AppId))
                    return "Set to " + settings.AppId.Trim() + ". An app id passed to "
                         + "Initialize(appId) in code wins over this.";
                return "Empty, which is fine when the app id is passed in code with "
                     + "Initialize(appId). Fill it in only to leave the Bootstrap component's "
                     + "App Id blank.";
            }
        }

        public override bool CanFix => true;
        public override string FixLabel => "Select";
        public override void Fix() => OpenPushSetupAssets.Reveal();
    }
}
