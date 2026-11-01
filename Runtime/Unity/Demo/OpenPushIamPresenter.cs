using System;
using System.Collections;
using OpenPushSDK.Core;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UIElements;

namespace OpenPushSDK
{
    internal sealed class OpenPushIamPresenter
    {
        internal static readonly OpenPushIamPresenter Shared = new OpenPushIamPresenter();
        private GameObject _host;
        public void Show(OpenPushInAppMessage message, Action<OpenPushInAppClick> action)
        {
            Dismiss(); _host = new GameObject("OpenPushInAppMessage") { hideFlags = HideFlags.HideAndDontSave };
            UnityEngine.Object.DontDestroyOnLoad(_host);
            var loader = _host.AddComponent<OpenPushIamImageLoader>();
            var document = _host.AddComponent<UIDocument>();
            var settings = ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "OpenPush In-App Panel";
            settings.sortingOrder = 100;
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(430, 932);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            var theme = Resources.Load<ThemeStyleSheet>("OpenPushRuntimeTheme");
            if (theme != null) settings.themeStyleSheet = theme;
            else OpenPush.Log(OpenPushLogLevel.Warning, "⚠ OpenPushRuntimeTheme.tss is missing from Resources — in-app text will not render");
            document.panelSettings = settings;
            // The click goes straight to the core, which takes the presentation
            // down itself when the click closes. Dismissing here as well would
            // destroy the host the core has by then given to the NEXT message.
            document.rootVisualElement.Add(new OpenPushIamView(message, action, loader.Load).Root);
        }
        public void Dismiss() { if (_host != null) UnityEngine.Object.Destroy(_host); _host = null; }
    }

    internal sealed class OpenPushIamImageLoader : MonoBehaviour
    {
        internal void Load(string url, Action<Texture2D> done)
            => StartCoroutine(Fetch(url, done));

        private IEnumerator Fetch(string url, Action<Texture2D> done)
        {
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                    done?.Invoke(DownloadHandlerTexture.GetContent(request));
            }
        }
    }
}
