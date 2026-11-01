using UnityEngine;
namespace OpenPushSDK.Editor
{
    /// <summary>
    /// The asset the runtime loads carries an SDK key. Without one — and without
    /// an explicit key in code — Initialize refuses rather than half-configuring
    /// the SDK, so this is the row that decides whether the game registers.
    /// </summary>
    public sealed class SdkKeyPresent : OpenPushSetupCheck
    {
        public override int Order => 30;
        public override string Title => "SDK key";

        public override bool Passes()
        {
            var settings = OpenPushSetupAssets.Loadable();
            return settings != null && !string.IsNullOrWhiteSpace(settings.SdkKey);
        }

        public override string Detail
        {
            get
            {
                var settings = OpenPushSetupAssets.Loadable();
                if (settings == null)
                    return "No loadable settings asset to read a key from. Fix the two rows "
                         + "above first.";
                if (!string.IsNullOrWhiteSpace(settings.SdkKey))
                    return "Set. The public SDK key is not a secret — it is ingest-only and "
                         + "ships inside the built player by design.";
                return "Empty. Copy the app id and SDK key from the OpenPush console and press Paste "
                     + "(it reads the two values, the openpush://setup link or the JSON), or pass the key "
                     + "in code with Initialize(appId, sdkKey), which wins over the asset.";
            }
        }

        // Paste: the console's copy (two values, a link or JSON) from the clipboard, or from the
        // OPENPUSH_SETUP environment variable in a batch run. Without either, the button reveals the asset.
        public override bool CanFix => true;
        // A passing row only reveals the asset: a working key is never overwritten from the clipboard.
        public override string FixLabel => OpenPushSetupAssets.Loadable() != null && !Passes() ? "Paste" : "Select";
        public override bool FixesItself => OpenPushSetupAssets.Loadable() != null && !Passes() && OpenPushPastedSetup.Available(out _);

        public override void Fix()
        {
            var settings = OpenPushSetupAssets.Loadable();
            if (settings == null || Passes()) { OpenPushSetupAssets.Reveal(); return; }
            if (!OpenPushPastedSetup.Apply(settings, out var problem))
            {
                Debug.LogWarning("OpenPush: " + problem);
                OpenPushSetupAssets.Reveal();
            }
        }
    }
}
