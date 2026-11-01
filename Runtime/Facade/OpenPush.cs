using System;
using System.Collections.Generic;
using System.Text;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK
{
    /// <summary>
    /// OpenPush entry point for Unity hosts: a thin layer over the native OpenPush iOS and Android
    /// SDKs, which own registration, identity, receipts, sessions, permission, notification
    /// processing and in-app messages. The Editor and desktop players run an in-memory stub.
    /// Every event is raised on the Unity main thread.
    /// </summary>
    public static partial class OpenPush
    {
        /// <summary>The hosted OpenPush server.</summary>
        public const string DefaultServerUrl = OpenPushConfiguration.DefaultServerUrl;

        internal static readonly OpenPushEventHub Hub =
            new OpenPushEventHub(() => OpenPushHost.Dispatcher, message => Log(OpenPushLogLevel.Error, message));
        internal static readonly OpenPushUserStateTracker UserTracker = new OpenPushUserStateTracker();

        private static readonly object WiringGate = new object();
        private static IOpenPushPlatform _wired;
        private static Action<string> _onLog = _ => { };
        private static Func<string> _credential;
        private static volatile bool _initializeCalled;
        private static bool _migrationAttempted;

        static OpenPush()
        {
            UserTracker.Changed += state => Hub.UserChanged.Raise(state);
            SubscribeInternal();
        }

        private static void SubscribeInternal()
        {
            Hub.Log.Subscribe(line =>
            {
                if (!OpenPushLogClassifier.Passes(line.Level, LogLevel)) return;
                try { _onLog(line.Message); } catch (Exception) { }
                // A throwing log handler is not logged: that line would come straight back here.
                Each(Logged, handler => handler(line.Message), report: false);
            });
            Hub.Registered.Subscribe(_ =>
            {
                Guarded(() => OnRegistered?.Invoke());
                Each(Registered, handler => handler());
            });
            Hub.RegistrationFailed.Subscribe(ReportRegistrationFailed);
        }

        /// <summary>
        /// The one log sink: assign it (one handler, replaced by the next assignment). <see cref="Logged"/>
        /// takes any number of subscribers; both hear the same lines, this one first.
        /// </summary>
        public static Action<string> OnLog { get => _onLog; set => _onLog = value ?? (_ => { }); }

        /// <summary>How much of the log reaches <see cref="OnLog"/> and <see cref="Logged"/>. Verbose (everything) by default.</summary>
        public static OpenPushLogLevel LogLevel { get; set; } = OpenPushLogLevel.Verbose;

        /// <summary>Assignable, one handler. <see cref="Registered"/> takes any number; this one runs first.</summary>
        public static Action OnRegistered { get; set; }

        /// <summary>Assignable, one handler. <see cref="RegistrationFailed"/> takes any number; this one runs first.</summary>
        public static Action<string> OnRegistrationFailed { get; set; }

        /// <summary>
        /// Every line <see cref="OnLog"/> receives, for any number of subscribers (+= and -=). Raised on the
        /// main thread right after <see cref="OnLog"/>, subscribers in the order they subscribed, and gated
        /// by <see cref="LogLevel"/> the same way. A subscriber that throws does not stop the others.
        /// </summary>
        public static event Action<string> Logged;

        /// <summary>
        /// The server accepted the registration: <see cref="OnRegistered"/>'s event, for any number of
        /// subscribers. Raised on the main thread right after <see cref="OnRegistered"/>, in subscription order.
        /// </summary>
        public static event Action Registered;

        /// <summary>
        /// A registration failed or <c>Initialize</c> refused, with the message: <see cref="OnRegistrationFailed"/>'s
        /// event, for any number of subscribers. Raised right after <see cref="OnRegistrationFailed"/>, in
        /// subscription order.
        /// </summary>
        public static event Action<string> RegistrationFailed;

        /// <summary>The property first, then each event subscriber; a throw is logged and stops nobody else.</summary>
        private static void ReportRegistrationFailed(string message)
        {
            Guarded(() => OnRegistrationFailed?.Invoke(message));
            Each(RegistrationFailed, handler => handler(message));
        }

        /// <summary>
        /// An Initialize refusal, reported inline as it always was: the property is called directly (a
        /// throw from it still reaches the Initialize caller), then each event subscriber.
        /// </summary>
        private static void ReportRefusal(string note)
        {
            OnRegistrationFailed?.Invoke(note);
            Each(RegistrationFailed, handler => handler(note));
        }

        private static void Guarded(Action call, bool report = true)
        {
            try { call(); }
            catch (Exception e)
            {
                if (report) Log(OpenPushLogLevel.Error, "✗ an OpenPush event handler threw: " + e.GetType().Name + ": " + e.Message);
            }
        }

        private static void Each<T>(T handlers, Action<T> invoke, bool report = true) where T : Delegate
        {
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList()) Guarded(() => invoke((T)handler), report);
        }

        /// <summary>
        /// The app requires identity verification and the SDK holds an external id without an auth
        /// hash (for example one migrated from an earlier OpenPush SDK build). Call Login(externalId, authHash) with a hash
        /// from your server. Buffered until the first subscriber.
        /// </summary>
        public static event Action<string> OnIdentityVerificationRequired
        {
            add => Hub.IdentityVerificationRequired.Subscribe(value);
            remove => Hub.IdentityVerificationRequired.Unsubscribe(value);
        }

        public static string CurrentToken => Platform.Core.CurrentToken;
        public static bool IsInitialized => Platform.Core.IsInitialized;

        /// <summary>The native SDK version (iOS 1.2.2 / Android 1.2.2; "stub" in the Editor).</summary>
        public static string SdkVersion => Platform.Core.SdkVersion;

        /// <summary>This Unity package's version.</summary>
        public static string WrapperVersion => OpenPushVersion.Current;

        internal static IOpenPushPlatform Platform
        {
            get
            {
                var platform = OpenPushPlatformRegistry.Current;
                if (!ReferenceEquals(platform, _wired)) Wire(platform);
                return platform;
            }
        }

        /// <summary>
        /// Initializes with the SDK key from the Resources/OpenPushSettings asset. Refuses, through
        /// OnLog and OnRegistrationFailed, when no key can be found.
        /// </summary>
        public static void Initialize(string appId)
        {
            var key = OpenPushConfiguration.ResolveSdkKey(null, OpenPushHost.Settings()?.SdkKey);
            if (key == null)
            {
                var note = "✗ Initialize refused — no SDK key. Create an "
                         + OpenPushConfiguration.SettingsResourceName
                         + " asset in a Resources folder (Assets → Create → OpenPush → Settings), "
                         + "or call Initialize(appId, sdkKey).";
                Log(OpenPushLogLevel.Error, note);
                ReportRefusal(note);
                return;
            }
            Initialize(appId, key);
        }

        /// <summary>
        /// Initializes with an explicit SDK key. Trailing slashes are trimmed from
        /// <paramref name="serverUrl"/>; a blank one means the hosted server.
        /// </summary>
        /// <exception cref="ArgumentException">sdkKey is null (ArgumentNullException) or blank.</exception>
        public static void Initialize(string appId, string sdkKey, string serverUrl = DefaultServerUrl,
                                      Func<string> credential = null)
        {
            // As in OpenPushConfiguration: a missing key is a programming error, not a state.
            if (sdkKey == null) throw new ArgumentNullException(nameof(sdkKey));
            if (string.IsNullOrWhiteSpace(sdkKey))
                throw new ArgumentException("The SDK key is blank. Pass the app's public SDK key, or call Initialize(appId) to read it from the OpenPushSettings asset.", nameof(sdkKey));
            if (string.IsNullOrWhiteSpace(appId))
            {
                const string note = "✗ Initialize refused — the app id is empty.";
                Log(OpenPushLogLevel.Error, note);
                ReportRefusal(note);
                return;
            }
            var platform = Platform;
            WarnIfPlatformWasStripped(platform);
            var settings = OpenPushHost.Settings();
            _credential = credential;
            var options = new OpenPushInitOptions
            {
                AppId = appId.Trim(),
                SdkKey = sdkKey.Trim(),
                ServerUrl = ServerUrlOrDefault(serverUrl),
                AppGroup = settings?.AppGroup,
                Credential = ReadCredential,
                Provisional = false,
                Wrapper = OpenPushVersion.Wrapper,
                LogLevel = LogLevel,
            };
            var legacy = CaptureLegacyStateOnce(platform);
            platform.Core.Initialize(options);
            MigrateLegacyStateOnce(platform, options, settings, legacy);
            _initializeCalled = true;
            UserTracker.Seed(platform.User.ExternalId, platform.PushSubscription.Id);
        }

        /// <summary>
        /// A device player running on the stub means its platform assembly never registered: the
        /// managed linker stripped it (Unity ignores link.xml inside packages). Nothing native works
        /// then, so say it loudly instead of letting the stub pretend.
        /// </summary>
        private static void WarnIfPlatformWasStripped(IOpenPushPlatform platform)
        {
            var message = StrippedPlatformMessage(platform);
            if (message != null) Log(OpenPushLogLevel.Error, message);
        }

        /// <summary>
        /// The Unity host's startup check: the same message straight to the Unity console, because
        /// OnLog is a no-op until the host sets it.
        /// </summary>
        internal static void ReportStrippedPlatform()
        {
            var message = StrippedPlatformMessage(Platform);
            if (message != null) OpenPushHost.ReportError?.Invoke("OpenPush: " + message);
        }

        private static string StrippedPlatformMessage(IOpenPushPlatform platform)
        {
            var expected = OpenPushHost.DevicePlatformAssembly;
            if (expected == null || !(platform is OpenPushStubPlatform)) return null;
            return "✗ " + expected + " was stripped from this player, so OpenPush is running "
                + "on the in-memory stub and no native SDK calls are made. Keep the assembly: it must carry "
                + "[assembly: AlwaysLinkAssembly], and check the Managed Stripping Level and any link settings.";
        }

        /// <summary>
        /// Replays an earlier build's PlayerPrefs state onto the native SDK, once per process (the migrator also
        /// marks itself done in PlayerPrefs). Never throws: failures are logged and retried next launch.
        /// </summary>
        private static void MigrateLegacyStateOnce(IOpenPushPlatform platform, OpenPushInitOptions options,
                                                   OpenPushSettingsSnapshot settings, OpenPushLegacySnapshot legacy)
        {
            if (_migrationAttempted || OpenPushHost.LegacyPrefs == null) return;
            _migrationAttempted = true;
            if (legacy == null) return;
            OpenPushLegacyStateMigrator.Run(new OpenPushLegacyMigrationContext
            {
                Prefs = OpenPushHost.LegacyPrefs,
                Platform = platform,
                AppId = options.AppId,
                HostOwnedAndroid = platform.Name == "android"
                    && settings?.AndroidPushDelivery == OpenPushAndroidPushDelivery.HostOwned,
                ReloginWithoutAuthHash = settings?.ReloginWithoutAuthHash ?? true,
                IdentityVerificationRequired = id => Hub.IdentityVerificationRequired.Raise(id),
                // Same follow-up as a host Login: adopt the id into User.Changed, then refresh the credential.
                LoginDone = (externalId, result) => OpenPushHost.Dispatcher.Post(() =>
                {
                    if (result != null && result.Success) UserTracker.OnExternalId(externalId);
                    RefreshCredential();
                }),
                Log = Log,
                Snapshot = legacy,
                NativeSharesPrefs = NativeSharesLegacyPrefs(platform),
            });
        }

        /// <summary>
        /// On iOS PlayerPrefs is NSUserDefaults.standardUserDefaults, the native SDK's own store, and
        /// the earlier build's key names are the native ones. Android keeps PlayerPrefs in a file of its own.
        /// </summary>
        private static bool NativeSharesLegacyPrefs(IOpenPushPlatform platform) => platform.Name == "ios";

        /// <summary>An earlier build's PlayerPrefs state before the native initialize can read or rewrite it; null when there is nothing to migrate.</summary>
        private static OpenPushLegacySnapshot CaptureLegacyStateOnce(IOpenPushPlatform platform)
        {
            if (_migrationAttempted || OpenPushHost.LegacyPrefs == null) return null;
            return OpenPushLegacyStateMigrator.Capture(OpenPushHost.LegacyPrefs, NativeSharesLegacyPrefs(platform), Log);
        }

        /// <summary>
        /// The OpenPushBootstrap component's Start. Returns false, without throwing, when Initialize
        /// refused (it has already said why through OnLog and OnRegistrationFailed); the permission
        /// prompt is then skipped, since it would throw before Initialize.
        /// </summary>
        internal static bool StartFromBootstrap(string appId, bool requestPermission)
        {
            Initialize(appId);
            if (!_initializeCalled) return false;
            if (requestPermission) Notifications.RequestPermission(false, null);
            return true;
        }

        private static string ServerUrlOrDefault(string serverUrl)
        {
            var trimmed = serverUrl?.Trim().TrimEnd('/');
            return string.IsNullOrEmpty(trimmed) ? DefaultServerUrl : trimmed;
        }

        [Obsolete("Flush is a no-op: the native SDK persists and sends on its own.")]
        public static void Flush()
            => Log(OpenPushLogLevel.Info, "• Flush() is a no-op — the native SDK persists and sends on its own.");

        /// <summary>
        /// Hands a provider token to the native SDK: the Forward (HostOwned) FCM path and tests. The
        /// registration carries the identity the last <see cref="Login"/> set, with its auth hash.
        /// </summary>
        public static void Register(string token)
        {
            RequireInitialized(nameof(Register));
            if (string.IsNullOrWhiteSpace(token)) { Log(OpenPushLogLevel.Warning, "⚠ Register ignored — empty token"); return; }
            Platform.Core.Register(token, null);
        }

        /// <summary>
        /// <see cref="Register(string)"/> that also names an external id. An identity is attached only
        /// through login, so a non-empty <paramref name="externalId"/> other than the current one is
        /// logged in first, as <c>Login(externalId)</c> without an auth hash would, and the registration
        /// then carries it. <c>null</c>, empty or the current id registers exactly as
        /// <see cref="Register(string)"/>, keeping the current login and its auth hash.
        /// </summary>
        [Obsolete("Call Login(externalId, authHash) for the identity and Register(token) for the token.")]
        public static void Register(string token, string externalId)
        {
            RequireInitialized(nameof(Register));
            if (string.IsNullOrWhiteSpace(token)) { Log(OpenPushLogLevel.Warning, "⚠ Register ignored — empty token"); return; }
            var id = string.IsNullOrWhiteSpace(externalId) ? null : externalId;
            Platform.Core.Register(token, id);
            if (id == null) return;
            // The native SDK has applied the id as a login: report it the way a Login result is reported.
            OpenPushHost.Dispatcher.Post(() => UserTracker.OnExternalId(Platform.User.ExternalId));
        }

        /// <summary>Records a custom event for Journey entry and wait-until conditions.</summary>
        public static void TrackEvent(string name, IReadOnlyDictionary<string, object> properties = null)
        {
            RequireInitialized(nameof(TrackEvent));
            Platform.Core.TrackEvent(name, properties == null ? "{}" : OpenPushJson.WriteValueMap(properties));
        }

        public static void Login(string externalId, string authHash = null, Action<OpenPushIdentityResult> done = null)
        {
            RequireInitialized(nameof(Login));
            Platform.Core.Login(externalId, authHash, result => OpenPushHost.Dispatcher.Post(() =>
            {
                if (result != null && result.Success) UserTracker.OnExternalId(externalId);
                done?.Invoke(result);
                RefreshCredential();
            }));
        }

        public static void Logout(Action<OpenPushIdentityResult> done = null)
        {
            RequireInitialized(nameof(Logout));
            Platform.Core.Logout(result => OpenPushHost.Dispatcher.Post(() =>
            {
                if (result != null && result.Success) UserTracker.OnExternalId(null);
                done?.Invoke(result);
            }));
        }

        public static void Receipt(string type, string messageId, Action then = null)
        {
            RequireInitialized(nameof(Receipt));
            Platform.Core.Receipt(type, messageId, then == null ? (Action)null : () => OpenPushHost.Dispatcher.Post(then));
        }

        public static string MessageId(IReadOnlyDictionary<string, string> data)
            => data != null && data.TryGetValue("op_message_id", out var value) && !string.IsNullOrEmpty(value) ? value : null;

        public static void ClearToken()
        {
            RequireInitialized(nameof(ClearToken));
            Platform.Core.ClearToken();
        }

        public static string ShortToken(string token)
        {
            if (token == null) return "";
            return token.Length <= 20 ? token : token.Substring(0, 12) + "…(" + token.Length + " chars)";
        }

        /// <summary>Converts raw APNs token bytes to lowercase hexadecimal.</summary>
        public static string HexToken(byte[] token)
        {
            if (token == null || token.Length == 0) return "";
            var sb = new StringBuilder(token.Length * 2);
            foreach (var b in token) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>The Unity layer's own way into the one gated log sink (also used by Runtime/Unity).</summary>
        internal static void Log(OpenPushLogLevel level, string text) => Hub.Log.Raise(new OpenPushLogLine(level, text));

        internal static void RequireInitialized(string member)
        {
            if (_initializeCalled) return;
            throw new InvalidOperationException(
                "OpenPush." + member + " was called before OpenPush.Initialize. Call Initialize first "
              + "(or add the OpenPush Bootstrap component to your first scene).");
        }

        /// <summary>Pushes the host's current credential to the native SDK (Initialize, focus, Login).</summary>
        internal static void RefreshCredential()
        {
            if (!_initializeCalled || _credential == null) return;
            Platform.Core.UpdateCredential(ReadCredential());
        }

        private static string ReadCredential()
        {
            try { return _credential?.Invoke(); }
            catch (Exception e) { Log(OpenPushLogLevel.Error, "✗ the credential closure threw: " + e.Message); return null; }
        }

        private static void Wire(IOpenPushPlatform platform)
        {
            lock (WiringGate)
            {
                if (ReferenceEquals(platform, _wired)) return;
                _wired = platform;
            }
            platform.Core.Log += (level, message) => Hub.Log.Raise(new OpenPushLogLine(level, message));
            platform.Core.Registered += () => Hub.Registered.Raise(true);
            platform.Core.RegistrationFailed += message => Hub.RegistrationFailed.Raise(message);
            platform.Core.IdentityVerificationRequired += id => Hub.IdentityVerificationRequired.Raise(id);
            platform.PushSubscription.Changed += (previous, current) =>
            {
                OpenPushHost.Dispatcher.Post(() => UserTracker.OnSubscription(current));
                Hub.SubscriptionCurrent.Raise(current);
                Hub.SubscriptionChanged.Raise(new OpenPushSubscriptionChange(previous, current));
            };
            platform.Notifications.PermissionChanged += granted => Hub.PermissionChanged.Raise(granted);
            platform.Notifications.Clicked += click => Hub.Clicked.Raise(click);
            platform.Notifications.ForegroundWillDisplay += foreground => Hub.Foreground.Raise(foreground);
            platform.Notifications.BackgroundMessage += message => Hub.BackgroundMessage.Raise(message);
            platform.InAppMessages.Lifecycle += (id, phase) => Hub.InAppLifecycle.Raise(new OpenPushInAppLifecycle(id, phase));
            platform.InAppMessages.Clicked += (id, click) => Hub.InAppClicked.Raise(new OpenPushInAppClicked(id, click));
        }

        internal static void ResetForTests()
        {
            Hub.Reset();
            SubscribeInternal();
            UserTracker.Reset();
            ResetHandlerMaps();
            lock (WiringGate) _wired = null;
            _onLog = _ => { };
            _credential = null;
            _initializeCalled = false;
            _migrationAttempted = false;
            LogLevel = OpenPushLogLevel.Verbose;
            OnRegistered = null;
            OnRegistrationFailed = null;
            Logged = null;
            Registered = null;
            RegistrationFailed = null;
            OpenPushHost.Reset();
        }

        // Wrapper maps for the two-argument events (declared here so ResetForTests clears them all;
        // used by Tasks 2.11 and 2.13).
        internal static readonly OpenPushHandlerMap<OpenPushSubscriptionChange> SubscriptionHandlers = new OpenPushHandlerMap<OpenPushSubscriptionChange>();
        internal static readonly OpenPushHandlerMap<OpenPushInAppLifecycle> InAppLifecycleHandlers = new OpenPushHandlerMap<OpenPushInAppLifecycle>();
        internal static readonly OpenPushHandlerMap<OpenPushInAppClicked> InAppClickHandlers = new OpenPushHandlerMap<OpenPushInAppClicked>();

        private static void ResetHandlerMaps()
        {
            SubscriptionHandlers.Clear();
            InAppLifecycleHandlers.Clear();
            InAppClickHandlers.Clear();
        }
    }
}
