using System.Collections.Generic;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Push plumbing the project already has, from any framework: one row per thing found, each asking Reuse
    /// (the default) or Create new. The answer is stored in OpenPushSettings, which builds read, so exports and
    /// batch builds never ask; an answered row shows it with a Change button. Nothing found, one passing row.
    /// </summary>
    public sealed class ExistingPushPlumbing : OpenPushSetupCheck
    {
        public override int Order => 1;
        public override string Title => "Existing push setup";
        public override bool Passes() => true;
        public override string Detail => "Push setup already in this project.";

        private static KeyValuePair<string, System.Action> Button(string label, System.Action run)
            => OpenPushSetupItem.Button(label, () => { run(); OpenPushExistingPlumbing.Invalidate(); });

        /// <summary>An unanswered row: Run all takes its first button, Reuse, the default answer.</summary>
        private static OpenPushSetupItem Ask(string title, string detail, params KeyValuePair<string, System.Action>[] buttons)
            => new OpenPushSetupItem(title, detail, false, buttons) { RunAll = buttons[0].Value };

        public override IReadOnlyList<OpenPushSetupItem> Items()
        {
            var settings = OpenPushSetupAssets.Loadable();
            if (settings == null)
                return new[] { new OpenPushSetupItem(Title, "Create the settings asset first; existing push setup is then checked here.", true) };
            var found = OpenPushExistingPlumbing.Detect();
            var file = OpenPushPlumbingAnswers.File();
            var items = new List<OpenPushSetupItem>();

            var extension = found.FirstOrDefault(f => f.Kind == OpenPushSetupFinding.ServiceExtension);
            if (extension != null) items.Add(ServiceExtension(settings, extension, file));
            var groups = found.Where(f => f.Kind == OpenPushSetupFinding.AppGroup).ToList();
            if (groups.Count > 0) items.Add(AppGroup(settings, groups, extension, file));
            var services = found.Where(f => f.Kind == OpenPushSetupFinding.FcmService).ToList();
            if (services.Count > 0) items.Add(Delivery(settings, services, file));
            var icons = found.Where(f => f.Kind == OpenPushSetupFinding.NotificationIcon).ToList();
            if (icons.Count > 0) items.Add(Icons(settings, icons, file));

            if (items.Count == 0)
                items.Add(new OpenPushSetupItem(Title, "Nothing found from your previous push SDK or your own push code: no notification "
                    + "service extension or App Group (in the last iOS export or in Assets), and no other Android messaging service.", true));
            return items;
        }

        private static OpenPushSetupItem ServiceExtension(OpenPushSettings settings, OpenPushSetupFinding extension, OpenPushSetupFile file)
        {
            var reuse = extension.Vendor != null ? OpenPushIosExtensionMode.TakeOver : OpenPushIosExtensionMode.Share;
            var title = "iOS notification service extension: " + extension.Id;
            // A hand-written file under Assets/Plugins/iOS names no target; Reuse then applies to whichever
            // service extension the export has.
            var target = extension.Flag("hand_written") ? null : extension.Id;
            if (!settings.InjectServiceExtension)
                return new OpenPushSetupItem(title, "OpenPush's extension is turned off (InjectServiceExtension), so " + extension.Id
                    + " is left as it is. Turn it on to Reuse it.", true);
            var mode = settings.IosExtensionMode;
            var answered = file.HasChoice(OpenPushPlumbingAnswers.ServiceExtensionChoice)
                && (mode == OpenPushIosExtensionMode.Off
                    || (mode == OpenPushIosExtensionMode.TakeOver || mode == OpenPushIosExtensionMode.Share)
                       && (string.IsNullOrWhiteSpace(settings.IosExtensionTarget) || settings.IosExtensionTarget == target));
            if (answered)
                return new OpenPushSetupItem(title, "Stored: " + (mode == OpenPushIosExtensionMode.Off
                        ? "Off. " + extension.Id + " is left as it is; OpenPush pushes show no images or buttons and report no delivery receipts."
                        : mode + " " + extension.Id + (mode == OpenPushIosExtensionMode.TakeOver
                            ? ": its code becomes OpenPush's handler at export, keeping its App ID, App Group and profile."
                            : ": OpenPush's lines are added first in its code at export; the rest is kept.")),
                    true, Button("Change", () => OpenPushPlumbingAnswers.Forget(OpenPushPlumbingAnswers.ServiceExtensionChoice)));

            var bundle = extension.Detail("bundle_id");
            var who = extension.Vendor != null
                ? extension.Id + (bundle != null ? " (" + bundle + ")" : "") + " comes from your previous push SDK. Reuse makes it OpenPush's "
                  + "extension and keeps its App ID, App Group and provisioning profile."
                : extension.Id + (bundle != null ? " (" + bundle + ")" : "") + " has code that is not a known push SDK template. Reuse keeps "
                  + "that code and adds OpenPush's lines first in it (the original is backed up at export).";
            if (extension.Flag("from_build_step"))
                who += " Your previous push SDK is still installed and its build step adds this extension at export: remove the "
                     + "previous SDK first (Previous push SDK row); a clean export then gives OpenPush's extension these identifiers.";
            return Ask(title, who + " iOS runs one service extension per app, so OpenPush cannot add its own beside it; "
                    + "Off leaves it alone and loses images, buttons and delivery receipts for OpenPush pushes.",
                Button("Reuse", () => OpenPushPlumbingAnswers.SetServiceExtension(settings, reuse, target, bundle, Profile(extension))),
                Button("Off", () => OpenPushPlumbingAnswers.SetServiceExtension(settings, OpenPushIosExtensionMode.Off, null, null, null)));
        }

        /// <summary>The extension's profile from the last export (Release first; specifier or UUID).</summary>
        private static string Profile(OpenPushSetupFinding extension)
        {
            if (extension.Detail("profile") is string recorded) return recorded;
            if (!(extension.Details.TryGetValue("configurations", out var c) && c is Dictionary<string, object> configurations)) return null;
            return configurations.OrderBy(p => p.Key == "Release" ? 0 : 1)
                .Select(p => p.Value is Dictionary<string, object> s
                    ? (s.TryGetValue("PROVISIONING_PROFILE_SPECIFIER", out var v) && v is string spec ? spec
                       : s.TryGetValue("PROVISIONING_PROFILE", out var u) ? u as string : null) : null)
                .FirstOrDefault(v => !string.IsNullOrEmpty(v));
        }

        private static OpenPushSetupItem AppGroup(OpenPushSettings settings, List<OpenPushSetupFinding> groups,
                                                  OpenPushSetupFinding extension, OpenPushSetupFile file)
        {
            // The group in use is the one on both the app and its extension; with no extension, the app's.
            var shared = groups.Where(g => g.Flag("on_app") && g.Strings("on_extensions").Count > 0).ToList();
            var candidates = shared.Count > 0 ? shared : groups.Where(g => g.Flag("on_app")).ToList();
            var single = candidates.Count == 1 ? candidates[0].Id : null;
            var names = string.Join(", ", groups.Select(g => g.Id));
            var title = "iOS App Group: " + (single ?? names);
            if (file.HasChoice(OpenPushPlumbingAnswers.AppGroupChoice))
                return new OpenPushSetupItem(title, "Stored: " + (settings.AppGroupSource == OpenPushPlumbingSource.Reused
                        ? "Reuse " + (OpenPushIOSIdentifiers.Optional(settings.AppGroupIdentifier) ?? "the group the app and its extension share")
                        : "Create new, " + (OpenPushIOSIdentifiers.Optional(settings.AppGroupIdentifier) ?? "group.<bundle id>.openpush")) + ".",
                    true, Button("Change", () => OpenPushPlumbingAnswers.Forget(OpenPushPlumbingAnswers.AppGroupChoice)));
            var from = groups.Any(g => g.Vendor != null) ? " (from your previous push SDK)" : "";
            var detail = (single != null
                    ? single + from + " is already on the app" + (extension != null ? " and " + extension.Id : "") + ". Reuse: OpenPush shares it, "
                      + "so there is nothing new to provision."
                    : "Several App Groups (" + names + ")" + from + ". Reuse uses the one the app and its extension share at export; "
                      + "name it in AppGroupIdentifier if they share more than one.")
                + " Create new: group.<bundle id>.openpush, which the App ID needs before a manual-signing build.";
            return Ask(title, detail,
                Button("Reuse", () => OpenPushPlumbingAnswers.SetAppGroup(settings, OpenPushPlumbingSource.Reused, single)),
                Button("Create new", () => OpenPushPlumbingAnswers.SetAppGroup(settings, OpenPushPlumbingSource.New, single)));
        }

        private static OpenPushSetupItem Delivery(OpenPushSettings settings, List<OpenPushSetupFinding> services, OpenPushSetupFile file)
        {
            var service = services[0];
            var names = string.Join(", ", services.Select(s => s.Id));
            var title = "Android messaging service: " + names;
            if (file.HasChoice(OpenPushPlumbingAnswers.AndroidDeliveryChoice))
                return new OpenPushSetupItem(title, "Stored: " + (settings.AndroidPushDelivery == OpenPushAndroidPushDelivery.Automatic
                        ? "Create new (Automatic): OpenPush's own service receives pushes. Remove " + names + " from the build, or pushes are split between them."
                        : "Reuse (Forward): " + names + " receives every push and forwards OpenPush's."),
                    true, Button("Change", () => OpenPushPlumbingAnswers.Forget(OpenPushPlumbingAnswers.AndroidDeliveryChoice)));
            var where = service.Detail("source_file") != null ? " (your code: " + service.Detail("source_file") + ")"
                      : service.Editable ? "" : " (from a package)";
            return Ask(title, names + where + " already receives FCM pushes. Reuse (Forward): it keeps receiving every "
                    + "push; call OpenPush.Register(token) on each new token and Notifications.ProcessForeground(data), ProcessClick(data) "
                    + "and ProcessDisplayed(data) from it. Create new (Automatic): OpenPush's own service receives pushes instead.",
                Button("Reuse", () => { OpenPushPlumbingAnswers.SetAndroidDelivery(settings, OpenPushAndroidPushDelivery.Forward, service.Id); OpenPushAndroidDependencies.WriteNow(); }),
                Button("Create new", () => { OpenPushPlumbingAnswers.SetAndroidDelivery(settings, OpenPushAndroidPushDelivery.Automatic, service.Id); OpenPushAndroidDependencies.WriteNow(); }));
        }

        private static OpenPushSetupItem Icons(OpenPushSettings settings, List<OpenPushSetupFinding> icons, OpenPushSetupFile file)
        {
            var names = string.Join(", ", icons.Select(i => i.Id));
            var title = "Android notification icons: " + names;
            if (file.HasChoice(OpenPushPlumbingAnswers.IconsChoice))
                return new OpenPushSetupItem(title, "Stored: " + (settings.IconsSource == OpenPushPlumbingSource.Reused
                        ? "Reuse " + icons[0].Id + "." : "Create new (OpenPush's own icon names)."),
                    true, Button("Change", () => OpenPushPlumbingAnswers.Forget(OpenPushPlumbingAnswers.IconsChoice)));
            return Ask(title, names + " " + (icons.Count == 1 ? "is" : "are") + " already in the project. Reuse: keep "
                    + (icons.Count == 1 ? "it" : "them") + " for OpenPush notifications. Create new: OpenPush's own names (ic_stat_openpush_default).",
                Button("Reuse", () => OpenPushPlumbingAnswers.SetIcons(settings, OpenPushPlumbingSource.Reused, icons[0].Id)),
                Button("Create new", () => OpenPushPlumbingAnswers.SetIcons(settings, OpenPushPlumbingSource.New, null)));
        }
    }
}
