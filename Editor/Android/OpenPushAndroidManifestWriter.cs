using System.IO;
using OpenPushSDK.Core;
using UnityEditor.Android;
using UnityEngine;

namespace OpenPushSDK.Editor
{
    /// <summary>Writes the settings-derived manifest into the exported adapter library.</summary>
    internal sealed class OpenPushAndroidManifestWriter : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 10;

        public void OnPostGenerateGradleAndroidProject(string unityLibraryPath)
        {
            // Unity 2022.3 copies the library under unityLibrary/; newer players may place it beside it.
            var target = Path.Combine(unityLibraryPath, "OpenPushUnity.androidlib", "AndroidManifest.xml");
            if (!File.Exists(target))
            {
                var sibling = Path.Combine(unityLibraryPath, "..", "OpenPushUnity.androidlib", "AndroidManifest.xml");
                if (!File.Exists(sibling))
                {
                    Debug.LogWarning("OpenPush: exported project has no OpenPushUnity.androidlib at " + target);
                    return;
                }
                target = sibling;
            }
            var settings = OpenPushSetupAssets.Loadable();
            var input = new OpenPushAndroidManifestInput
            {
                AppId = settings?.AppId, SdkKey = settings?.SdkKey,
                FirebaseProjectId = settings?.AndroidFirebaseProjectId,
                FirebaseApplicationId = settings?.AndroidFirebaseApplicationId,
                FirebaseApiKey = settings?.AndroidFirebaseApiKey,
                Delivery = settings != null ? settings.AndroidPushDelivery : OpenPushAndroidPushDelivery.Automatic,
                SuppressLaunchUrls = settings != null && !settings.OpenLaunchUrls,
                NotificationIcon = settings != null && settings.IconsSource == OpenPushPlumbingSource.Reused ? settings.AndroidNotificationIconName : null,
            };
            File.WriteAllText(target, OpenPushAndroidManifestBuilder.Build(input));
        }
    }
}
