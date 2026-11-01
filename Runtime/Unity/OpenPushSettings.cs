using UnityEngine;
using OpenPushSDK.Core;

namespace OpenPushSDK
{
    /// <summary>
    /// Project-level OpenPush configuration, so Initialize can take only the app id.
    /// Create it with Assets → Create → OpenPush → Settings and keep it in a
    /// Resources folder, which is where the SDK looks for it.
    ///
    /// The SDK key is not a secret: the server calls it ingest-only and documents
    /// that it ships inside public binaries by design. Storing it in a shipped
    /// asset changes nothing about who can read it.
    ///
    /// Upgrades never change what an existing asset does. Unity gives a field the asset file does not
    /// hold its initializer, so every field added after the first releases is initialized to what an
    /// asset without it did before the field existed: <see cref="InjectLiveActivityWidget"/> on.
    /// <see cref="AndroidPushDelivery"/> is Automatic whether or not the asset holds it: OpenPush owns FCM,
    /// as it did on 1.1.5 to 1.2.1, so an asset without the field never loses the SDK's FCM service. A project
    /// that brings its own FCM service and has no <c>AndroidPushDelivery</c> in its asset must set it to Forward
    /// (<c>AndroidPushDelivery: 1</c>) before upgrading, or pushes arrive twice.
    /// <see cref="OpenLaunchUrls"/> is on whether or not the asset holds it, the same as in new assets
    /// and in the native SDKs: a tapped push's launch URL opens unless the app turns it off. A new asset gets today's defaults from
    /// <see cref="CreateWithCurrentDefaults"/> (the Create menu and the Setup window use it) and is
    /// stamped with <see cref="CurrentSettingsVersion"/>.
    /// </summary>
    public sealed class OpenPushSettings : ScriptableObject
    {
        /// <summary>The settings layout a new asset is stamped with.</summary>
        internal const int CurrentSettingsVersion = 1;

        /// <summary>
        /// 0: an asset made before the layout was versioned, whose missing fields keep their earlier
        /// meaning. <see cref="CurrentSettingsVersion"/>: made with today's defaults.
        /// </summary>
        [SerializeField, HideInInspector] private int settingsVersion;

        internal int SettingsVersion => settingsVersion;

        [Tooltip("Your public SDK key from the OpenPush console.")]
        public string SdkKey;

        [Tooltip("Optional. Only used by hosts that would rather not repeat the app id in code.")]
        public string AppId;

        [Tooltip("Optional. When set, this App Group is added to the exported iOS "
               + "project's entitlements, and the notification service extension "
               + "shares the app's context through it instead of through the "
               + "default group.")]
        public string AppGroupIdentifier;

        [Tooltip("Inject a notification service extension into exported iOS projects, for "
               + "rich media and delivery receipts while the app is not running. Turn this "
               + "off if the host already owns a service extension — iOS runs one.")]
        public bool InjectServiceExtension = true;

        [Tooltip("Inject a Live Activity widget extension into exported iOS projects. "
               + "Off in new settings assets; turn it on (or use the Setup window's Live Activity widget row) "
               + "if the game starts Live Activities with OpenPush's default widget. Leave it off if the host "
               + "already owns a widget extension.")]
        public bool InjectLiveActivityWidget = true;

        [Tooltip("Open a tapped push's launch URL straight away: on Android instead of starting the "
               + "app (Automatic delivery only), on iOS as the app comes up, before Unity has loaded. "
               + "A tapped action button with its own link opens that link instead. The click still reaches "
               + "Notifications.Clicked with LaunchUrlOpened set. Turn this off to route every URL "
               + "from a Clicked handler yourself. On by default, including in an asset made before this "
               + "setting existed.")]
        public bool OpenLaunchUrls = true;

        [Header("iOS extension identifiers and signing")]
        [Tooltip("Optional. The notification service extension's bundle id. Blank: <app bundle id>."
               + "OpenPushNotificationService. Must start with the app's bundle id and a dot. Set it to an "
               + "App ID you already have (for example the extension your previous push SDK created).")]
        public string ServiceExtensionBundleIdentifier;

        [Tooltip("Optional. The provisioning profile (name or UUID) the service extension signs with under "
               + "manual signing. Written as PROVISIONING_PROFILE_SPECIFIER with manual signing on the "
               + "extension. Blank: nothing is written. A batch build with manual signing fails without it.")]
        public string ServiceExtensionProvisioningProfile;

        [Tooltip("Optional. The Live Activity widget's bundle id. Blank: <app bundle id>.LiveActivityWidget. "
               + "Must start with the app's bundle id and a dot.")]
        public string LiveActivityWidgetBundleIdentifier;

