using OpenPushSDK.Core;
using UnityEditor;

namespace OpenPushSDK.Editor
{
    /// <summary>
    /// Assets → Create → OpenPush → Settings. A menu item rather than [CreateAssetMenu], so a new asset
    /// gets today's defaults (<see cref="OpenPushSettings.CreateWithCurrentDefaults"/>) while an existing
    /// asset's missing fields keep their earlier meaning.
    /// </summary>
    internal static class OpenPushSettingsMenu
    {
        [MenuItem("Assets/Create/OpenPush/Settings", priority = 0)]
        private static void Create()
            => ProjectWindowUtil.CreateAsset(OpenPushSettings.CreateWithCurrentDefaults(),
                                             OpenPushConfiguration.SettingsResourceName + ".asset");
    }
}
