# Migrating from OneSignal Unity 5.x

> OneSignal is a trademark of OneSignal, Inc. OpenPush is not affiliated with,
> endorsed by, or sponsored by OneSignal, Inc. The OneSignal name appears only
> to describe the optional compatibility layer for apps migrating from the
> OneSignal SDK.

`OpenPush.OneSignalCompat` is an **opt-in name alias assembly**. It contains no
independent behavior, state, or provider implementation — supported members
delegate to `OpenPushSDK.OpenPush`. Its purpose is that a
OneSignal v5 host can change one `using` line and keep compiling.

The assembly lives at `Runtime/Compat/` and declares `noEngineReferences: true` in
its asmdef, which the Unity compiler enforces: the assembly cannot touch
`UnityEngine` at all. No CI job checks that boundary outside Unity, so the
enforcement is the editor's, not a test's.

> **Do not ship this assembly alongside the real OneSignal Unity SDK.** Both
> declare a type called `OneSignal`. Remove the OneSignal package first; the two
> are mutually exclusive by design, not by accident.

---

## 0. Project setup: keep OneSignal's App IDs, then remove it

Before touching code, open **Window → OpenPush → Setup**. With OneSignal still installed, the first
rows show what its build step sets up and ask **Reuse** or **Create new** once:

- **iOS notification service extension `OneSignalNotificationServiceExtension`.** Reuse is
  **TakeOver**: the export rewrites its `NotificationService` class as OpenPush's handler (the
  original goes to `.openpush-backup/` in the export) and keeps its bundle id
  (`<bundle id>.OneSignalNotificationServiceExtension`), entitlements and provisioning profile.
  Reuse also copies that bundle id, and the profile the last export used, into
  `ServiceExtensionBundleIdentifier` and `ServiceExtensionProvisioningProfile`. After OneSignal is
  removed, a clean export then gives OpenPush's own extension the same App ID and profile, so no
  App ID or profile has to be created.
- **App Group `group.<bundle id>.onesignal`.** Reuse shares it, so nothing new goes on the App ID.
- **Android icons `ic_stat_onesignal_default` / `ic_onesignal_large_icon_default`.** Reuse keeps
  `OneSignalConfig.androidlib`; the Android renderer uses those names after OpenPush's own.

Then press **Remove OneSignal** on the **Previous push SDK** row. The dialog lists every path
before anything is deleted. It removes:

- the three `com.onesignal.unity.*` dependencies from `Packages/manifest.json`, and the
  `com.onesignal` scope (dropping a registry it leaves empty), but only when the file parses as
  JSON;
- `Assets/OneSignal/`;
- `ProjectSettings/OneSignalSettings.json` and `ProjectSettings/GoogleDependencyOneSignal.xml`;
- every `OneSignal*Dependencies.xml`;
- `OneSignalConfig.androidlib`, but only when you chose Create new for the icons.

Files under `Assets` go to the trash. Scripts that use `OneSignalSDK` are listed and not edited.
**Use compat** adds `OpenPush.OneSignalCompat` to the asmdefs that compile them, and section 1
below is the one edit left. If a TakeOver export still has a Podfile block that adds
`OneSignalXCFramework` to the extension, the build log prints those lines so you can delete them.

The setup fields, with placeholder ids, for a project that signs manually:

```text
AppGroupIdentifier                   group.com.example.game.onesignal
ServiceExtensionBundleIdentifier     com.example.game.OneSignalNotificationServiceExtension
ServiceExtensionProvisioningProfile  Example Game NSE AppStore
```

## 1. The change at the call site

