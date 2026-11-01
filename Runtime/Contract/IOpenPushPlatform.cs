// Internal platform contract. Implementations: OpenPushStubPlatform (Editor/desktop), AndroidPlatform, IOSPlatform. Events and callbacks may fire on any thread; the facade marshals them to the Unity main thread through OpenPushHost.Dispatcher.
using System;
using System.Collections.Generic;
using OpenPushSDK.Core;

namespace OpenPushSDK.Platform
{
    // Thread contract: platform events and callbacks may fire on any thread; the facade marshals
    // them to the Unity main thread. IOpenPushUser has no Changed event: neither native SDK exposes
    // a user observer, so the facade synthesizes it (OpenPushUserStateTracker). There is no Language
    // accessor for the same reason (no native accessor).
    internal interface IOpenPushPlatform
    {
        string Name { get; }
        IOpenPushCore Core { get; }
        IOpenPushUser User { get; }
        IOpenPushPushSubscription PushSubscription { get; }
        IOpenPushNotifications Notifications { get; }
        IOpenPushInAppMessages InAppMessages { get; }
        IOpenPushLiveActivities LiveActivities { get; }
        IOpenPushLiveUpdates LiveUpdates { get; }
        IOpenPushVerify Verify { get; }
    }
    internal interface IOpenPushCore
    {
        void Initialize(OpenPushInitOptions options);
        bool IsInitialized { get; }
        string CurrentToken { get; }
        string SdkVersion { get; }
        void UpdateCredential(string credential);
        void Register(string token, string externalId);
        void Receipt(string type, string messageId, Action then);
        void TrackEvent(string name, string propertiesJson);
        void Login(string externalId, string authHash, Action<OpenPushIdentityResult> done);
        void Logout(Action<OpenPushIdentityResult> done);
        void OnAppForegrounded();
        void ClearToken();
        event Action<OpenPushLogLevel, string> Log;
        event Action Registered;
        event Action<string> RegistrationFailed;
        event Action<string> IdentityVerificationRequired;
    }
    internal interface IOpenPushUser
    {
        void AddTags(IReadOnlyDictionary<string, string> tags);
        void RemoveTags(IReadOnlyList<string> keys);
        Dictionary<string, string> GetTags();
        void AddAliases(IReadOnlyDictionary<string, string> aliases);
        void RemoveAliases(IReadOnlyList<string> labels);
        string ExternalId { get; }
        // Phone numbers arrive already E.164 (the facade normalizes and refuses the rest). Each
        // write is fire-and-forget like a tag: the native SDK persists it and retries it.
        void AddSms(string number);
        void RemoveSms(string number);
        void AddWhatsApp(string number);
        void RemoveWhatsApp(string number);
        // Email addresses arrive already trimmed and lower-cased (the facade checks and refuses the rest).
        // Fire-and-forget like the phone writes: the native SDK persists and retries them.
        void AddEmail(string address);
        void RemoveEmail(string address);
    }
    internal interface IOpenPushPushSubscription
    {
        string Id { get; }
        string Token { get; }
        bool OptedIn { get; }
        int? Status { get; }
        void OptIn();
        void OptOut();
        event Action<PushSubscriptionState, PushSubscriptionState> Changed; // (previous or null, current)
    }
    internal interface IOpenPushNotifications
    {
        bool Permission { get; }
        bool CanRequestPermission { get; }
        void RequestPermission(bool fallbackToSettings, Action<bool> done);
        void RequestProvisionalPermission(Action<bool> done);
        void ClearAll();
        void Remove(string messageId);
        void SetBadgeCount(int count);
        /// <summary>
        /// A host-owned integration forwarded a tapped payload (a flat JSON string map). The
        /// platform posts the <c>clicked</c> receipt and raises <see cref="Clicked"/> exactly
        /// once, through the same path a native tap takes. The facade does not raise the click
        /// itself, and the native click listener must not raise it a second time.
        /// </summary>
        void ProcessClick(string dataJson);

        /// <summary>
        /// A host-owned integration forwarded a payload received in the foreground. The facade
        /// has already run the C# ForegroundWillDisplay handlers, so the platform posts the
        /// <c>received</c> receipt only. It MUST NOT raise <see cref="ForegroundWillDisplay"/>
        /// and MUST NOT allocate a completion id for this payload. A background (data-only)
        /// payload skips the foreground handlers instead; a platform that supports background
        /// pushes raises <see cref="BackgroundMessage"/> for it, once.
        /// </summary>
        void ProcessForeground(string dataJson);

        /// <summary>
        /// Claims a host-forwarded payload before any handler runs. False means this push was
        /// already handled on this device (a resent copy) or is being handled now: skip it.
        /// A true answer MUST be followed by exactly one <see cref="FinishReceived"/>.
        /// </summary>
        bool BeginReceived(string dataJson);

        /// <summary>
        /// Ends a claim from <see cref="BeginReceived"/>. <c>handled</c> records the push so a later
        /// copy is dropped; false releases it so a later copy can still be shown.
        /// </summary>
        void FinishReceived(string dataJson, bool handled);

        /// <summary>
        /// A host renderer posted this notification. The platform posts the <c>confirmed</c>
        /// receipt only and raises no event.
        /// </summary>
        void ProcessDisplayed(string dataJson);
        event Action<bool> PermissionChanged;
        event Action<OpenPushClickEvent> Clicked;
        event Action<OpenPushForegroundEvent> ForegroundWillDisplay;
        /// <summary>Background (data-only) pushes. Android only; iOS never raises it.</summary>
        event Action<OpenPushBackgroundMessage> BackgroundMessage;
    }
    internal interface IOpenPushInAppMessages
    {
        bool Paused { get; set; }
        void AddTriggers(string triggersJson);
        void RemoveTriggers(IReadOnlyList<string> keys);
        void ClearTriggers();
        void Fetch();
        event Action<string, string> Lifecycle;
        event Action<string, OpenPushInAppClick> Clicked;
    }
    internal interface IOpenPushLiveActivities
    {
        void SetupDefault(LiveActivitySetupOptions options);
        void StartDefault(string activityId, string attributesJson, string contentJson);
        void Enter(string activityId, string token, string activityType, Action<bool> done);
        void Exit(string activityId, Action<bool> done);
        void SetPushToStartToken(string activityType, string token, Action<bool> done);
        void RemovePushToStartToken(string activityType, Action<bool> done);
    }
    /// <summary>
    /// Phone codes. Each call answers <c>done</c> exactly once, on any thread, with a verification or
    /// an error. The native side holds each verification it returned (it carries the full number,
    /// which never crosses into C#), so check and resend name one by id.
    /// </summary>
    internal interface IOpenPushVerify
    {
        void Start(string phone, OpenPushVerifyChannel channel, string locale, Action<OpenPushVerifyResult> done);
        void Check(string verificationId, string code, Action<OpenPushVerifyResult> done);
        void Resend(string verificationId, OpenPushVerifyChannel channel, Action<OpenPushVerifyResult> done);
    }
    internal interface IOpenPushLiveUpdates
    {
        OpenPushLiveUpdateResult HandleAndroidPayload(string dataJson, string collapseId);
        OpenPushLiveUpdateCapability Capability { get; }
        bool OpenPromotionSettings();
    }
}
