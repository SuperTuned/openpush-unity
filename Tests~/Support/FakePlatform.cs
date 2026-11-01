using System;
using System.Collections.Generic;
using OpenPushSDK.Core;
using OpenPushSDK.Platform;

namespace OpenPushSDK.Tests
{
    /// Records every call and lets a test raise every platform event, from any thread it likes.
    internal sealed class FakePlatform : IOpenPushPlatform
    {
        public readonly List<string> Calls = new List<string>();
        public readonly FakeCore CoreFake;
        public readonly FakeUser UserFake;
        public readonly FakePushSubscription SubscriptionFake;
        public readonly FakeNotifications NotificationsFake;
        public readonly FakeInAppMessages InAppFake;
        public readonly FakeLiveActivities LiveActivitiesFake;
        public readonly FakeLiveUpdates LiveUpdatesFake;
        public readonly FakeVerify VerifyFake;

        public FakePlatform()
        {
            CoreFake = new FakeCore(Calls); UserFake = new FakeUser(Calls);
            SubscriptionFake = new FakePushSubscription(Calls); NotificationsFake = new FakeNotifications(Calls);
            InAppFake = new FakeInAppMessages(Calls); LiveActivitiesFake = new FakeLiveActivities(Calls);
            LiveUpdatesFake = new FakeLiveUpdates(Calls); VerifyFake = new FakeVerify(Calls);
        }

        public string Name { get; set; } = "fake";
        public IOpenPushCore Core => CoreFake;
        public IOpenPushUser User => UserFake;
        public IOpenPushPushSubscription PushSubscription => SubscriptionFake;
        public IOpenPushNotifications Notifications => NotificationsFake;
        public IOpenPushInAppMessages InAppMessages => InAppFake;
        public IOpenPushLiveActivities LiveActivities => LiveActivitiesFake;
        public IOpenPushLiveUpdates LiveUpdates => LiveUpdatesFake;
        public IOpenPushVerify Verify => VerifyFake;
    }

    internal sealed class FakeCore : IOpenPushCore
    {
        private readonly List<string> _calls;
        public FakeCore(List<string> calls) { _calls = calls; }
        public OpenPushInitOptions InitOptions;
        public string Credential;
        public Action<OpenPushIdentityResult> PendingIdentity;
        /// Runs inside Initialize, standing in for what the native SDK does to shared storage there.
        public Action OnInitialize;
        public bool IsInitialized { get; set; }
        public string CurrentToken { get; set; }
        public string SdkVersion { get; set; } = "9.9.9";
        public void Initialize(OpenPushInitOptions options)
        { InitOptions = options; IsInitialized = true; Credential = options.Credential?.Invoke(); _calls.Add("Initialize " + options.AppId); OnInitialize?.Invoke(); }
        public void UpdateCredential(string credential) { Credential = credential; _calls.Add("UpdateCredential " + credential); }
        public void Register(string token, string externalId) => _calls.Add($"Register {token} {externalId}");
        public void Receipt(string type, string messageId, Action then) { _calls.Add($"Receipt {type} {messageId}"); then?.Invoke(); }
        public void TrackEvent(string name, string propertiesJson) => _calls.Add($"TrackEvent {name} {propertiesJson}");
        public void Login(string externalId, string authHash, Action<OpenPushIdentityResult> done)
        { _calls.Add($"Login {externalId} {authHash}"); PendingIdentity = done; }
        public void Logout(Action<OpenPushIdentityResult> done) { _calls.Add("Logout"); PendingIdentity = done; }
        public void OnAppForegrounded() => _calls.Add("OnAppForegrounded");
        public void ClearToken() => _calls.Add("ClearToken");
        public event Action<OpenPushLogLevel, string> Log;
        public event Action Registered;
        public event Action<string> RegistrationFailed;
        public event Action<string> IdentityVerificationRequired;
        public void RaiseLog(OpenPushLogLevel level, string message) => Log?.Invoke(level, message);
        public void RaiseRegistered() => Registered?.Invoke();
        public void RaiseRegistrationFailed(string message) => RegistrationFailed?.Invoke(message);
        public void RaiseIdentityVerificationRequired(string externalId) => IdentityVerificationRequired?.Invoke(externalId);
    }

