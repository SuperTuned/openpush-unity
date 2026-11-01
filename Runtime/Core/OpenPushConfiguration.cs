using System;

namespace OpenPushSDK.Core
{
    /// <summary>
    /// Receive-only client configuration. The SDK key is public and may ship in
    /// the app; a device credential is not part of this type.
    /// </summary>
    public sealed class OpenPushConfiguration
    {
        // Core owns this value because its constructor default cannot reference the Unity facade.
        // OpenPush.DefaultServerUrl aliases this const so there is still one source of truth.
        public const string DefaultServerUrl = "https://app.openpush.ai";

        public string ServerURL { get; }
        public string AppID { get; }
        public string SdkKey { get; }
        /// <summary>
        /// Null means "resolve from the runtime platform at attach" (android/ios/unity).
        /// </summary>
        public string Platform { get; }
        public string TokenKey { get; }

        public OpenPushConfiguration(string appID, string sdkKey,
                                     string serverURL = DefaultServerUrl,
                                     string platform = null,
                                     string tokenKey = "openpush.deviceToken")
        {
            if (serverURL == null) throw new ArgumentNullException(nameof(serverURL));
            if (appID == null) throw new ArgumentNullException(nameof(appID));
            if (sdkKey == null) throw new ArgumentNullException(nameof(sdkKey));
            ServerURL = serverURL.TrimEnd('/');
            AppID = appID;
            SdkKey = sdkKey;
            Platform = platform;
            TokenKey = tokenKey;
        }

        /// <summary>
        /// The Resources asset name the Unity layer loads project configuration from.
        /// Named here so the SDK, the docs and the tests cannot drift apart.
        /// </summary>
        public const string SettingsResourceName = "OpenPushSettings";

        /// <summary>
        /// Resolves which public SDK key to use: an explicit argument first, then
        /// the bundled asset, then nothing so the caller can refuse.
        ///
        /// The key is not a secret — the server calls it ingest-only and documents
        /// that it ships inside public binaries by design — so keeping it in a
        /// shipped asset changes nothing about who can read it. It moves there so
        /// Initialize can take an app id while the SDK key comes from settings.
        ///
        /// A blank value counts as absent on either side: an empty string means
        /// "not configured", never "configured with nothing", and a blank explicit
        /// argument must fall through to the asset rather than shadowing it.
        /// </summary>
        public static string ResolveSdkKey(string explicitKey, string bundledKey)
        {
            var chosen = NonBlank(explicitKey) ?? NonBlank(bundledKey);
            return chosen;
        }

        private static string NonBlank(string value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();

        public string Url(string path) => ServerURL + path;
    }
}
