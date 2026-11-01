using OpenPushSDK.Core;
using UnityEngine;

namespace OpenPushSDK
{
    /// <summary>
    /// Drop-in startup: add this to any object in the first scene and OpenPush
    /// initializes from the Inspector, with no code in the host project.
    ///
    /// Hosts that already call <see cref="OpenPush.Initialize(string)"/> themselves
    /// do not need it; it is the no-code path, not a required component.
    /// </summary>
    [AddComponentMenu("OpenPush/Bootstrap")]
    public sealed class OpenPushBootstrap : MonoBehaviour
    {
        [Tooltip("Leave empty to use OpenPushSettings.AppId from Resources.")]
        public string AppId;

        [Tooltip("Ask for notification permission as soon as the scene starts. Leave "
               + "this off to prompt at a moment that makes sense in your game.")]
        public bool RequestPermissionOnStart;

        [Tooltip("Route OpenPush's log line to the Unity console.")]
        public bool LogToConsole = true;

        [Tooltip("How much of the log to route. Verbose is everything; lower it for a "
               + "shipping build. Applied only when Log To Console is on.")]
        public OpenPushLogLevel LogLevel = OpenPushLogLevel.Verbose;

        private void Start()
        {
            if (LogToConsole) { OpenPush.OnLog = Debug.Log; OpenPush.LogLevel = LogLevel; }
            var appId = string.IsNullOrEmpty(AppId) ? OpenPushSettings.Load()?.AppId : AppId;
            if (string.IsNullOrEmpty(appId))
            {
                Debug.LogError("OpenPushBootstrap: set App Id or fill OpenPushSettings.AppId");
                return;
            }
            if (!OpenPush.StartFromBootstrap(appId, RequestPermissionOnStart))
                Debug.LogError("OpenPushBootstrap: OpenPush.Initialize was refused (no SDK key?), so OpenPush "
                             + "is not running and no permission prompt was shown. See the OpenPush log for why.");
        }
    }
}
