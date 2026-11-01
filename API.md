# Public API — 1.2.2

`1.2.2` is the first public release. It runs over the native OpenPush iOS and Android 1.2.2
SDKs, which are vendored inside the package. Badge increments (iOS) and replacing a shown
notification (Android) need no API: the vendored natives handle them.

The package registers the device, names the account it belongs to (`Login`/`Logout`, with
identity verification), carries device tags and user aliases, opts the device in and out of
sends, requests and tracks OS notification permission, reports push-subscription state,
raises click, foreground and in-app events, records Custom Events, drives iOS Live Activities
and Android Live Updates, and stores an installation credential in the platform keystore.
Receipts, sessions, retries and in-app presentation happen inside the native SDKs. It
contains no send surface.

The Editor and desktop players run an in-memory stub platform: calls are logged at
`Verbose`, state (subscription, tags, aliases, external id, permission, token) is kept in
memory, events fire on the main thread, and nothing touches the network.

## OpenPushSDK.OpenPush (static facade)

**Before `Initialize`.** `LogLevel`, `OnLog`, `OnRegistered`, `OnRegistrationFailed`, every
event `+=`/`-=`, read-only getters and the static helpers work at any time. Every call with a
side effect (`Register`, `Login`, `TrackEvent`, `User.*` writes, `PushSubscription.OptIn/OptOut`,
`Notifications.RequestPermission`, in-app and live-feature calls) throws
`InvalidOperationException` until `Initialize` has been called.

**Events are buffered.** Each event keeps up to ten values raised before its first subscriber
and replays them to that subscriber once, so a handler added after `Initialize` still sees a
cold-start click and the first subscription state. `Notifications.ForegroundWillDisplay` is
the exception: with no handler the notification is displayed at once and never replayed.
One throwing handler is logged and does not stop the others.

- `const string DefaultServerUrl`: `https://app.openpush.ai`, the server used
  when `Initialize` is called without one.
- `Initialize(string appId)`: reads the SDK key from the `Resources/OpenPushSettings`
  asset. With no key it refuses through `OnLog` and `OnRegistrationFailed`, without throwing.
