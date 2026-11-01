using System.Collections.Generic;
using System.IO;
using System.Linq;
using OpenPushSDK.Core;
using UnityEditor;
namespace OpenPushSDK.Editor
{
    /// <summary>Reports which bundled Android notification icons the build will use. Never moves files.</summary>
    public sealed class NotificationIconsPresent : OpenPushSetupCheck
    {
        public override int Order => 65;
        public override string Title => "Android notification icons";

        private static readonly OpenPushStampedCache<OpenPushNotificationIconReport> Cache = new OpenPushStampedCache<OpenPushNotificationIconReport>();

        /// Searches the asset database and walks every .androidlib folder, so it is cached until an asset
        /// changes or the delivery mode or reused icon in the settings does; Passes and Detail share it.
        private static OpenPushNotificationIconReport Report()
        {
            var settings = OpenPushSetupAssets.Loadable();
            var delivery = settings != null ? settings.AndroidPushDelivery : OpenPushAndroidPushDelivery.Automatic;
            var reused = settings != null && settings.IconsSource == OpenPushPlumbingSource.Reused ? settings.AndroidNotificationIconName : null;
            return Cache.Get(() => Scan(delivery, reused), (delivery, reused));
        }

        private static OpenPushNotificationIconReport Scan(OpenPushAndroidPushDelivery delivery, string reused)
        {
            var names = new[]
            {
                OpenPushNotificationIcons.StatusIcon, OpenPushNotificationIcons.LargeIcon,
                OpenPushMigratedIconNames.StatusIcon, OpenPushMigratedIconNames.LargeIcon,
            }.Concat(string.IsNullOrWhiteSpace(reused) ? new string[0] : new[] { reused.Trim() }).ToArray();
            var paths = names.SelectMany(name => AssetDatabase.FindAssets(name))
                             .Select(AssetDatabase.GUIDToAssetPath)
                             .Concat(AndroidLibraryFiles(names))
                             .Distinct();
            return OpenPushNotificationIcons.Inspect(delivery, paths, reused);
        }

        /// The files inside .androidlib folders too, which Unity hands to Gradle whether or not it
        /// lists them as assets.
        private static IEnumerable<string> AndroidLibraryFiles(string[] names)
        {
            try
            {
                return Directory.GetDirectories("Assets", "*.androidlib", SearchOption.AllDirectories)
                    .SelectMany(lib => names.SelectMany(name =>
                        Directory.EnumerateFiles(lib, name + ".*", SearchOption.AllDirectories)))
                    .ToList();
            }
            catch (IOException) { return Enumerable.Empty<string>(); }
            catch (System.UnauthorizedAccessException) { return Enumerable.Empty<string>(); }
        }

        public override bool Passes() => Report().Passes;
        public override string Detail => Report().Detail;
    }
}
