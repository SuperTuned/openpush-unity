using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Whether iOS exports get OpenPush's Live Activity widget extension, and the switch for it. A
    /// project with no settings asset gets none; a settings asset decides with
    /// <see cref="OpenPushSettings.InjectLiveActivityWidget"/>. A game without Live Activities is not
    /// misconfigured, so the row always passes and Run all never presses its button.
    /// </summary>
    public sealed class LiveActivityWidget : OpenPushSetupCheck
    {
        public override int Order => 50;
        public override string Title => "Live Activity widget";

        public override bool Passes() => true;

        public override string Detail
        {
            get
            {
                var settings = OpenPushSetupAssets.Loadable();
                if (settings == null)
                    return "Off: with no settings asset, iOS exports get no Live Activity widget. Create the "
                         + "settings asset (Settings asset row), then turn the widget on here if the game starts "
                         + "Live Activities with OpenPush's default widget.";
                return settings.InjectLiveActivityWidget
                    ? "On: iOS exports get OpenPush's Live Activity widget extension, which needs its own App ID "
                      + "(and a provisioning profile under manual signing). Turn it off if the game does not use "
                      + "Live Activities or already owns a widget extension."
                    : "Off: iOS exports get no Live Activity widget. Turn it on if the game starts Live Activities "
                      + "with OpenPush's default widget.";
            }
        }

        public override bool CanFix => OpenPushSetupAssets.Loadable() != null;

        public override string FixLabel
            => OpenPushSetupAssets.Loadable() is OpenPushSettings s && s.InjectLiveActivityWidget ? "Turn off" : "Turn on";

        public override void Fix()
        {
            var settings = OpenPushSetupAssets.Loadable();
            if (settings == null) return;
            Undo.RecordObject(settings, "OpenPush Live Activity widget");
            settings.InjectLiveActivityWidget = !settings.InjectLiveActivityWidget;
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }
}
