using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;
using UnityEngine;

namespace OpenPushSDK.Android
{
    internal sealed class AndroidPlatform : IOpenPushPlatform
    {
        internal readonly OpenPushNativeEventRouter Router;
        private AndroidEventListenerProxy _listener;

        // Created on first use, not at SubsystemRegistration, so constructing the platform never
        // touches JNI before the player has loaded the adapter class.
        internal AndroidEventListenerProxy Listener => _listener ??= new AndroidEventListenerProxy(Router);

        internal AndroidPlatform()
        {
            Router = new OpenPushNativeEventRouter(CompleteForeground,
                _ => Router.RaiseLog(OpenPushLogLevel.Warning,
                    "⚠ Display() after PreventDefault is not supported on Android: the notification was already suppressed."));
            Core = new AndroidCore(this); User = new AndroidUser(); PushSubscription = new AndroidPushSubscription(this);
            Notifications = new AndroidNotifications(this); InAppMessages = new AndroidInAppMessages(this);
            LiveActivities = new AndroidLiveActivities(this); LiveUpdates = new AndroidLiveUpdates();
            Verify = new AndroidVerify();
        }

        public string Name => "android";
        public IOpenPushCore Core { get; }
        public IOpenPushUser User { get; }
        public IOpenPushPushSubscription PushSubscription { get; }
        public IOpenPushNotifications Notifications { get; }
        public IOpenPushInAppMessages InAppMessages { get; }
        public IOpenPushLiveActivities LiveActivities { get; }
        public IOpenPushLiveUpdates LiveUpdates { get; }
        public IOpenPushVerify Verify { get; }

        private static AndroidJavaClass B => AndroidBridgeClass.Bridge;

        private static void CompleteForeground(string id, bool display)
        {
            using (var completion = B.CallStatic<AndroidJavaObject>("foregroundCompletion"))
                completion.Call("complete", id, display);
        }

        internal static string Json(IReadOnlyList<string> list) => OpenPushWire.ToJson(list);

        private sealed class AndroidCore : IOpenPushCore
        {
            private readonly AndroidPlatform _p;
            internal AndroidCore(AndroidPlatform p) { _p = p; }
            public bool IsInitialized => B.CallStatic<bool>("isInitialized");
            public string CurrentToken => B.CallStatic<string>("currentToken");
            public string SdkVersion => B.CallStatic<string>("sdkVersion");

            public void Initialize(OpenPushInitOptions options)
            {
                B.CallStatic("setListener", _p.Listener);
                using (var activity = AndroidBridgeClass.Activity())
                    B.CallStatic("initialize", activity, options.AppId, options.SdkKey, options.ServerUrl,
                                 options.Wrapper, options.Credential?.Invoke());
            }

            public void UpdateCredential(string credential) => B.CallStatic("setCredential", credential);
            public void Register(string token, string externalId) => B.CallStatic("register", token, externalId);
            public void Receipt(string type, string messageId, Action then)
                => B.CallStatic("receipt", type, messageId, new AndroidCallbackProxy(_ => then?.Invoke()));
            public void TrackEvent(string name, string propertiesJson) => B.CallStatic("trackEvent", name, propertiesJson);
            public void Login(string externalId, string authHash, Action<OpenPushIdentityResult> done)
                => B.CallStatic("login", externalId, authHash, new AndroidCallbackProxy(json => done?.Invoke(Identity(json))));
            public void Logout(Action<OpenPushIdentityResult> done)
                => B.CallStatic("logout", new AndroidCallbackProxy(json => done?.Invoke(Identity(json))));
            public void OnAppForegrounded() => B.CallStatic("onAppForegrounded");
            public void ClearToken() => B.CallStatic("clearToken");

            private static OpenPushIdentityResult Identity(string json)
                => IdentityResultWire.From(OpenPushWire.Parse(json))?.ToPublic() ?? OpenPushIdentityResult.Failed(null);

            public event Action<OpenPushLogLevel, string> Log { add => _p.Router.Log += value; remove => _p.Router.Log -= value; }
            public event Action Registered { add => _p.Router.Registered += value; remove => _p.Router.Registered -= value; }
            public event Action<string> RegistrationFailed { add => _p.Router.RegistrationFailed += value; remove => _p.Router.RegistrationFailed -= value; }
            public event Action<string> IdentityVerificationRequired { add => _p.Router.IdentityVerificationRequired += value; remove => _p.Router.IdentityVerificationRequired -= value; }
        }