One `using` line, one `Initialize` decision, and a delivery-mode choice on
Android (forwarding is needed only in HostOwned mode) — plus a fourth edit if you
subscribed to any forwarded event, which you almost certainly did. See
[§1.1](#11-the-fourth-edit-forwarded-events-change-shape).

```diff
-using OneSignalSDK;
+using OpenPushSDK.Compat;
```

That is the whole edit at this call site. The `Initialize` line below is context,
not a change — it is character-for-character what you already wrote:

```csharp
void Start()
{
    OneSignal.Initialize("replace-with-app-id");
}
```

`Initialize(appId)` is unchanged, because OpenPush reads the public SDK key from
an `OpenPushSettings` asset rather than from the call. Create it once:
**Assets → Create → OpenPush → Settings**, saved inside a `Resources` folder.
To keep the key in code instead, call the four-argument form — an explicit key
always wins over the asset:

```csharp
OneSignal.Initialize("replace-with-app-id", "replace-with-public-sdk-key");
```

With no key in either place `Initialize` refuses through `OpenPush.OnLog` and
`OpenPush.OnRegistrationFailed` rather than leaving the SDK half-configured.

### Unity's provider handoff

OpenPush owns provider integration natively, like the SDK you are migrating from:

- **Android, Automatic delivery (default):** the native FCM module fetches the token, renders
  notifications and reports taps. Remove your token forwarding. Fill the Firebase fields in
  `OpenPushSettings` (the meta-data is the path; `google-services.json` only works when
  Firebase Unity or the google-services Gradle plugin is also present), and install External Dependency Manager for
  Unity (EDM4U) 1.2.189 from **Window → OpenPush → Setup**.
- **Android, HostOwned delivery:** keep your own FCM service (for example Firebase Unity
  Messaging), set `OpenPushSettings.AndroidPushDelivery = HostOwned`, and forward:

```diff
+OneSignal.Register(providerToken);                       // every new FCM token
+bool suppressed = OneSignal.Notifications.ProcessForeground(data);   // foreground messages
+OneSignal.Notifications.ProcessClick(data);               // taps your service renders
```

- **iOS:** nothing to forward. The package observes Unity's APNs callbacks and captures taps
  and foreground arrivals itself.

`Clicked`, `ForegroundWillDisplay` and their receipts are automatic for OpenPush payloads
in Automatic delivery and on iOS. In HostOwned mode, `ProcessDisplayed(data)` after your
service posts a notification records the display.

Android Live Updates render natively with Automatic delivery, including while the app is
killed. In HostOwned mode your FCM service calls the native
`OpenPushLiveUpdates.handle` entry point, and `OpenPush.LiveUpdates.HandleAndroidPayload(data,
collapseId)` handles payloads you forward while Unity runs. See
[README.md](README.md#android-live-updates).

### 1.1 The fourth edit: forwarded events change shape

This is Unity's counterpart of the rewrite the iOS guide also calls the fourth
edit, and it is the one thing in this migration that a `using` change cannot
absorb. OneSignal Unity 5.x declares its observable hooks as
**`EventHandler<…EventArgs>`**; OpenPush declares them as delegates over its
own payload types. So both the handler shape (`(sender, e)` → `(e)`) and the
payload type name change, and a v5 handler is a **type error**, not a named
refusal.

| OneSignal Unity 5.x | OpenPush |
|---|---|
| `event EventHandler<NotificationClickEventArgs> Notifications.Clicked` | `event Action<OpenPushClickEvent> Notifications.Clicked` |
| `event EventHandler<NotificationWillDisplayEventArgs> Notifications.ForegroundWillDisplay` | `event Action<OpenPushForegroundEvent> Notifications.ForegroundWillDisplay` |
| `event EventHandler<NotificationPermissionChangedEventArgs> Notifications.PermissionChanged` | `event Action<bool> Notifications.PermissionChanged` |
| `event EventHandler<PushSubscriptionChangedEventArgs> User.PushSubscription.Changed` | `event Action<PushSubscriptionState> User.PushSubscription.Changed` |
| `event EventHandler<UserStateChangedEventArgs> User.Changed` | `event Action<OpenPushUserState> User.Changed` |
| `event EventHandler<InAppMessageClickEventArgs> InAppMessages.Clicked` | `event Action<string, OpenPushInAppClick> InAppMessages.Clicked` |

The mechanical fix is to drop the `sender` parameter and let the payload type be
inferred, which keeps the edit to the handler's own signature:

```diff
-OneSignal.Notifications.Clicked += (sender, e) =>
-    Debug.Log(e.Notification.NotificationId);
+OneSignal.Notifications.Clicked += e =>
+    Debug.Log(e.MessageId);
```

Registering a named method changes that method's declaration instead:

```diff
-private void OnClicked(object sender, NotificationClickEventArgs e) { … }
+private void OnClicked(OpenPushClickEvent e) { … }
```

The payload members differ too, because these are OpenPush's own types and not
aliases: an `OpenPushClickEvent` carries `MessageId` and `ActionId` rather than
v5's nested `Notification` and `Result`, and `OpenPushForegroundEvent` carries
`Data` plus `PreventDefault()` rather than a nested `IDisplayableNotification`.

`User.Changed` reports `ExternalId` and `SubscriptionId` through
`OpenPushUserState`. Subscription-only changes remain available through
`User.PushSubscription.Changed`.

Nothing here is silent. Every one of these is a compile error you get on the
first build, and the automatic click bridge / `ProcessForeground` still post
their `clicked` and `received` receipts with no subscriber attached — so a
migrated project reports delivery correctly before you touch a single handler.

### 1.2 Behaviors that carry over without code

- **Launch URLs open on tap.** As before, a tapped push with a launch URL opens it: on Android
  straight from the notification, without starting the game; on iOS as the game comes up,
  before Unity has loaded. The click still reaches `Notifications.Clicked`, with
  `LaunchUrlOpened = true`, so remove any `Application.OpenURL(click.Url)` you added while
  migrating. To route URLs yourself, turn off `OpenPushSettings.OpenLaunchUrls` (it replaces
  `com.onesignal.suppressLaunchURLs` / `OneSignal_suppress_launch_urls`).
- **Notification icons.** Keep `Assets/Plugins/Android/OneSignalConfig.androidlib`. When a push
  names no icon, Android uses `ic_stat_onesignal_default` for the status-bar icon and
  `ic_onesignal_large_icon_default` for the large icon (after the OpenPush names
  `ic_stat_openpush_default` and `ic_openpush_large_icon_default`, if you bundle those). The large
  icon field also takes a drawable name instead of an HTTPS URL. The Setup window's **Android
  notification icons** row confirms which icons it found.
- **Permission asked by your own code.** If the game asks for notification permission itself
  (Unity's `Permission.RequestUserPermission`, another plugin's `requestAuthorization`), OpenPush
  notices the answer when the dialog closes: Android updates the subscription on the next
  resume, and iOS asks for the APNs token. `OpenPush.Notifications.RequestPermission()` is no
  longer required for a grant to register.
- **iOS entitlements.** Push and background-mode entitlements are merged into the entitlements
  file the Unity-iPhone target already signs with (or `Unity-iPhone/Unity-iPhone.entitlements`),
  never a second file, so a custom post-processor that adds its own entitlements keeps working.

---

## 2. Forwarded symbols

Everything in this table compiles unchanged under `using OpenPushSDK.Compat;`
**except the six rows marked "shape changes"** — those keep v5's member name but
not its delegate type, and are the fourth edit of [§1.1](#11-the-fourth-edit-forwarded-events-change-shape).

| OneSignal Unity 5.x | OpenPush | Notes |
|---|---|---|
| `OneSignal.Initialize(appId)` | same name | the public SDK key comes from the `OpenPushSettings` asset |
| `Login` / `Logout` | same names | optional `OpenPushIdentityResult` callback |
| `User.AddTag/AddTags/RemoveTag/RemoveTags/GetTags` | same names | local session map works in the editor too |
| `User.AddAlias/AddAliases/RemoveAlias/RemoveAliases` | same names | same shape |
| `User.ExternalId` | same name | same shape |
| `User.AddEmail` / `RemoveEmail` | same names | Forwarded to `OpenPush.User.AddEmail` / `RemoveEmail`. The address is trimmed and lower-cased and must have one `@`, something before it, a dot in the domain and at most 254 characters, or the call is refused with a log line and nothing is sent. AddEmail subscribes the address at once, unless this app's suppression list has it. RemoveEmail removes the subscription; it is not an unsubscribe |
| `User.AddSms` / `RemoveSms` | same names | Forwarded to `OpenPush.User.AddSms` / `RemoveSms`. The number is normalised as the server does it (separators removed, a leading `00` read as `+`) and must then be E.164 (`+` and 8–15 digits, the first not `0`), or the call is refused with a log line and nothing is sent. A new number is added **not verified** and receives nothing until a code sent with `OpenPush.Verify.StartAsync` is approved. `RemoveSms` opts the number out. WhatsApp numbers (`OpenPush.User.AddWhatsApp`) have no v5 counterpart, so the facade does not carry them |
| `User.Changed` | same name, **shape changes** | `Action<OpenPushUserState>`, not `EventHandler<UserStateChangedEventArgs>` — see §1.1 |
| `User.TrackEvent` | same name | forwards to the persisted OpenPush Custom Events pipeline |
| `User.PushSubscription.Id/Token/OptedIn/OptIn/OptOut` | same shape | `.Snapshot()` returns an immutable value; `OptIn()` requests permission when needed |
| `User.PushSubscription.Changed` | same name, **shape changes** | `Action<PushSubscriptionState>`, not `EventHandler<PushSubscriptionChangedEventArgs>` — see §1.1. A C# event over immutable snapshots, sharing one observer list with `OpenPush.PushSubscription.Changed` |
| `Notifications.Permission` / `CanRequestPermission` / `RequestPermissionAsync` | same names | unchanged; a callback `RequestPermission` form is also available |
| `Notifications.ClearAllNotifications` | same name | forwards to `OpenPush.Notifications.ClearAll()` |
| `Notifications.PermissionChanged` | same name, **shape changes** | `Action<bool>`, not `EventHandler<NotificationPermissionChangedEventArgs>` — see §1.1 |
| `Notifications.Clicked` | same name, **shape changes** | `Action<OpenPushClickEvent>`, not `EventHandler<NotificationClickEventArgs>` — see §1.1. Android/iOS taps are automatic; `ProcessClick` is for Android HostOwned delivery (ignored on iOS) |
| `Notifications.ForegroundWillDisplay` | same name, **shape changes** | `Action<OpenPushForegroundEvent>`, not `EventHandler<NotificationWillDisplayEventArgs>` — see §1.1. Automatic on Android (Automatic delivery) and iOS; Android HostOwned hosts forward through `ProcessForeground` |
| `Notifications.NotificationTypes` | same name | any positive value means the OS permits notifications |
| `InAppMessages.Paused` and trigger methods | same names | string trigger dictionaries are copied into OpenPush's object-valued trigger map |
| `InAppMessages.Clicked` | same name, **shape changes** | `Action<string, OpenPushInAppClick>` carries the message id and action data |
| Android Live Notifications | `OpenPush.LiveUpdates.HandleAndroidPayload` | not aliased: OpenPush-only surface |
| `LiveActivities.EnterAsync` / `ExitAsync` / `SetupDefault` / `StartDefault` / `SetPushToStartToken` / `RemovePushToStartToken` | same names and signatures | OpenPush uses a static class; `ExitAsync` keeps OneSignal's obsolete warning but works |
| `Session.AddOutcome/AddUniqueOutcome/AddOutcomeWithValue` | same names | forwarded to `OpenPush.TrackEvent` as Journey Custom Events |
| — | `Register`, `Flush`, `CurrentToken`, `IsInitialized`, `SdkVersion`, `Version`, `DefaultServerUrl` | OpenPush additions, aliased for convenience. `Flush` is an obsolete no-op; `SdkVersion` is the native SDK version and `Version` the package version |

Three members copy their container rather than forwarding it, and they are the only
non-delegating lines in the assembly:

- `User.AddTags(IDictionary)` / `AddAliases(IDictionary)` — v5 takes a mutable
  `IDictionary`, OpenPush takes `IReadOnlyDictionary`. The copy exists so a
  caller's dictionary cannot mutate under the SDK after the call.
- `User.GetTags()` returns a mutable `Dictionary`. It is a copy, so
  writing to it does not reach the session.
- `InAppMessages.AddTriggers(Dictionary<string, string>)` copies string values
  into OpenPush's object-valued trigger map.

---

## 3. Refused symbols

Nothing OpenPush does not implement is a silent no-op that returns success while
doing nothing. The symbols enumerated in the table below are **declared and
refused at compile time** with the replacement named at the call site, so one
compile reports that whole set. Nothing in this list is stubbed.

The refusals are declared on the real classes — they are `partial` continuations
of `OneSignal`, `OneSignal.User` and `OneSignal.Notifications`, plus nested
`Debug`, `InAppMessages` and `Location` classes. So you do not have to
look anything up: leave the OneSignal call where it is, compile, and the error
lands on that line quoting what to do instead.

```
error CS0619: 'OneSignal.User.Language' is obsolete:
  'OpenPush reads the device language and sends it with every registration. To override it, send a language tag with OpenPush.User.AddTag.'
```

All three platforms behave this way — the error lands on the untouched v5 call
site and quotes the replacement. iOS uses `@available(*, unavailable, message:)`;
Android uses `@Deprecated(level = DeprecationLevel.ERROR)`, and its `OneSignal.Debug`,
`InAppMessages` and `Location` paths resolve through extension
properties in the compat module so that the v5 spelling reaches the message there
too — Kotlin extensions resolve only in scope, which is why that guide imports the
whole `ai.openpush.android.onesignal` package rather than the class. On Unity and Android each member also throws `NotSupportedException`, so a
reflective or warning-suppressed caller still cannot get a silent success.

| Refused symbol | What to do instead |
|---|---|
| `OneSignal.ConsentRequired` | OpenPush has no consent gate. Gate your own call to `OneSignal.Initialize(...)` instead — until you call it, nothing is collected and no request is made. |
| `OneSignal.ConsentGiven` | OpenPush has no consent gate. Call `OneSignal.Initialize(...)` once the user has consented. |
| `OneSignal.Debug.LogLevel` | OpenPush has one diagnostic sink, gated by one level: set `OpenPush.OnLog` to receive lines and `OpenPush.LogLevel` to choose how many (`None`, `Error`, `Warning`, `Info`, `Verbose`). |
| `OneSignal.Debug.AlertLevel` | OpenPush does not display diagnostic alerts. Use `OpenPush.OnLog` for diagnostics. |
| `OneSignal.Location.IsShared` | OpenPush never collects or shares location. Store location-derived targeting as a tag or alias when needed. |
| `OneSignal.Location.RequestPermission` | OpenPush never collects location. Send a tag or alias you compute yourself if you need geo targeting. |
| `OneSignal.User.OneSignalId` | OpenPush has no OneSignal-assigned user id. Use `OneSignal.User.ExternalId`, or `OneSignal.User.PushSubscription.Id` for this installation. |
| `OneSignal.User.Language` | OpenPush reads the device language and sends it with every registration. To override it, send a tag: `OneSignal.User.AddTag("language", "fr")`. |
| `OneSignal.Notifications.PermissionNative` | Use `OpenPush.Notifications.Permission` for the cached grant and `NotificationTypes` for the detailed device value. |
| `OneSignal.Notifications.RemoveNotification` | Call `OpenPush.Notifications.Remove(messageId)`. The legacy method takes an Android notification id the host owns, while OpenPush matches the message's `op_message_id`. |
| `OneSignal.InAppMessages.WillDisplay` / `DidDisplay` / `WillDismiss` / `DidDismiss` | Use `OpenPush.InAppMessages.Lifecycle` and branch on the phase string. |

`Initialize(string appId)` is deliberately **not** in this table. It works. Neither are
`User.AddSms`, `User.RemoveSms`, `User.AddEmail` and `User.RemoveEmail`: they are forwarded to OpenPush's phone
numbers and email addresses (§2).

There is no send surface on any of these paths: sending stays in OP Console.

## Verification

The compat assembly is compiled and exercised without a Unity license:

```sh
dotnet test "Tests~/OpenPush.Compat.Tests.csproj"
```

Because the assembly is nothing but forwarding, compiling it against the real
`Runtime/Facade` sources is most of the test; the rest asserts that each alias
lands on the same platform call an `OpenPush.*` call would, over a recorded fake
platform, never on a log line.

---

## Further reading

The event-shape rewrite is documented as the fourth edit in
[§1.1](#11-the-fourth-edit-forwarded-events-change-shape). The adaptive Feature
Lab under `Samples~/FeatureLab` is the maintained integration reference.
