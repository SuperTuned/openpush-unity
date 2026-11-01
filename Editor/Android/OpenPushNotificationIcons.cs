using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using OpenPushSDK.Core;

namespace OpenPushSDK.Editor
{
    internal enum OpenPushIconSource { None, OpenPush, Migrated, Reused }

    internal sealed class OpenPushNotificationIconReport
    {
        public OpenPushIconSource Small, Large;
        public bool Passes;
        public string Detail;
    }

    /// <summary>
    /// Which default notification icons the Android build will carry, in the order the native
    /// renderer tries them when a push names none: the OpenPush name, then the name a migrating
    /// project already bundles. Pure, so the Setup check stays a thin wrapper over asset paths.
    /// </summary>
    internal static class OpenPushNotificationIcons
    {
        internal const string StatusIcon = "ic_stat_openpush_default";
        internal const string LargeIcon = "ic_openpush_large_icon_default";

        // A file directly inside an Android res/drawable* or res/mipmap* folder (any qualifiers).
        private static readonly Regex ResourceFile =
            new Regex(@"(^|/)res/(drawable|mipmap)(-[^/]+)?/(?<name>[^/.]+)\.(png|webp|jpg|xml)$", RegexOptions.IgnoreCase);

        /// <param name="reusedIcon">The project's own status-bar icon reused by the Existing push setup answer, if any.</param>
        internal static OpenPushNotificationIconReport Inspect(OpenPushAndroidPushDelivery delivery, IEnumerable<string> assetPaths,
                                                               string reusedIcon = null)
        {
            if (delivery == OpenPushAndroidPushDelivery.Forward)
                return new OpenPushNotificationIconReport
                {
                    Passes = true,
                    Detail = "Forward: your FirebaseMessagingService draws notifications and chooses their icons.",
                };

            var names = new HashSet<string>(
                (assetPaths ?? Enumerable.Empty<string>())
                    .Select(p => ResourceFile.Match(p.Replace('\\', '/')))
                    .Where(m => m.Success)
                    .Select(m => m.Groups["name"].Value));
            var small = !string.IsNullOrWhiteSpace(reusedIcon) && names.Contains(reusedIcon.Trim())
                ? OpenPushIconSource.Reused : Source(names, StatusIcon, OpenPushMigratedIconNames.StatusIcon);
            var large = Source(names, LargeIcon, OpenPushMigratedIconNames.LargeIcon);
            return new OpenPushNotificationIconReport
            {
                Small = small,
                Large = large,
                Passes = small != OpenPushIconSource.None,
                Detail = (small == OpenPushIconSource.Reused
                    ? "Status-bar icon: your " + reusedIcon.Trim() + " (reused; written as ai.openpush.notification_icon)."
                    : (string.IsNullOrWhiteSpace(reusedIcon) ? "" : "The reused icon " + reusedIcon.Trim() + " is not in the project. ")
                      + SmallDetail(small)) + " " + LargeDetail(large),
            };
        }

        private static OpenPushIconSource Source(ISet<string> names, string openPush, string migrated)
            => names.Contains(openPush) ? OpenPushIconSource.OpenPush
             : names.Contains(migrated) ? OpenPushIconSource.Migrated
             : OpenPushIconSource.None;

        private static string SmallDetail(OpenPushIconSource source)
        {
            switch (source)
            {
                case OpenPushIconSource.OpenPush:
                    return "Status-bar icon: " + StatusIcon + ".";
                case OpenPushIconSource.Migrated:
                    return "Status-bar icon: your existing " + OpenPushMigratedIconNames.StatusIcon
                         + ", used as is (rename it to " + StatusIcon + " whenever convenient).";
                default:
                    return "No status-bar icon is bundled, so Android draws the app icon as a white square. Add a "
                         + "white-on-transparent PNG named " + StatusIcon + " to the res/drawable-* folders of an "
                         + ".androidlib library under Assets/Plugins/Android (one with its own AndroidManifest.xml).";
            }
        }

        private static string LargeDetail(OpenPushIconSource source)
        {
            switch (source)
            {
                case OpenPushIconSource.OpenPush:
                    return "Large icon: " + LargeIcon + ".";
                case OpenPushIconSource.Migrated:
                    return "Large icon: your existing " + OpenPushMigratedIconNames.LargeIcon + ", used as is.";
                default:
                    return "No default large icon (optional): pushes that do not set one show none. Add "
                         + LargeIcon + " to bundle one.";
            }
        }
    }
}