        private sealed class AndroidUser : IOpenPushUser
        {
            public void AddTags(IReadOnlyDictionary<string, string> tags) => B.CallStatic("addTags", OpenPushWire.StringMapToJson(tags));
            public void RemoveTags(IReadOnlyList<string> keys) => B.CallStatic("removeTags", Json(keys));
            public Dictionary<string, string> GetTags()
                => new Dictionary<string, string>(OpenPushWire.StringMap(OpenPushWire.Parse("{\"t\":" + (B.CallStatic<string>("getTags") ?? "{}") + "}"), "t"));
            public void AddAliases(IReadOnlyDictionary<string, string> aliases) => B.CallStatic("addAliases", OpenPushWire.StringMapToJson(aliases));
            public void RemoveAliases(IReadOnlyList<string> labels) => B.CallStatic("removeAliases", Json(labels));
            public string ExternalId => B.CallStatic<string>("externalId");
            public void AddSms(string number) => B.CallStatic("addSms", number);
            public void RemoveSms(string number) => B.CallStatic("removeSms", number);
            public void AddWhatsApp(string number) => B.CallStatic("addWhatsApp", number);
            public void RemoveWhatsApp(string number) => B.CallStatic("removeWhatsApp", number);
            public void AddEmail(string address) => B.CallStatic("addEmail", address);
            public void RemoveEmail(string address) => B.CallStatic("removeEmail", address);
        }

        private sealed class AndroidPushSubscription : IOpenPushPushSubscription
        {
            private readonly AndroidPlatform _p;
            internal AndroidPushSubscription(AndroidPlatform p) { _p = p; }
            private static PushSubscriptionWire Snapshot()
                => PushSubscriptionWire.From(OpenPushWire.Parse(B.CallStatic<string>("subscription"))) ?? new PushSubscriptionWire();
            public string Id => Snapshot().id;
            public string Token => Snapshot().token;
            public bool OptedIn => Snapshot().opted_in;
            // The adapter reports only the "not answered" statuses (-18 never asked, -19 asked and still
            // askable); null otherwise, so the calculator derives 1 or 0 from the permission.
            public int? Status => Snapshot().status;
            public void OptIn() => B.CallStatic("optIn");
            public void OptOut() => B.CallStatic("optOut");
            public event Action<PushSubscriptionState, PushSubscriptionState> Changed
            { add => _p.Router.SubscriptionChanged += value; remove => _p.Router.SubscriptionChanged -= value; }
        }

        private sealed class AndroidNotifications : IOpenPushNotifications
        {
            private readonly AndroidPlatform _p;
            internal AndroidNotifications(AndroidPlatform p) { _p = p; }
            public bool Permission => B.CallStatic<bool>("permission");
            public bool CanRequestPermission => B.CallStatic<bool>("canRequestPermission");
            public void RequestPermission(bool fallbackToSettings, Action<bool> done) => AndroidPermissionRequester.Request(fallbackToSettings, done);
            public void RequestProvisionalPermission(Action<bool> done) => done?.Invoke(false);
            public void ClearAll() => B.CallStatic("clearAll");
            public void Remove(string messageId) => B.CallStatic("remove", messageId);
            public void SetBadgeCount(int count)
                => _p.Router.RaiseLog(OpenPushLogLevel.Verbose, "• SetBadgeCount is iOS-only; ignored on Android");
            public void ProcessClick(string dataJson) => B.CallStatic("processClick", dataJson);
            public void ProcessForeground(string dataJson) => B.CallStatic("processForeground", dataJson);
            public bool BeginReceived(string dataJson) => B.CallStatic<bool>("beginReceived", dataJson);
            public void FinishReceived(string dataJson, bool handled) => B.CallStatic("finishReceived", dataJson, handled);
            public void ProcessDisplayed(string dataJson) => B.CallStatic("processDisplayed", dataJson);
            public event Action<bool> PermissionChanged { add => _p.Router.PermissionChanged += value; remove => _p.Router.PermissionChanged -= value; }
            public event Action<OpenPushClickEvent> Clicked { add => _p.Router.Clicked += value; remove => _p.Router.Clicked -= value; }
            public event Action<OpenPushForegroundEvent> ForegroundWillDisplay
            { add => _p.Router.ForegroundWillDisplay += value; remove => _p.Router.ForegroundWillDisplay -= value; }
            public event Action<OpenPushBackgroundMessage> BackgroundMessage
            { add => _p.Router.BackgroundMessage += value; remove => _p.Router.BackgroundMessage -= value; }
        }

