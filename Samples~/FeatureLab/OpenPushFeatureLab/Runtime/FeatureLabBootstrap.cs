using UnityEngine;
using UnityEngine.UIElements;

namespace OpenPushFeatureLab
{
    [DefaultExecutionOrder(-1000)]
    internal sealed class FeatureLabBootstrap : MonoBehaviour
    {
        private FeatureLabController _controller;
        private FeatureLabView _view;
        private float _nextAppearancePoll;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FindObjectOfType<FeatureLabBootstrap>() != null) return;
            var host = new GameObject("OpenPush Feature Lab");
            DontDestroyOnLoad(host);
            host.AddComponent<FeatureLabBootstrap>();
        }

        private void Awake()
        {
            var tree = Resources.Load<VisualTreeAsset>("OpenPushFeatureLab");
            var styles = Resources.Load<StyleSheet>("OpenPushFeatureLab");
            var theme = Resources.Load<ThemeStyleSheet>("OpenPushFeatureLabTheme");
            if (tree == null || styles == null || theme == null)
            {
                Debug.LogError("OpenPush Feature Lab resources are missing.");
                enabled = false;
                return;
            }

            var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "OpenPush Feature Lab Panel";
            panelSettings.themeStyleSheet = theme;
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(430, 932);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            panelSettings.sortingOrder = 100;

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            tree.CloneTree(document.rootVisualElement);
            document.rootVisualElement.styleSheets.Add(styles);

            _controller = new FeatureLabController();
            _view = new FeatureLabView(
                document.rootVisualElement,
                _controller,
                Resources.Load<Texture2D>("feature-announcement"));
        }

        private void Update()
        {
            if (_view == null || Time.unscaledTime < _nextAppearancePoll) return;
            _nextAppearancePoll = Time.unscaledTime + 0.5f;
            _view.RefreshSystemAppearance();
            _view.RefreshSafeArea();
        }

        private void OnDestroy()
        {
            _view?.Dispose();
            _controller?.Dispose();
        }
    }

    internal static class FeatureLabSystemAppearance
    {
        public static bool IsDark
        {
            get
            {
#if UNITY_EDITOR
                return UnityEditor.EditorGUIUtility.isProSkin;
#elif UNITY_ANDROID
                try
                {
                    using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                    using (var resources = activity.Call<AndroidJavaObject>("getResources"))
                    using (var configuration = resources.Call<AndroidJavaObject>("getConfiguration"))
                    {
                        const int mask = 0x30;
                        const int nightYes = 0x20;
                        return (configuration.Get<int>("uiMode") & mask) == nightYes;
                    }
                }
                catch { return false; }
#elif UNITY_IOS
                return OpenPushFeatureLabSystemThemeIsDark();
#else
                return false;
#endif
            }
        }

#if UNITY_IOS && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")]
        private static extern bool OpenPushFeatureLabSystemThemeIsDark();
#endif
    }
}