    internal sealed class FakeUser : IOpenPushUser
    {
        private readonly List<string> _calls;
        public FakeUser(List<string> calls) { _calls = calls; }
        public readonly Dictionary<string, string> Tags = new Dictionary<string, string>();
        public readonly Dictionary<string, string> Aliases = new Dictionary<string, string>();
        public string ExternalId { get; set; }
        public bool ThrowOnAddTags;
        public void AddTags(IReadOnlyDictionary<string, string> tags)
        { if (ThrowOnAddTags) throw new InvalidOperationException("native AddTags failed"); foreach (var p in tags) Tags[p.Key] = p.Value; _calls.Add("AddTags " + string.Join(",", tags.Keys)); }
        public void RemoveTags(IReadOnlyList<string> keys)
        { foreach (var k in keys) Tags.Remove(k); _calls.Add("RemoveTags " + string.Join(",", keys)); }
        public Dictionary<string, string> GetTags() => new Dictionary<string, string>(Tags);
        public void AddAliases(IReadOnlyDictionary<string, string> aliases)
        { foreach (var p in aliases) Aliases[p.Key] = p.Value; _calls.Add("AddAliases " + string.Join(",", aliases.Keys)); }
        public void RemoveAliases(IReadOnlyList<string> labels)
        { foreach (var l in labels) Aliases.Remove(l); _calls.Add("RemoveAliases " + string.Join(",", labels)); }
        public void AddSms(string number) => _calls.Add("AddSms " + number);
        public void RemoveSms(string number) => _calls.Add("RemoveSms " + number);
        public void AddWhatsApp(string number) => _calls.Add("AddWhatsApp " + number);
        public void RemoveWhatsApp(string number) => _calls.Add("RemoveWhatsApp " + number);
        public void AddEmail(string address) => _calls.Add("AddEmail " + address);
        public void RemoveEmail(string address) => _calls.Add("RemoveEmail " + address);
    }

    internal sealed class FakePushSubscription : IOpenPushPushSubscription
    {
        private readonly List<string> _calls;
        public FakePushSubscription(List<string> calls) { _calls = calls; }
        public string Id { get; set; }
        public string Token { get; set; }
        public bool OptedIn { get; set; }
        public int? Status { get; set; }
        public void OptIn() => _calls.Add("OptIn");
        public void OptOut() => _calls.Add("OptOut");
        public event Action<PushSubscriptionState, PushSubscriptionState> Changed;
        public void RaiseChanged(PushSubscriptionState previous, PushSubscriptionState current) => Changed?.Invoke(previous, current);
    }

    internal sealed class FakeNotifications : IOpenPushNotifications
    {
        private readonly List<string> _calls;
        public FakeNotifications(List<string> calls) { _calls = calls; }
        public bool Permission { get; set; }
        public bool CanRequestPermission { get; set; } = true;
        public Action<bool> PendingPermission;
        public void RequestPermission(bool fallbackToSettings, Action<bool> done)
        { _calls.Add("RequestPermission " + fallbackToSettings); PendingPermission = done; }
        public void RequestProvisionalPermission(Action<bool> done) { _calls.Add("RequestProvisionalPermission"); PendingPermission = done; }
        public void ClearAll() => _calls.Add("ClearAll");
        public void Remove(string messageId) => _calls.Add("Remove " + messageId);
        public void SetBadgeCount(int count) => _calls.Add("SetBadgeCount " + count);
        // Process* honour the IOpenPushNotifications contract: foreground and displayed post a
        // receipt only; click posts a receipt and raises Clicked once, like a native tap.
        public readonly List<string> ProcessClickCalls = new List<string>();
        public readonly List<string> ProcessForegroundCalls = new List<string>();
        public readonly List<string> ProcessDisplayedCalls = new List<string>();
        public int ClicksRaisedByPlatform, ForegroundRaisedByPlatform;
        public void ProcessClick(string dataJson)
        {
            _calls.Add("ProcessClick " + dataJson); ProcessClickCalls.Add(dataJson);
            var data = OpenPushWire.StringMap(OpenPushWire.Parse("{\"d\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) + "}"), "d");
            RaiseClicked(new ClickEventWire { data = data }.ToPublic());
        }
        public void ProcessForeground(string dataJson)
        {
            _calls.Add("ProcessForeground " + dataJson); ProcessForegroundCalls.Add(dataJson);
            // Like Android: a forwarded background push goes to the background listeners.
            var data = OpenPushWire.StringMap(OpenPushWire.Parse("{\"d\":" + (string.IsNullOrEmpty(dataJson) ? "{}" : dataJson) + "}"), "d");
            var message = new BackgroundMessageWire { data = data }.ToPublic();
            if (message.Notification.IsBackground) RaiseBackground(message);
        }
        public void ProcessDisplayed(string dataJson) { _calls.Add("ProcessDisplayed " + dataJson); ProcessDisplayedCalls.Add(dataJson); }
        // Mirrors the native received-message log: a payload recorded as handled is a repeat.
        public readonly HashSet<string> ReceivedHandled = new HashSet<string>();
        public readonly List<string> FinishReceivedCalls = new List<string>();
        public bool BeginReceived(string dataJson) => !ReceivedHandled.Contains(dataJson);
        public void FinishReceived(string dataJson, bool handled)
        {
            FinishReceivedCalls.Add(dataJson + " " + handled);
            if (handled) ReceivedHandled.Add(dataJson);
        }
        public event Action<bool> PermissionChanged;
        public event Action<OpenPushClickEvent> Clicked;
        public event Action<OpenPushForegroundEvent> ForegroundWillDisplay;
        public event Action<OpenPushBackgroundMessage> BackgroundMessage;
        public void RaiseBackground(OpenPushBackgroundMessage message) => BackgroundMessage?.Invoke(message);
        public void RaisePermission(bool granted) => PermissionChanged?.Invoke(granted);
        public void RaiseClicked(OpenPushClickEvent click) { ClicksRaisedByPlatform++; Clicked?.Invoke(click); }
        public void RaiseForeground(OpenPushForegroundEvent foreground) { ForegroundRaisedByPlatform++; ForegroundWillDisplay?.Invoke(foreground); }
    }