        private sealed class AndroidInAppMessages : IOpenPushInAppMessages
        {
            private readonly AndroidPlatform _p;
            internal AndroidInAppMessages(AndroidPlatform p) { _p = p; }
            public bool Paused { get => B.CallStatic<bool>("isPaused"); set => B.CallStatic("setPaused", value); }
            public void AddTriggers(string triggersJson) => B.CallStatic("addTriggers", triggersJson);
            public void RemoveTriggers(IReadOnlyList<string> keys) => B.CallStatic("removeTriggers", Json(keys));
            public void ClearTriggers() => B.CallStatic("clearTriggers");
            public void Fetch() => B.CallStatic("fetch");
            public event Action<string, string> Lifecycle { add => _p.Router.InAppLifecycle += value; remove => _p.Router.InAppLifecycle -= value; }
            public event Action<string, OpenPushInAppClick> Clicked { add => _p.Router.InAppClicked += value; remove => _p.Router.InAppClicked -= value; }
        }

        private sealed class AndroidLiveActivities : IOpenPushLiveActivities
        {
            private readonly AndroidPlatform _p;
            internal AndroidLiveActivities(AndroidPlatform p) { _p = p; }
            private void Refuse(Action<bool> done) { _p.Router.RaiseLog(OpenPushLogLevel.Warning, "⚠ " + OpenPushLiveActivityNotes.IOSOnly); done?.Invoke(false); }
            public void SetupDefault(LiveActivitySetupOptions options) => Refuse(null);
            public void StartDefault(string activityId, string attributesJson, string contentJson) => Refuse(null);
            public void Enter(string activityId, string token, string activityType, Action<bool> done) => Refuse(done);
            public void Exit(string activityId, Action<bool> done) => Refuse(done);
            public void SetPushToStartToken(string activityType, string token, Action<bool> done) => Refuse(done);
            public void RemovePushToStartToken(string activityType, Action<bool> done) => Refuse(done);
        }

        private sealed class AndroidVerify : IOpenPushVerify
        {
            public void Start(string phone, OpenPushVerifyChannel channel, string locale, Action<OpenPushVerifyResult> done)
                => B.CallStatic("verifyStart", phone, OpenPushVerifyWire.Channel(channel), locale, Answer(done));
            public void Check(string verificationId, string code, Action<OpenPushVerifyResult> done)
                => B.CallStatic("verifyCheck", verificationId, code, Answer(done));
            public void Resend(string verificationId, OpenPushVerifyChannel channel, Action<OpenPushVerifyResult> done)
                => B.CallStatic("verifyResend", verificationId, OpenPushVerifyWire.Channel(channel), Answer(done));
            private static AndroidCallbackProxy Answer(Action<OpenPushVerifyResult> done)
                => new AndroidCallbackProxy(json => done?.Invoke(OpenPushVerifyWire.Read(OpenPushWire.Parse(json))));
        }

        private sealed class AndroidLiveUpdates : IOpenPushLiveUpdates
        {
            public OpenPushLiveUpdateResult HandleAndroidPayload(string dataJson, string collapseId)
            {
                using (var activity = AndroidBridgeClass.Activity())
                    return OpenPushLiveUpdateNames.Result(B.CallStatic<string>("handleLiveUpdate", activity, dataJson, collapseId ?? ""));
            }
            public OpenPushLiveUpdateCapability Capability
            {
                get { using (var activity = AndroidBridgeClass.Activity()) return OpenPushLiveUpdateNames.Capability(B.CallStatic<string>("liveUpdateCapability", activity)); }
            }
            public bool OpenPromotionSettings()
            {
                using (var activity = AndroidBridgeClass.Activity()) return B.CallStatic<bool>("openPromotionSettings", activity);
            }
        }
    }
}