- `Initialize(string appId, string sdkKey, string serverUrl = DefaultServerUrl,
  Func<string> credential = null)`: initializes the native SDK. A `null` key throws
  `ArgumentNullException`, a blank one `ArgumentException`; the key is trimmed. `serverUrl`
  is trimmed and loses trailing slashes; blank means `DefaultServerUrl`. An empty `appId` is
  refused through `OnLog` and `OnRegistrationFailed`. The credential closure returns the
  current linked-installation credential or `null`; it is read at `Initialize`, after
  `Login`, and when the app regains focus. When an earlier OpenPush SDK build left state in
  PlayerPrefs, the first `Initialize` also migrates it once (see
  [Upgrading from an earlier OpenPush SDK build](#upgrading-from-an-earlier-openpush-sdk-build)).
- `Login(string externalId, string authHash = null,
  Action<OpenPushIdentityResult> done = null)`: names the account this device
  belongs to. Pass `authHash` when identity verification is enabled for the
  app; compute it on your server, never in the client. `done` runs on the main thread.
- `Logout(Action<OpenPushIdentityResult> done = null)`: detaches the device from
  the named account. The provider token is untouched: the device keeps receiving
  app-wide sends, which is what an anonymous subscription is.
- `event Action<string> OnIdentityVerificationRequired`: the app requires identity
  verification and the SDK holds an external id without an auth hash (for example one
  migrated from an earlier OpenPush SDK build). Call `Login(externalId, authHash)` with a hash from your server.
- `Action<string> OnLog`: bounded operational messages from the Unity layer and the
  native SDKs, which never contain credentials or full tokens. Assigning `null` restores
  a no-op sink. On Android, native lines are also copied to logcat, but only in a debuggable
  build (a Development Build); a release build writes nothing to logcat.
- `OpenPushLogLevel LogLevel`: how much of that stream reaches `OnLog`.
  `Verbose` by default, which is everything. Lines that carry a leading glyph
  use it to signal their level, and that is what the level gates on:

  | Level | Value | Glyph | What it covers |
  | --- | --- | --- | --- |
  | `None` | 0 | — | nothing reaches `OnLog` |
  | `Error` | 1 | `✗` | something the host asked for did not happen |
  | `Warning` | 2 | `⚠` | it happened, but not the way the host meant |
  | `Info` | 3 | `✓` `→` | a request left, or a request landed |
  | `Verbose` | 4 | `•` `·` | routine bookkeeping |

  A line is delivered when its own level is at or below the configured one, so
  `Error` is the quietest useful setting and `None` silences the sink entirely.
  Set it once at startup, before `Initialize` (the level is passed to the native SDK):

  ```csharp
  using OpenPushSDK.Core;   // the enum lives in the Core namespace

  OpenPush.LogLevel = OpenPushLogLevel.Error;
  ```
- `Action OnRegistered`: called after the server accepts the registration.
- `Action<string> OnRegistrationFailed`: called with a user-facing registration
  failure message.
- `event Action<string> Logged`, `event Action Registered`, `event Action<string> RegistrationFailed`:
  the same three hooks for any number of subscribers (`+=`/`-=`). `OnLog`, `OnRegistered` and
  `OnRegistrationFailed` stay assignable with `=` (one handler, replaced by the next assignment).
  Both fire for every line, registration and failure: the property first, then each subscriber in
  the order it subscribed, on the main thread. `Logged` is gated by `LogLevel` like `OnLog`, and
  `RegistrationFailed` also hears an `Initialize` refusal. A handler that throws is logged and
  does not stop the others.
- `string CurrentToken`: the provider token the native SDK holds (read-only).
- `bool IsInitialized`: whether the native SDK is initialized.
- `string SdkVersion`: the native SDK version (`stub` in the Editor and on desktop).
- `string WrapperVersion`: this package's version, `1.2.2`. The registration names the
  wrapper as `unity/1.2.2`.
- `[Obsolete] Flush()`: a no-op that logs. The native SDKs persist and send on their own.
- `Register(string token)`: hands a provider token to the native SDK. Only Android Forward
  (HostOwned) delivery and tests need it; Android Automatic delivery and iOS obtain the token
  themselves. The registration carries the identity the last `Login` set. A `null` or empty
  token is ignored with a log line.
- `[Obsolete] Register(string token, string externalId)`: kept so existing calls compile and
  behave as before. An identity is attached only through login, so a non-empty `externalId`
  other than the current one is logged in first, as `Login(externalId)` without an auth hash
  would, and the registration then carries it (`User.Changed` reports it). `null`, empty or
  the current id registers as `Register(token)`. New code calls `Login(externalId, authHash)`
  and `Register(token)`.
- `TrackEvent(string name, IReadOnlyDictionary<string, object> properties = null)`:
  records a Custom Event for Journey entry and wait-until conditions. The native SDK
  validates, persists, batches and retries it.
- `Receipt(string type, string messageId, Action then = null)`: posts `received`,
  `confirmed`, or `clicked` evidence through the native SDK. `then` runs on the main thread.
  Automatic delivery, iOS and the `Process*` calls post receipts themselves.
- `MessageId(IReadOnlyDictionary<string, string> data)`: extracts
  `op_message_id` from a data payload, or `null`.
- `ClearToken()`: asks the native SDK to forget the provider token.
- `ShortToken(string token)`: returns a display-safe abbreviated token.
- `HexToken(byte[] token)`: converts raw APNs token bytes to lowercase hex.

### OpenPush.User

- `AddTag(string key, string value)` / `AddTags(IReadOnlyDictionary<string, string> tags)`:
  sets tags on the native SDK. An empty key is ignored with a log line; an empty map does
  nothing.
- `RemoveTag(string key)` / `RemoveTags(params string[] keys)`: deletes tags.
- `GetTags()`: a copy of the tags the native SDK holds; never null.
- `AddAlias(string label, string id)` / `AddAliases(IReadOnlyDictionary<string, string> aliases)`:
  adds or replaces aliases, second labels the server can address this user by alongside
  `external_id`. An empty label is ignored with a log line.
- `RemoveAlias(string label)` / `RemoveAliases(params string[] labels)`: deletes aliases.
- `string ExternalId`: the currently named identity, or null.
- `bool AddSms(string number)` / `bool RemoveSms(string number)` /
  `bool AddWhatsApp(string number)` / `bool RemoveWhatsApp(string number)`: adds this
  user's SMS or WhatsApp number, or opts it out. The number is normalised as the server does it
  (every character but digits and `+` removed, a leading `00` read as `+`) and must then be
  E.164: `+` and 8 to 15 digits, the first not `0`. Anything else returns `false`, is logged without the number, and sends nothing. `true` means the write was
  handed to the native SDK, which stores and retries it for the user logged in at the call. A new
  number is added **not verified** and receives nothing until a code from `OpenPush.Verify` is
  approved.
- `bool AddEmail(string address)` / `bool RemoveEmail(string address)`: adds this user's
  email address, or removes its subscription. The address is trimmed and lower-cased and must then
  be at most 254 characters with one `@`, something before it and a dot in the domain; anything
  else returns `false`, is logged without the address, and sends nothing. `true` means the write
  was handed to the native SDK, which stores and retries it for the user logged in at the call.
  `AddEmail` subscribes the address at once, unless this app's suppression list has it (it
  unsubscribed, bounced or complained): then it is stored but never emailed. `RemoveEmail` is not
  an unsubscribe: no suppression is written, and a later `AddEmail` subscribes the address again.
- `PushSubscriptionAccessor PushSubscription { get; }`: live `Id`, `Token` and `OptedIn`
  with `OptIn()`, `OptOut()`, `Snapshot()` and a `Changed` event (the same event as
  `OpenPush.PushSubscription.Changed`).
- `event Action<OpenPushUserState> Changed`: fires when the account this device
  belongs to changes: `Login`, `Logout`, an external id restored by the PlayerPrefs migration,
  and the registration that assigns the subscription id. It never fires while both
  halves are still unknown.

With identity verification enabled, tag and alias writes pass the same gate as
`external_id`; a rejected write is logged. The server caps aliases at 10 per user and
names a refused label (over the cap, or the reserved `external_id`) in its response,
which the native SDK logs.

### OpenPush.Verify

Phone codes. Each call is one request with no retry; the task completes on the Unity
main thread with the verification, or faults with an `OpenPushVerifyException`.

- `Task<OpenPushVerification> StartAsync(string phone, OpenPushVerifyChannel channel = Auto,
  string locale = null)`: sends a code. A number that is not E.164 faults with `InvalidNumber`
  before anything is sent; `locale` (`"en"`, `"pt-BR"`) picks the message language, null leaves
  it to the server.
- `Task<OpenPushVerification> CheckAsync(OpenPushVerification verification, string code)`: checks
  the code the player typed (trimmed). A wrong code is not an error: the verification comes back
  `Pending` with `Attempts` counted up.
- `Task<OpenPushVerification> ResendAsync(OpenPushVerification verification,
  OpenPushVerifyChannel? channel = null)`: sends the code again, on `channel` or, when null, the
  channel it last went out on. Within 30 seconds of the last send it faults with `TooSoon`.

`CheckAsync` and `ResendAsync` throw `ArgumentNullException` for a null verification, and all
three throw `InvalidOperationException` before `Initialize`, as every other call does. The full
number never reaches C#: the native SDK holds each verification in memory, by `Id`, for check and
resend. In the Editor the stub sends nothing and accepts `123456`.

### OpenPush.PushSubscription

- `OptIn()`: clears the explicit opt-out; the native SDK requests permission when it is
  missing.
- `OptOut()`: status `-2`. The subscription and the provider token both survive: this
  stops sends, it does not delete anything, and it is reversible. The opt-out survives
  process restarts.
- `bool OptedIn { get; }`: OS permission **and** a provider token **and** `OptOut()` was
  not called.
- `event Action<PushSubscriptionState> Changed`: fires with the new state when `Id`,
  `Token`, or `OptedIn` changes.
- `event Action<PushSubscriptionState, PushSubscriptionState> ChangedWithPrevious`: the
  same moments, with the state that was replaced (`null` on the first) and the new one.

`OpenPush.User.PushSubscription` (a property) and `OpenPush.PushSubscription` (this
nested static class) are members of different declaring types and both stay
available.

### OpenPush.Notifications

With Android Automatic delivery and on iOS the native SDK renders notifications, posts
receipts, and raises `Clicked` and `ForegroundWillDisplay` on its own. The `Process*`
calls are for Android HostOwned delivery.

- `bool Permission { get; }`: the OS notification-permission state the native SDK caches.
- `int NotificationTypes { get; }`: the device value before an explicit opt-out is
  applied, computed from the permission, whether it can still be requested and the
  subscription status. Any positive value means subscribed.
- `bool CanRequestPermission { get; }`: true while the OS prompt can still be shown. On
  Android 13+ the package records that it asked and then uses Android's rationale state to
  tell a requestable denial from a final one.
- `event Action<bool> PermissionChanged`: the cached permission changed.
- `RequestPermission(bool fallbackToSettings = false, Action<bool> done = null)`:
  asks the OS. On Android 13+ the prompt goes through `UnityEngine.Android.Permission` and
  the result is handed to the native SDK; with `fallbackToSettings`, a request after a
  permanent denial opens the app's notification settings. Below Android 13 there is no
  runtime prompt and `done` reports the current permission. `done` runs on the main thread.
- `Task<bool> RequestPermissionAsync(bool fallbackToSettings = false)`: the
  task-shaped equivalent.
- `RequestProvisionalPermission(Action<bool> done = null)`: iOS provisional (quiet)
  authorization. Android, the Editor and desktop players report `false` and change nothing.
- `event Action<OpenPushClickEvent> Clicked`: a notification tap opened the app. A tap that
  launched the app is replayed to the first subscriber.
- `event Action<OpenPushForegroundEvent> ForegroundWillDisplay`: a push arrived with the
  app foregrounded. Not buffered: with no handler it is displayed at once.
- `event Action<OpenPushBackgroundMessage> BackgroundMessageReceived` (Android): a
  background (data-only) push arrived — `op_background` = `"1"` with no title or body. It is never
  displayed, and its `received` receipt is already posted. Handlers run on the main thread; while
  the game is paused in the background they run when it resumes. A push that arrives before
  `Initialize` has run in this process (one that starts a killed app) is logged and dropped by the
  native SDK, so work that must run then belongs in a native Android plugin. Buffered like
  `Clicked`: values raised before the first handler are replayed to it (newest ten). iOS never
  raises it: iOS background pushes use `content-available` and reach the native SDK only for
  their receipt.
- `bool ProcessForeground(IReadOnlyDictionary<string, string> data)`: HostOwned hosts forward
  a foreground message. The `ForegroundWillDisplay` handlers run now, on the calling thread,
  exactly once; the native SDK then posts only the `received` receipt. Returns `true` when a
  handler called `PreventDefault()`, or on Android when this push was already handled on the
  device (a copy the server re-sent) — no handler runs and no receipt is posted for that repeat.
  Either way, do not display it. A background push (`Notification.IsBackground`) skips the
  `ForegroundWillDisplay` handlers, is raised through `BackgroundMessageReceived`, and returns
  `true`. A `null` payload is safe.
- `ProcessDisplayed(IReadOnlyDictionary<string, string> data)`: records a host-rendered
  display (posts `confirmed`, raises nothing). Call it right after `NotificationManager.notify`
  succeeds, not for a suppressed or failed presentation.
- `ProcessClick(IReadOnlyDictionary<string, string> data)`: HostOwned hosts forward a tap on a
  notification their service rendered. The native SDK posts `clicked` and raises `Clicked`
  once; this call raises nothing itself. Ignored on iOS, where taps are captured.
- `OnAppForegrounded()`: tells the native SDK the app came to the foreground, for hosts
  that drive their own pause lifecycle. The native SDKs observe the lifecycle themselves.
- `ClearAll()`: removes every notification this app has already delivered from the shade
  or notification centre.
- `Remove(string messageId)`: removes the delivered notifications of one OpenPush message.
  Android removes what the OpenPush renderer posted (Automatic delivery); iOS matches
  `op_message_id` in the notification centre. An empty id does nothing.
- `SetBadgeCount(int count)`: sets the iOS app icon badge (negative values count as 0).
  A no-op, logged at `Verbose`, elsewhere. The native SDK also stores the count in the
  App Group, and `ClearAll()` resets it to 0, so a push sent with a badge increment counts up from
  what the app last set.

#### Badge increments (iOS) and replacing a notification (Android)

Both are payload features the vendored natives handle; there is no C# API.

- **Badge increment (iOS).** A push sent with `platform_options.ios.badge_increment` carries
  `op_badge_increment`. The injected notification service extension adds it to the badge count
  stored in the App Group, shows the sum and stores it back. It needs the App Group the package
  already configures for the extension (`OpenPushSettings.AppGroupIdentifier`); without one the badge is
  left unchanged. Android ignores the key: launchers own app-icon badges.
- **Replace a shown notification (Android).** A push sent with
  `platform_options.android.replace: true` and a collapse key carries `op_android_replace` = `"1"`;
  the native renderer posts it under the collapse key, so a newer push replaces the one on screen.
  Without it, pushes stack as before. Automatic delivery only: a Forward (HostOwned) renderer
  decides for itself.

#### `notification_types` and `PushSubscriptionState.Status`

| Value | Meaning |
|---:|---|
| positive | subscribed; on iOS the authorization bitmask (badge `1`, sound `2`, alert `4`, CarPlay `8`, critical alert `16`, app settings `32`, provisional `64`, announcement `128`, time-sensitive `256`) |
| `0` | denied / no permission |
| `-2` | explicitly opted out |
| `-18` | never prompted |
| `-19` | prompt started but unanswered |

`PushSubscriptionState.Status` carries the native wire status. iOS reports every value.
Android reports only the not-answered values `-18`/`-19` and `null` otherwise; the Editor
reports `null`.

### Threading

Every event and callback runs on the Unity main thread. Native callbacks are posted through
Unity's main-thread `SynchronizationContext`; nothing blocks a native thread on Unity.
`ForegroundWillDisplay` is answered once all handlers return: displayed unless one called
`PreventDefault()`. On iOS `Display()` after `PreventDefault()` presents it within the native
25-second window; on Android it is logged and ignored. Android waits at most one second for
the handlers' answer and then displays. The one handler call that is not posted is
`ProcessForeground`, which runs the handlers on the calling thread, so call it from the main
thread. `Notifications.Permission`, `PushSubscription.OptedIn` and `User.PushSubscription`
are plain reads of state the native SDK caches.

### Message IDs are strings

The payload dictionary is `IReadOnlyDictionary<string, string>`, which cannot
carry a numeric `op_message_id`. Stringify a numeric message ID before handing a
payload to the SDK, otherwise `MessageId` returns `null` and the receipt is dropped.

### Upgrading from an earlier OpenPush SDK build

If you used an earlier OpenPush SDK build before 1.2.2 whose C# layer kept its state in
PlayerPrefs, the first `Initialize` replays that state onto the native SDK, once (guarded by
the `openpush.migrated.1.1.5` PlayerPrefs key):

- The device credential is not moved: the host keeps passing its `DeviceCredentialStore`
  closure, so the server keeps the same subscription id.
- An explicit opt-out is replayed with `OptOut()`, tags with `AddTags`, aliases with
  `AddAliases` (empty values are skipped).
- In Android HostOwned mode the stored provider token is registered again. Automatic
  delivery and iOS fetch a fresh token.
- A stored external id (not detached by `Logout`) is logged back in without an auth hash
  when `OpenPushSettings.ReloginWithoutAuthHash` is true (the default), and
  `User.Changed` follows. Otherwise `OnIdentityVerificationRequired(externalId)` is raised
  and the device stays anonymous.
- In-app dismissals are not migrated, so a dismissed message may show once more.
- The legacy keys are deleted afterwards. A failure is logged, the keys are kept, and the
  migration retries on the next launch.

## OpenPushSDK.Core

Pure C# (no `UnityEngine`).

### OpenPushConfiguration

`OpenPushConfiguration(string appID, string sdkKey,
string serverURL = OpenPushConfiguration.DefaultServerUrl,
string platform = null, string tokenKey = "openpush.deviceToken")` exposes `ServerURL`,
`AppID`, `SdkKey`, `Platform`, `TokenKey`, and `Url(path)`; a `null` argument throws
`ArgumentNullException` and trailing slashes are removed from the server URL. It is kept
for source compatibility: with `Attach` removed, nothing in the package takes one.
`ResolveSdkKey(explicitKey, bundledKey)` returns the first non-blank key, trimmed, and
`SettingsResourceName` is `OpenPushSettings`.

### OpenPushIdentityResult

The outcome of a `Login` or `Logout`: `bool Success`, `string
IdentityRejectedNote`, `string Error`, built with `Ok()`, `Rejected(note)` and
`Failed(message)`.

A rejection is `Success == false` with a **non-null** `IdentityRejectedNote` and a
null `Error`: the device *is* registered, anonymously, and the note says why the
external id was refused. `Rejected` never leaves the note null, so a rejection
stays distinguishable from a transport failure. A failure is `Success == false`
with a non-null `Error` and no note.

### OpenPushVerification / OpenPushVerifyException

`OpenPushVerification` is one phone code as the server last described it: `string Id`
(`vf_…`), `string MaskedNumber` (`+1415•••0123`), `OpenPushVerifyChannel Channel` (`Auto`, `Sms`,
`WhatsApp`, `Call`), `OpenPushVerificationStatus Status` (`Pending`, `Approved`, `Expired`,
`MaxAttempts`), `int Attempts` and `DateTimeOffset? ExpiresAt`. It is immutable; check and
resend return a new value. Only `Pending` accepts another check or a resend.

`OpenPushVerifyException` is the typed error: `OpenPushVerifyErrorCode Code` (`RateLimited`,
`TooSoon`, `NotPending`, `Provider`, `Refused`, `InvalidNumber`, `Network`, `OptedOut`,
`NoCredit`, `NoRate`) and `string Message`, the server's own sentence when it sent one. `Network`
means no usable answer came back; a code this SDK does not know reads as `Refused`. `OptedOut`
(409) means the number replied STOP: never retry. `NoCredit` (402) means the workspace has no text
credit left. `NoRate` (409) means texts to the number's country are not priced or not allowed.

### PushSubscriptionState

`Id`, `Token`, `OptedIn`, `int? Status`. `OptedIn` is true when the device has a provider
token, the OS currently allows notifications, **and** `OptOut()` was not called. `Status`
is described under [`notification_types`](#notification_types-and-pushsubscriptionstatestatus).

### OpenPushUserState

`ExternalId`, `SubscriptionId`. Either may be null: there is no subscription id
before the first accepted registration, and an anonymous device has no external
id. `Logout` leaves the subscription id in place: the device stays registered,
without an account.

### OpenPushClickEvent / OpenPushForegroundEvent

- `OpenPushClickEvent`: `MessageId`, `Data`, `Notification`, `ActionId`, `Url`,
  `ActionUrl`, `LaunchUrlOpened`. `ActionId` is the id of the action button that was tapped,
  or null when the notification itself was opened. `Url` is the message's `deep_link`, or
  null; it does not change with the button. `ActionUrl` is the tapped button's own
  link, or null for a tap on the notification or on a button without one. `LaunchUrlOpened` is
  true when the SDK already opened a URL for this tap — `ActionUrl` when the button has one,
  `Url` otherwise (see `OpenPushSettings.OpenLaunchUrls`) — so a handler should not open it
  again. Route with `click.ActionUrl ?? click.Url` when `LaunchUrlOpened` is false.
- `OpenPushBackgroundMessage`: `MessageId`, `Data` (the delivered map, untouched),
  `Notification` (never null; `IsBackground` is true).
- `OpenPushForegroundEvent`: `MessageId`, `Data`, `Notification`,
  `bool DefaultPrevented { get; }`, `void PreventDefault()`, `void Display()`. `Data` is
  never null: an absent payload reads as an empty dictionary.

  Handlers run on the Unity main thread; when they return, the SDK answers the native side
  once: displayed unless a handler called `PreventDefault()`. Call `PreventDefault()` from
  the handler itself. `Display()` called from a handler overrides an earlier
  `PreventDefault()`; called later, it presents a held-back notification where the platform
  can: iOS, within the native 25-second window. On Android it is logged and ignored. For a
  payload forwarded through `ProcessForeground`, `DefaultPrevented` is a decision the host
  reads and carries out.

### OpenPushNotification

`OpenPushNotification.From(data)` names what the delivered data map carries, so a
host does not spell the wire keys itself. The click and foreground events fill it from the
native notification object as well, with the data map winning wherever it carries a value.
`Data` is the same dictionary instance that was passed in, never null. Strings are null
when the key is absent or empty.

| Member | Wire key |
| --- | --- |
| `string MessageId` | `op_message_id` |
| `string Title` | `title` |
| `string Body` | `body` |
| `string ImageUrl` | `image_url` |
| `string LaunchUrl` | `deep_link` |
| `string CollapseId` | `op_collapse_id` |
| `string Priority` | `op_priority` (`high` or `normal`) |
| `string ActionCategory` | `op_action_category` |
| `string AppId` | `op_app` |
| `int? TtlSeconds` | `op_ttl`, in seconds; null when absent or not a number |
| `bool SystemMessage` | `op_system_message` present and not `false`/`0` |
| `bool IsBackground` | `op_background` is `1` and there is no title and no body |
| `IReadOnlyList<OpenPushAction> Actions` | `op_actions` |
| `IReadOnlyDictionary<string, string> AndroidOverrides` | every `op_android_<name>` key, keyed by `<name>` |
| `IReadOnlyDictionary<string, string> Data` | the payload itself |

`OpenPushAction`: `Id`, `Label`, `Icon`, `bool SystemIcon`, and `Url`, the button's
own link or null. `op_actions` is a
JSON string holding at most three of these; an entry without both an id and a
label is dropped, and an unreadable array reads as no actions rather than
failing the notification. A HostOwned renderer reads the buttons it draws from `Actions`.

Title, body, action labels, and category IDs are final server-rendered values.
Do not interpret Liquid or choose an A/B arm again.

### OpenPushJson

Minimal JSON helpers for the SDK's small, fixed payloads. Not a general JSON library.

- `Escape(string value)`, `TryGetStringField(string json, string field)`,
  `TryGetIntField(string json, string field, out int value)`.
- `WriteStringMap(entries)`, `WriteValueMap(entries)`, `TryParseStringMap(json, out map)`
  and `TryParse(json, out value)`.
- `bool HasTruthyField(string json, string field)`: true when the field is present with a
  value that is neither `null` nor `false`.

## OpenPushSDK.DeviceCredentialStore

Platform-keystore credential storage.
`DeviceCredentialStore(string service, string iosAccessGroup = null)`:

- `bool Save(string value)`: returns `false` for a `null` value, and `false`
  if secure storage is unavailable.
- `string Load()`: returns the current value or `null`.
- `void Wipe()`: removes the stored credential.

Android encrypts with AES-GCM under a key generated in the Android Keystore and
keeps only ciphertext and IV in private `SharedPreferences`. iOS uses the
Keychain (generic password, the given service plus account `device-key`,
optional access group, accessible after first unlock on this device only, so a
backup restored onto another device does not carry it). `Save` updates the
existing item in place and adds one only when there is none, so a failed write
never loses the stored credential. `Load` moves an item an earlier build wrote
with another accessibility class to this one in place, keeping its value; if
that update fails the value is still returned and the move is retried on the
next load.

The Android backend ships inside `OpenPushUnity.androidlib`, whose consumer ProGuard rules
(`consumer-proguard.pro`) travel with the package, so an Android release build with Minify
enabled keeps it with no keep rules in the project.

In the Unity Editor the store is a volatile in-memory session store, so an
Editor-saved credential does not survive a domain reload or an Editor restart.
On every other platform (anything that is not Android, iOS, or the Editor)
the store fails closed: `Save` returns `false` and `Load` returns `null`.
Nothing is ever written as plaintext.

## OpenPushSDK.OpenPushSettings

A `ScriptableObject` (Assets → Create → OpenPush → Settings) kept in a
`Resources` folder. `OpenPush.Initialize(appId)` reads `SdkKey` from it; the two
`Inject*` flags and `AppGroupIdentifier` are read by the iOS post-export steps; the Android
fields are written into the adapter library's manifest and the EDM4U dependency file at
export. **Project Settings → OpenPush → Android delivery** edits the Android and migration
fields.

**New and existing assets.** A new asset (the Create menu or the Setup window's **Create**) gets the
defaults below and is stamped with a hidden settings version. An upgrade never changes what an existing
asset does: a field the asset file does not hold behaves as the asset did before the field existed. That
is why `InjectLiveActivityWidget` is on in an older asset that never stored it. `AndroidPushDelivery`
is `Automatic` in every asset that does not hold it, as it was on 1.1.5 to 1.2.1: a project that brings
its own FCM service and has no `AndroidPushDelivery` in its asset must set it to `Forward`
(`AndroidPushDelivery: 1`) before upgrading, or pushes arrive twice.
`OpenLaunchUrls` is on in every asset that does not turn it off, old or new. A value the asset holds
always wins; change it in the Inspector.

| Field | Default | Meaning |
| --- | --- | --- |
| `string SdkKey` | empty | The public SDK key. Required by `Initialize(appId)`. |
| `string AppId` | empty | Optional, for hosts that would rather not repeat the app id in code. |
| `string AppGroupIdentifier` | empty | Adds this App Group to the export, and names the container the notification service extension reads its shared context from. Left empty, an export adds no group unless `InjectServiceExtension` is on, which then supplies `group.<bundle id>.openpush`. |
| `bool InjectServiceExtension` | `true` | Inject the `OpenPushNotificationService` notification service extension (rich media, plus the `received` and `confirmed` receipts while the app is not running) into exported iOS projects, with the App Group entitlement on both targets. A host that owns a service extension of its own must turn it off. |
| `bool InjectLiveActivityWidget` | `false` (`true` in an older asset without the field) | Inject `OpenPushLiveActivityWidget` and `NSSupportsLiveActivities` into exported iOS projects. The asset's value decides; a project with no settings asset gets no widget (unless an earlier export already added OpenPush's widget target, which is kept up to date). The Setup window's **Live Activity widget** row turns it on and off. |
| `string ServiceExtensionBundleIdentifier` | empty | The service extension's bundle id. Empty: `<bundle id>.OpenPushNotificationService`. Must start with the app's bundle id and a dot. |
| `string ServiceExtensionProvisioningProfile` | empty | Profile name or UUID the service extension signs with; written as `PROVISIONING_PROFILE_SPECIFIER` with `CODE_SIGN_STYLE = Manual`. A batch build that signs manually fails without it. |
| `string LiveActivityWidgetBundleIdentifier` | empty | The widget's bundle id. Empty: `<bundle id>.LiveActivityWidget`. |
| `string LiveActivityWidgetProvisioningProfile` | empty | Profile the widget signs with, as above. |
| `OpenPushIosExtensionMode IosExtensionMode` | `Inject` | What an export does about the service extension: `Inject` OpenPush's own, `TakeOver` the one a previous push SDK made, `Share` (add OpenPush's lines to your own extension's code), or `Off`. |
| `string IosExtensionTarget` | empty | The existing extension `TakeOver` and `Share` apply to; empty means the one in the export. |
| `OpenPushPlumbingSource AppGroupSource` | `New` | `Reused` with `AppGroupIdentifier` empty uses the group the app and its existing extension share. |
| `OpenPushPlumbingSource IconsSource` | `New` | `Reused` keeps the project's own notification icons (see `AndroidNotificationIconName`). |
| `string AndroidNotificationIconName` | empty | With `IconsSource = Reused`, the drawable written as `ai.openpush.notification_icon` (Automatic delivery). |
| `OpenPushAndroidPushDelivery AndroidPushDelivery` | `Automatic` (also in an older asset without the field) | Automatic: the OpenPush FCM module fetches the token, renders notifications and handles taps. Forward (also accepted as HostOwned): your own FirebaseMessagingService (for example Firebase Unity Messaging) receives every push and calls `OpenPush.Register(token)` and `Notifications.ProcessForeground`/`ProcessClick`; the manifest gets `ai.openpush.fcm.delivery = forward`. |
| `string AndroidFirebaseProjectId` | empty | Firebase project id (Automatic mode; not needed when Firebase Unity already builds the app from `google-services.json`). |
| `string AndroidFirebaseApplicationId` | empty | Firebase Android application id, `1:<number>:android:<hex>` (Automatic mode; see above). |
| `string AndroidFirebaseApiKey` | empty | Firebase Web API key (Automatic mode; see above). Not a secret. |
| `bool OpenLaunchUrls` | `true` (also in an older asset without the field) | Open a tapped push's launch URL natively: on Android instead of starting the app, on iOS as the app comes up. Off writes `ai.openpush.suppress_launch_urls` (Android manifest) and `OpenPushSuppressLaunchURLs` (iOS Info.plist), and the URL is left to a `Clicked` handler. |
| `bool ReloginWithoutAuthHash` | `true` | When migrating state from an earlier OpenPush SDK build, log the migrated external id back in without an auth hash. Turn this off if your app uses identity verification: OpenPush then raises `OnIdentityVerificationRequired(externalId)` and leaves the device anonymous. |

`OpenPushAndroidPushDelivery` (namespace `OpenPushSDK.Core`) is `Automatic = 0` or
`Forward = 1`; `HostOwned` is kept as another name for `Forward`. `OpenPushIosExtensionMode` is
`Inject = 0`, `TakeOver = 1`, `Share = 2` or `Off = 3`; `OpenPushPlumbingSource` is `New = 0` or
`Reused = 1`.

## OpenPushSDK.Editor.OpenPushSetupCheck

Editor-only (assembly `OpenPush.Editor`). One row of the setup checklist shown at
**Window → OpenPush → Setup** and on the **Project Settings → OpenPush** page.
Both surfaces render the same rows through `OpenPushSetupRows.Draw`, and both
discover rows with `TypeCache`, so a host adds a project-specific check by
deriving from this class; there is nothing to register.

| Member | Default | Meaning |
| --- | --- | --- |
| `abstract string Title` | — | The row's short name. |
| `abstract string Detail` | — | Sentence describing the current state; re-read on every evaluation, so it may report values. |
| `abstract bool Passes()` | — | True when the project already satisfies the check. |
| `virtual bool CanFix` | `false` | True to draw this row's button. |
| `virtual string FixLabel` | `"Fix"` | The button's label. |
| `virtual void Fix()` | no-op | The button's action. Must be safe to run when the check passes. |
| `virtual int Order` | `100` | Ascending sort key; ties break on type name. |
| `virtual bool FixesItself` | `false` | True when `Fix()` can make the row pass with no one's input, so **Run all** may press it. |
| `virtual IReadOnlyList<OpenPushSetupItem> Items()` | `null` | Rows to show instead of this one, one per thing found, each with its own buttons (`OpenPushSetupItem`: `Title`, `Detail`, `Passed`, `Actions`, and `RunAll`, the action Run all takes). |
| `static IReadOnlyList<OpenPushSetupCheck> Discover()` | — | Every concrete subclass, instantiated and ordered. Cached until the next domain reload. |

The shipped checks, by order:

| Check | Order | What it does |
| --- | --- | --- |
| `ExistingPushPlumbing` | 1 | One row per thing a previous push SDK or your own code left (service extension, App Group, Android messaging service, notification icons), each with **Reuse** / **Create new** (or **Off**) and **Change** once answered. |
| `KnownPushSdk` | 2 | A known push SDK still installed: **Remove** lists every package and path and asks first; **Use compat** references the compatibility assembly from asmdefs whose scripts call it. |
| `UnityVersionSupported` | 5 | Fails below Unity 2022.3. |
| `SettingsAssetExists` | 10 | The settings asset exists. |
| `SettingsInResources` | 20 | It sits in a `Resources` folder. |
| `SdkKeyPresent` | 30 | It carries an SDK key; **Paste** reads the app id and key copied from the console (two values, `openpush://setup?app=…&sdk=…[&server=…]`, or JSON). |
| `AppIdPresent` | 40 | Reports only; the app id is optional when `Initialize(appId)` carries it. |
| `BootstrapInScene` | 45 | Reports what starts OpenPush; **Add Bootstrap** (opt-in, never pressed by Run all) adds the component to the first build scene. |
| `IosInjectionReviewed` | 50 | Reports only; summarises the iOS export switches. |
| `LiveActivityWidget` | 50 | Reports only; says whether iOS exports get the Live Activity widget. **Turn on** / **Turn off** flips `InjectLiveActivityWidget` (never pressed by Run all). |
| `IosExtensionIdentifiers` | 51 | Extension bundle ids sit under the app's, and each injected extension has a profile when the app signs manually. |
| `SampleImported` | 60 | Whether Feature Lab is imported. |
| `Edm4uInstalled` | 60 | EDM4U is installed; **Add EDM4U** adds the OpenUPM scoped registry and `com.google.external-dependency-manager` 1.2.189 to `Packages/manifest.json`. |
| `Edm4uResolved` | 61 | A resolve happened for this package version, path and delivery mode; **Resolve** rewrites the dependency file and forces one. |
| `AndroidSdkLevels` | 62 | Minimum API Level 24 and Target API Level 34 or Automatic; the button raises them. |
| `FirebaseConfigPresent` | 63 | Automatic delivery has the three Firebase fields, or `google-services.json` in `Assets` together with Firebase Unity's `FirebaseApp.androidlib` or a `google-services.xml` resource. **Fill** copies the three values from `google-services.json`, from the client whose package name is the Android application id. |
| `HostOwnedDeliveryReminder` | 64 | Reports only; names the calls Forward delivery needs. |

A check that throws is reported as a failing row rather than taking the checklist down.

**Run all** works through the rows in order, re-evaluating every row between two fixes: it
presses each fix that can fix itself, takes the stored answer or **Reuse** for existing-setup
questions, and stops at the first row only a person can fix, saying why.

## OpenPushSDK.Editor.OpenPushSetupCli

Batch entry points for `-batchmode -quit -executeMethod`:

- `OpenPushSetupCli.RunAll()`: Run all without prompts. The app id and SDK key come from the
  `OPENPUSH_SETUP` environment variable (any format **Paste** reads). Throws, so Unity exits with
  code 1, at the first row only a person can fix, naming it. Then writes the report.
- `OpenPushSetupCli.Report()`: writes `ProjectSettings/OpenPushSetup.json`: `found` (what the project
  and its last iOS export already have, schema `openpush-setup/1`) and `checks` (every row).

## In-app messages

`OpenPush.InAppMessages` exposes `Paused`, `AddTrigger`, `AddTriggers`,
`RemoveTrigger`, `RemoveTriggers`, `ClearTriggers`, `Fetch()`, `Lifecycle`,
`Clicked`, and the obsolete `ResetDisplayState()`.

| Member | Notes |
| --- | --- |
| `Paused` | pauses and resumes presentation in the native SDK (setting it requires `Initialize`) |
| `AddTrigger` / `AddTriggers` / `RemoveTrigger` / `RemoveTriggers` / `ClearTriggers` | device trigger values, handed to the native SDK, which re-evaluates its queue |
| `Fetch()` | asks the native SDK for a fresh eligibility check; it already fetches on its own and throttles itself |
| `Lifecycle` | `event Action<string, string>`: message id plus `willDisplay`, `didDisplay`, `willDismiss`, or `didDismiss` |
| `Clicked` | `event Action<string, OpenPushInAppClick>`: message id plus the action |
| `[Obsolete] ResetDisplayState()` | a no-op that logs; display state lives in the native SDK, which has no reset yet |

On Android and iOS the native in-app module fetches messages, evaluates triggers, persists
dismissals and redisplay state, presents the server-rendered document in a native web view
over the game, carries out prompt, tag and outcome actions, and posts impression, click and
page receipts. The Editor and desktop players fetch nothing; Feature Lab Demo mode renders a
bundled fixture document with the UI Toolkit view.

`OpenPushInAppClick`: `Kind`, `ClickId`, `BlockKey`, `ActionId`, `Url`, `UrlTarget`,
`Outcome`, `Prompt`, `Tags`, `bool Closes`. On both platforms `Kind` is `url`, `custom` or
`close` (a plain `click` when the action is none of these); prompt, tag and outcome actions
are handled inside the native module and do not surface as their own kinds. `ActionId` is
set only for `custom` clicks.

## Android

### Android push delivery

`OpenPushSettings.AndroidPushDelivery` picks who owns FCM:

- **Automatic** (default): the OpenPush FCM module fetches the token, renders notifications
  and reports taps; the host adds no code. The export writes `ai.openpush.app_id`,
  `ai.openpush.sdk_key` and, when set, `ai.openpush.fcm.project_id`,
  `ai.openpush.fcm.application_id` and `ai.openpush.fcm.api_key` meta-data into the adapter
  library's manifest. The meta-data is the path: fill the three Firebase fields.
  `google-services.json` in `Assets/` only works when Firebase Unity (or the google-services
  Gradle plugin) is also present, since a plain Unity export never turns it into a Firebase
  app. Without a configuration the FCM module skips Firebase initialization and only logs it. `firebase-messaging` 24.0.2 comes with the module.
- **HostOwned**: the FCM module is not resolved and the manifest removes
  `ai.openpush.android.fcm.OpenPushMessagingService` (`tools:node="remove"`), so the host's
  own `FirebaseMessagingService` (for example Firebase Unity Messaging) is the only one. The
  host calls `OpenPush.Register(token)` on every new token and forwards payloads:
  `ProcessForeground(data)` (returns true when a handler prevented display) and
  `ProcessClick(data)`; `ProcessDisplayed(data)` records a host-rendered display. To render
  Live Updates while the app is killed, the host's service calls
  `ai.openpush.android.liveupdates.OpenPushLiveUpdates.handle(context, data, collapseKey)`
  from the native live-updates module before its ordinary display path.

Both modes need External Dependency Manager for Unity (EDM4U) 1.2.189 from OpenUPM, which
resolves the vendored natives from the in-package Maven repository
(`Runtime/Plugins/Android/maven~`) plus their Maven Central and Google Maven dependencies,
and a player with Minimum API Level 24 (the SDK floor; Unity 6000.x enforces 25 as its own
minimum and raises it by itself) and Target API Level 34 or Automatic. The adapter
library `OpenPushUnity.androidlib` declares `POST_NOTIFICATIONS` and ships
`consumer-proguard.pro`, so Minify builds need no project keep rules. Android does not
deliver FCM after the user force-stops an app.

### Android Live Updates

`OpenPush.LiveUpdates.HandleAndroidPayload(data, collapseId)` passes a provider data map to
the native live-updates module (Android). It returns `OpenPushLiveUpdateResult`:
`NotLiveUpdate`, `Displayed`, `Ended`, `Dismissed`, `Invalid`, `Stale` (a repeated or
out-of-order frame that was dropped), or `Unsupported`. Call `IsHandled()` on the result; a
handled result must not fall through to the host's ordinary notification renderer. With
Automatic delivery the native FCM service renders Live Updates itself, including while the
app is killed.

`OpenPush.LiveUpdates.Capability` returns `FallbackOngoing`, `Eligible`, `PromotionDisabled`, or
`UnsupportedPlatform`. `OpenPush.LiveUpdates.OpenPromotionSettings()` opens the app-specific Android
16 setting only when the device resolves it. These calls return unsupported/false without pretending
to display anything on iOS, the Editor, and other platforms.

The payload key is `live_notification`. The built-in `progress` renderer accepts `start`, `update`,
and `end`; the legacy `dismiss` event spelling is an end alias. Use one stable `collapse_id` and
`instance_id` for a lifecycle and an increasing optional `sequence`. Android 16 uses platform
`ProgressStyle` and requests promoted ongoing treatment; earlier versions update one quiet ongoing
notification in place. A dismissal or Unpin is durable and later updates do not resurrect the
instance. A new `start` explicitly resets that lifecycle.

## iOS

### iOS Live Activities

`OpenPush.LiveActivities` exposes `SetupDefault(options)`,
`StartDefault(activityId, attributes, content)`, `Enter(activityId, token[, activityType])`,
`Exit(activityId)`, `SetPushToStartToken(activityType, token)`, and
`RemovePushToStartToken(activityType[, token])` (the token argument is accepted for
source compatibility and ignored). The calls go through the native `OpenPushLiveActivities`
module; completions run on the main thread. On Android they log a warning and report
`false`. Call setup on every launch after `Initialize`; ActivityKit token streams are
per-process. Activity IDs cannot contain `/`, and an activity type is the case-sensitive
Swift struct name used as APNs `attributes-type`. Push-to-start requires iOS 17.2; update
tokens require iOS 16.1.

The package can inject `OpenPushLiveActivityWidget` and `NSSupportsLiveActivities` after an
iOS export; the widget imports `DefaultLiveActivityAttributes` from the vendored
`OpenPushLiveActivities` framework and links it. `OpenPushSettings.InjectLiveActivityWidget`
decides; a project with no settings asset gets no widget. Turn it on with the **Live Activity
widget** row in **Window → OpenPush → Setup**, and leave it off when the project already owns a
widget. The
manual path is: add a Widget Extension with Live Activity support; set its floor to iOS 16.1;
link `OpenPushLiveActivities.xcframework` from `Runtime/Plugins/iOS/Native~` and
`import OpenPushLiveActivities`; render `DefaultLiveActivityAttributes`; and add
`NSSupportsLiveActivities = YES` to the main plist.

None of these APIs sends remotely: local start is ActivityKit, and remote start/update/end
stay in OP Console or the REST API. The compatibility facade keeps its `EnterAsync`/`ExitAsync`
wrappers.

### Launch URLs on iOS

The package opens a tapped push's URL itself, from the native tap, so a cold start does not
wait for Unity to load: the tapped button's own link when it has one, otherwise the push's
`deep_link`. Any URL that parses is opened, as on Android (a URL iOS has no handler for
is logged). `OpenPushSettings.OpenLaunchUrls`
turns it off (it writes `OpenPushSuppressLaunchURLs` to Info.plist). The native SDK's own opener
(`openLaunchURLs`, on by default in native iOS) is switched off at initialize, so a link is
never opened twice.

### VoIP (iOS)

Native iOS has an `OpenPushVoIP` product for PushKit VoIP pushes. The Unity package does
not vendor or wrap it: iOS requires every VoIP push to be reported to CallKit before the PushKit
handler returns, which has to happen in native code, not after a hop to the Unity main thread. An
app that needs calls adds `OpenPushVoIP` (and its CallKit handling) in a native plugin of its
own. Android has no VoIP push; a call there is a background (data-only) push.

## Vendored natives

| Platform | Location | Contents | Check |
| --- | --- | --- | --- |
| Android | `Runtime/Plugins/Android/maven~` | Maven repository with `ai.openpush:openpush-android`, `-in-app-messages`, `-live-updates` and `-fcm` 1.2.2, `SHA256SUMS` and `openpush-android.version.txt` | `scripts/verify-android-artifacts.sh` |
| iOS | `Runtime/Plugins/iOS/Native~` | static `OpenPush`, `OpenPushCore`, `OpenPushLiveActivities` and `OpenPushNSE` XCFrameworks (device arm64, simulator arm64/x86_64), `SHA256SUMS` and `openpush-ios.version.txt` | `scripts/verify-ios-artifacts.sh` |

The `~` suffix hides both folders from Unity's importer. EDM4U resolves the Android
repository; the iOS export copies the XCFrameworks into the Xcode project and links them
into UnityFramework (static, never embedded), and `OpenPushCore` plus `OpenPushNSE` into the
injected service extension. The Swift interfaces need Xcode 26 with Swift 6.3.3 or newer.

## Not included

> `sendEvent` and Inbox are not in this release: each is blocked on a server capability that does not exist yet, and a stub that returns success would turn a server gap into a silent client-side lie. They ship when their endpoints do.
