using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    /// <summary>
    /// The Editor and desktop platform: no native SDK, no network. State lives in memory so a
    /// scene (and Feature Lab Demo mode) behaves plausibly; every call is logged at Verbose.
    /// </summary>
    internal sealed class OpenPushStubPlatform : IOpenPushPlatform
    {
        private readonly StubState _state = new StubState();

        public OpenPushStubPlatform()
        {
            var core = new StubCore(_state);
            Core = core; User = new StubUser(_state); PushSubscription = new StubPushSubscription(_state);
            Notifications = new StubNotifications(_state); InAppMessages = new StubInAppMessages(_state);
            LiveActivities = new StubLiveActivities(_state); LiveUpdates = new StubLiveUpdates(_state);
            Verify = new OpenPushStubVerify(_state.Trace);
        }

        public string Name => "stub";
        public IOpenPushCore Core { get; }
        public IOpenPushUser User { get; }
        public IOpenPushPushSubscription PushSubscription { get; }
        public IOpenPushNotifications Notifications { get; }
        public IOpenPushInAppMessages InAppMessages { get; }
        public IOpenPushLiveActivities LiveActivities { get; }
        public IOpenPushLiveUpdates LiveUpdates { get; }
        public IOpenPushVerify Verify { get; }

        private sealed class StubState
        {
            public readonly object Gate = new object();
            public bool Initialized, Permission, Prompted, OptedOut, Paused;
            public string Token, SubscriptionId, ExternalId;
            public readonly Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.Ordinal);
            public readonly Dictionary<string, string> Aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            public Action<OpenPushLogLevel, string> Log = (_, __) => { };
            public Action<PushSubscriptionState, PushSubscriptionState> SubscriptionChanged = (_, __) => { };
            public Action<bool> PermissionChanged = _ => { };

            public void Trace(string call) => Log(OpenPushLogLevel.Verbose, "· stub: " + call);

            public int Status() => OptedOut ? OpenPushNotificationTypes.OptedOut
                : Permission ? OpenPushNotificationTypes.Subscribed
                : Prompted ? OpenPushNotificationTypes.NoPermission : OpenPushNotificationTypes.NeverPrompted;

            public PushSubscriptionState Snapshot() => new PushSubscriptionState
            { Id = SubscriptionId, Token = Token, OptedIn = Permission && Token != null && !OptedOut, Status = Status() };

            public void Mutate(Action change)
            {
                PushSubscriptionState before, after;
                lock (Gate) { before = Snapshot(); change(); after = Snapshot(); }
                if (before.Id != after.Id || before.Token != after.Token || before.OptedIn != after.OptedIn || before.Status != after.Status)
                    SubscriptionChanged(before, after);
            }
        }

        private sealed class StubCore : IOpenPushCore
        {
            private readonly StubState _s;
            public StubCore(StubState s) { _s = s; s.Log = (l, m) => Log?.Invoke(l, m); }
            public bool IsInitialized => _s.Initialized;
            public string CurrentToken => _s.Token;
            public string SdkVersion => "stub";
            public void Initialize(OpenPushInitOptions options) { _s.Initialized = true; _s.Trace("initialize " + options?.AppId + " (" + options?.Wrapper + ")"); }
            public void UpdateCredential(string credential) => _s.Trace("updateCredential");
            public void Register(string token, string externalId)
            {
                _s.Trace("register");
                _s.Mutate(() => { _s.Token = token; _s.SubscriptionId ??= "stub-subscription"; if (!string.IsNullOrEmpty(externalId)) _s.ExternalId = externalId; });
                Registered?.Invoke();
            }
            public void Receipt(string type, string messageId, Action then) { _s.Trace("receipt " + type + " " + messageId); then?.Invoke(); }
            public void TrackEvent(string name, string propertiesJson) => _s.Trace("trackEvent " + name);
            public void Login(string externalId, string authHash, Action<OpenPushIdentityResult> done)
            { _s.Trace("login " + externalId); lock (_s.Gate) _s.ExternalId = externalId; done?.Invoke(OpenPushIdentityResult.Ok()); }
            public void Logout(Action<OpenPushIdentityResult> done)
            { _s.Trace("logout"); lock (_s.Gate) _s.ExternalId = null; done?.Invoke(OpenPushIdentityResult.Ok()); }
            public void OnAppForegrounded() => _s.Trace("onAppForegrounded");
            public void ClearToken() { _s.Trace("clearToken"); _s.Mutate(() => _s.Token = null); }
            public event Action<OpenPushLogLevel, string> Log;
            public event Action Registered;
            public event Action<string> RegistrationFailed { add { } remove { } }
            public event Action<string> IdentityVerificationRequired { add { } remove { } }
        }

        private sealed class StubUser : IOpenPushUser
        {
            private readonly StubState _s;
            public StubUser(StubState s) { _s = s; }
            public string ExternalId { get { lock (_s.Gate) return _s.ExternalId; } }
            public void AddTags(IReadOnlyDictionary<string, string> tags)
            { _s.Trace("addTags"); lock (_s.Gate) foreach (var p in tags) if (string.IsNullOrEmpty(p.Value)) _s.Tags.Remove(p.Key); else _s.Tags[p.Key] = p.Value; }
            public void RemoveTags(IReadOnlyList<string> keys) { _s.Trace("removeTags"); lock (_s.Gate) foreach (var k in keys) _s.Tags.Remove(k); }
            public Dictionary<string, string> GetTags() { lock (_s.Gate) return new Dictionary<string, string>(_s.Tags); }
            public void AddAliases(IReadOnlyDictionary<string, string> aliases)
            { _s.Trace("addAliases"); lock (_s.Gate) foreach (var p in aliases) _s.Aliases[p.Key] = p.Value; }
            public void RemoveAliases(IReadOnlyList<string> labels) { _s.Trace("removeAliases"); lock (_s.Gate) foreach (var l in labels) _s.Aliases.Remove(l); }
            // Nothing reads phone numbers back, so the stub only traces the write (never the number).
            public void AddSms(string number) => _s.Trace("addSms");
            public void RemoveSms(string number) => _s.Trace("removeSms");
            public void AddWhatsApp(string number) => _s.Trace("addWhatsApp");
            public void RemoveWhatsApp(string number) => _s.Trace("removeWhatsApp");
            public void AddEmail(string address) => _s.Trace("addEmail");
            public void RemoveEmail(string address) => _s.Trace("removeEmail");
        }

        private sealed class StubPushSubscription : IOpenPushPushSubscription
        {
            private readonly StubState _s;
            public StubPushSubscription(StubState s) { _s = s; s.SubscriptionChanged = (p, c) => Changed?.Invoke(p, c); }
            public string Id { get { lock (_s.Gate) return _s.SubscriptionId; } }
            public string Token { get { lock (_s.Gate) return _s.Token; } }
            public bool OptedIn { get { lock (_s.Gate) return _s.Snapshot().OptedIn; } }
            public int? Status { get { lock (_s.Gate) return _s.Status(); } }
            public void OptIn() { _s.Trace("optIn"); _s.Mutate(() => { _s.OptedOut = false; _s.Prompted = true; _s.Permission = true; }); }
            public void OptOut() { _s.Trace("optOut"); _s.Mutate(() => _s.OptedOut = true); }
            public event Action<PushSubscriptionState, PushSubscriptionState> Changed;
        }

        private sealed class StubNotifications : IOpenPushNotifications
        {
            private readonly StubState _s;
            public StubNotifications(StubState s) { _s = s; s.PermissionChanged = g => PermissionChanged?.Invoke(g); }
            public bool Permission { get { lock (_s.Gate) return _s.Permission; } }
            public bool CanRequestPermission { get { lock (_s.Gate) return !_s.Prompted; } }
            public void RequestPermission(bool fallbackToSettings, Action<bool> done)
            {
                _s.Trace("requestPermission");
                var changed = false;
                _s.Mutate(() => { changed = !_s.Permission; _s.Prompted = true; _s.Permission = true; });
                if (changed) _s.PermissionChanged(true);
                done?.Invoke(true);
            }
            public void RequestProvisionalPermission(Action<bool> done) { _s.Trace("requestProvisionalPermission"); done?.Invoke(false); }
            public void ClearAll() => _s.Trace("clearAll");
            public void Remove(string messageId) => _s.Trace("remove " + messageId);
            public void SetBadgeCount(int count) => _s.Trace("setBadgeCount " + count);
            public void ProcessClick(string dataJson)
            {
                _s.Trace("processClick");
                var data = OpenPushWire.StringMap(OpenPushWire.Parse("{\"d\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) + "}"), "d");
                Clicked?.Invoke(new ClickEventWire { data = data }.ToPublic());
            }
            public void ProcessForeground(string dataJson)
            {
                _s.Trace("processForeground");
                // Mirrors Android: a forwarded background push goes to the background listeners.
                var data = OpenPushWire.StringMap(OpenPushWire.Parse("{\"d\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) + "}"), "d");
                var message = new BackgroundMessageWire { data = data }.ToPublic();
                if (message.Notification.IsBackground) BackgroundMessage?.Invoke(message);
            }
            public bool BeginReceived(string dataJson) => true;
            public void FinishReceived(string dataJson, bool handled) { }
            public void ProcessDisplayed(string dataJson) => _s.Trace("processDisplayed");
            public event Action<bool> PermissionChanged;
            public event Action<OpenPushClickEvent> Clicked;
            public event Action<OpenPushForegroundEvent> ForegroundWillDisplay { add { } remove { } }
            public event Action<OpenPushBackgroundMessage> BackgroundMessage;
        }

        private sealed class StubInAppMessages : IOpenPushInAppMessages
        {
            private readonly StubState _s;
            public StubInAppMessages(StubState s) { _s = s; }
            public bool Paused { get { lock (_s.Gate) return _s.Paused; } set { lock (_s.Gate) _s.Paused = value; } }
            public void AddTriggers(string triggersJson) => _s.Trace("addTriggers " + triggersJson);
            public void RemoveTriggers(IReadOnlyList<string> keys) => _s.Trace("removeTriggers");
            public void ClearTriggers() => _s.Trace("clearTriggers");
            public void Fetch() { _s.Trace("fetch (Demo fixture only)"); OpenPushSDK.OpenPushHost.ShowDemoInAppFixture?.Invoke(); }
            public event Action<string, string> Lifecycle { add { } remove { } }
            public event Action<string, OpenPushInAppClick> Clicked { add { } remove { } }
        }

        private sealed class StubLiveActivities : IOpenPushLiveActivities
        {
            private readonly StubState _s;
            public StubLiveActivities(StubState s) { _s = s; }
            public void SetupDefault(LiveActivitySetupOptions options) => _s.Trace("liveActivities.setupDefault: " + OpenPushLiveActivityNotes.IOSOnly);
            public void StartDefault(string activityId, string attributesJson, string contentJson) => _s.Trace("liveActivities.startDefault " + activityId);
            public void Enter(string activityId, string token, string activityType, Action<bool> done) { _s.Trace("liveActivities.enter"); done?.Invoke(false); }
            public void Exit(string activityId, Action<bool> done) { _s.Trace("liveActivities.exit"); done?.Invoke(false); }
            public void SetPushToStartToken(string activityType, string token, Action<bool> done) { _s.Trace("liveActivities.setPushToStartToken"); done?.Invoke(false); }
            public void RemovePushToStartToken(string activityType, Action<bool> done) { _s.Trace("liveActivities.removePushToStartToken"); done?.Invoke(false); }
        }

        private sealed class StubLiveUpdates : IOpenPushLiveUpdates
        {
            private readonly StubState _s;
            public StubLiveUpdates(StubState s) { _s = s; }
            public OpenPushLiveUpdateResult HandleAndroidPayload(string dataJson, string collapseId)
            { _s.Trace("liveUpdates.handle"); return OpenPushLiveUpdateResult.Unsupported; }
            public OpenPushLiveUpdateCapability Capability => OpenPushLiveUpdateCapability.UnsupportedPlatform;
            public bool OpenPromotionSettings() => false;
        }
    }
}