    internal sealed class FakeInAppMessages : IOpenPushInAppMessages
    {
        private readonly List<string> _calls;
        public FakeInAppMessages(List<string> calls) { _calls = calls; }
        public bool Paused { get; set; }
        public void AddTriggers(string triggersJson) => _calls.Add("AddTriggers " + triggersJson);
        public void RemoveTriggers(IReadOnlyList<string> keys) => _calls.Add("RemoveTriggers " + string.Join(",", keys));
        public void ClearTriggers() => _calls.Add("ClearTriggers");
        public void Fetch() => _calls.Add("Fetch");
        public event Action<string, string> Lifecycle;
        public event Action<string, OpenPushInAppClick> Clicked;
        public void RaiseLifecycle(string messageId, string phase) => Lifecycle?.Invoke(messageId, phase);
        public void RaiseClicked(string messageId, OpenPushInAppClick click) => Clicked?.Invoke(messageId, click);
    }

    internal sealed class FakeLiveActivities : IOpenPushLiveActivities
    {
        private readonly List<string> _calls;
        public FakeLiveActivities(List<string> calls) { _calls = calls; }
        public Action<bool> Pending;
        public void SetupDefault(LiveActivitySetupOptions options)
            => _calls.Add($"SetupDefault {options.EnablePushToStart} {options.EnablePushToUpdate}");
        public void StartDefault(string activityId, string attributesJson, string contentJson)
            => _calls.Add($"StartDefault {activityId} {attributesJson} {contentJson}");
        public void Enter(string activityId, string token, string activityType, Action<bool> done)
        { _calls.Add($"Enter {activityId} {token} {activityType}"); Pending = done; }
        public void Exit(string activityId, Action<bool> done) { _calls.Add("Exit " + activityId); Pending = done; }
        public void SetPushToStartToken(string activityType, string token, Action<bool> done)
        { _calls.Add($"SetPushToStartToken {activityType} {token}"); Pending = done; }
        public void RemovePushToStartToken(string activityType, Action<bool> done)
        { _calls.Add("RemovePushToStartToken " + activityType); Pending = done; }
    }

    internal sealed class FakeLiveUpdates : IOpenPushLiveUpdates
    {
        private readonly List<string> _calls;
        public FakeLiveUpdates(List<string> calls) { _calls = calls; }
        public OpenPushLiveUpdateResult NextResult = OpenPushLiveUpdateResult.Displayed;
        public OpenPushLiveUpdateResult HandleAndroidPayload(string dataJson, string collapseId)
        { _calls.Add($"HandleAndroidPayload {dataJson} {collapseId}"); return NextResult; }
        public OpenPushLiveUpdateCapability Capability { get; set; } = OpenPushLiveUpdateCapability.Eligible;
        public bool OpenPromotionSettings() { _calls.Add("OpenPromotionSettings"); return true; }
    }

    internal sealed class FakeVerify : IOpenPushVerify
    {
        private readonly List<string> _calls;
        public FakeVerify(List<string> calls) { _calls = calls; }
        /// The last call's completion; a test answers it, from any thread, as the native side would.
        public Action<OpenPushVerifyResult> Pending;
        public void Start(string phone, OpenPushVerifyChannel channel, string locale, Action<OpenPushVerifyResult> done)
        { _calls.Add($"VerifyStart {phone} {channel} {locale}"); Pending = done; }
        public void Check(string verificationId, string code, Action<OpenPushVerifyResult> done)
        { _calls.Add($"VerifyCheck {verificationId} {code}"); Pending = done; }
        public void Resend(string verificationId, OpenPushVerifyChannel channel, Action<OpenPushVerifyResult> done)
        { _calls.Add($"VerifyResend {verificationId} {channel}"); Pending = done; }
    }
}