        [Tooltip("Optional. The provisioning profile (name or UUID) the Live Activity widget signs with under "
               + "manual signing. Blank: nothing is written.")]
        public string LiveActivityWidgetProvisioningProfile;

        [Header("Existing push setup")]
        [Tooltip("What an iOS export does about the notification service extension. Inject: add OpenPush's own. "
               + "TakeOver: reuse the extension your previous push SDK created (its code becomes OpenPush's, its "
               + "App ID and profile are kept). Share: add OpenPush's lines to your own extension's code. Off: change "
               + "nothing. Set from the Setup window when an existing extension is found.")]
        public OpenPushIosExtensionMode IosExtensionMode = OpenPushIosExtensionMode.Inject;

        [Tooltip("The existing extension target TakeOver and Share apply to. Blank: the one service extension in the export.")]
        public string IosExtensionTarget;

        [Tooltip("Reused: with AppGroupIdentifier blank, the App Group the app and its existing extension already "
               + "share. New: AppGroupIdentifier, or group.<bundle id>.openpush.")]
        public OpenPushPlumbingSource AppGroupSource = OpenPushPlumbingSource.New;

        [Tooltip("Reused: keep the Android notification icons already in the project as OpenPush's. New: OpenPush's own names.")]
        public OpenPushPlumbingSource IconsSource = OpenPushPlumbingSource.New;

        [Tooltip("Optional. The drawable OpenPush notifications use as their status-bar icon (Automatic delivery), "
               + "written as ai.openpush.notification_icon. Reusing your existing icons in the Setup window fills it; "
               + "blank: OpenPush's default names.")]
        public string AndroidNotificationIconName;

        [Header("Android")]
        [Tooltip("Automatic: the OpenPush FCM module fetches the token, renders notifications and handles taps. "
               + "Forward (also accepted as HostOwned): your own FirebaseMessagingService (for example Firebase Unity "
               + "Messaging) receives every push and calls OpenPush.Register(token) and "
               + "Notifications.ProcessForeground/ProcessClick. Automatic by default, including in an asset that "
               + "does not hold this field. A project with its own FCM service must set Forward before upgrading.")]
        public OpenPushAndroidPushDelivery AndroidPushDelivery = OpenPushAndroidPushDelivery.Automatic;

        [Tooltip("Firebase project id (Automatic mode, when google-services.json is not in Assets).")]
        public string AndroidFirebaseProjectId;

        [Tooltip("Firebase Android application id, 1:<number>:android:<hex> (Automatic mode, without google-services.json).")]
        public string AndroidFirebaseApplicationId;

        [Tooltip("Firebase Web API key (Automatic mode, without google-services.json). Not a secret.")]
        public string AndroidFirebaseApiKey;

        [Header("Upgrading from an earlier OpenPush SDK build")]
        [Tooltip("When migrating state from an earlier OpenPush SDK build, log the migrated external id back in without an auth hash. "
               + "Turn this off if your app uses identity verification: OpenPush then raises "
               + "OnIdentityVerificationRequired(externalId) and leaves the device anonymous.")]
        public bool ReloginWithoutAuthHash = true;

        /// <summary>
        /// A settings object for a project that has none yet: today's defaults, stamped with
        /// <see cref="CurrentSettingsVersion"/>. Not an asset until the caller saves it.
        /// </summary>
        internal static OpenPushSettings CreateWithCurrentDefaults()
        {
            var settings = CreateInstance<OpenPushSettings>();
            settings.ApplyCurrentDefaults();
            return settings;
        }

        /// <summary>
        /// Today's defaults for the fields whose initializer keeps an earlier asset's meaning. Live
        /// Activity widget injection is off in a new asset; the Setup window's row turns it on.
        /// </summary>
        internal void ApplyCurrentDefaults()
        {
            InjectLiveActivityWidget = false;
            settingsVersion = CurrentSettingsVersion;
        }

        // The Inspector's Reset (and, in Unity versions that call it there, creation from a menu).
        // Never called for an asset loaded from disk, so an existing asset is untouched.
        private void Reset() => ApplyCurrentDefaults();

        internal OpenPushSettingsSnapshot Snapshot()
            => OpenPushSettingsSnapshotReader.From(SdkKey, AppId, AppGroupIdentifier, InjectServiceExtension,
                                                   Application.identifier, AndroidPushDelivery, ReloginWithoutAuthHash);

        /// The asset, or null when the project has none.
        internal static OpenPushSettings Load() =>
            Resources.Load<OpenPushSettings>(OpenPushConfiguration.SettingsResourceName);
    }
}
