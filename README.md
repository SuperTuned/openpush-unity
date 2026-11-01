# openpush-unity

Receive-only Unity package for registering a device with OpenPush.ai and
posting notification delivery receipts. It intentionally contains no message
creation, campaign management, or send API.

> `1.2.2` is the first public release. It is a thin layer over the native OpenPush iOS and
> Android 1.2.2 SDKs, which ship inside the package and own registration, identity, tags,
> receipts, permission, notification processing, in-app messages, Live Activities and Live
> Updates. Android fetches its FCM token automatically (no Firebase Unity SDK needed); iOS
> captures the APNs token Unity receives. This SDK has no send surface: sending stays in
> OP Console.
>
> Projects moving from another push SDK can use the optional compatibility layer; see
> [MIGRATING-FROM-ONESIGNAL.md](MIGRATING-FROM-ONESIGNAL.md).

## Requirements

- Unity 2022.3 LTS or newer
- Android: Minimum API Level 24 (Unity 6000.x enforces 25 as its own minimum and raises it
  by itself), Target API Level 34 or Automatic, IL2CPP + ARM64 recommended
- iOS: deployment target 16.0, built with Xcode 26 or newer (Swift 6.3.3 or newer is
  needed to read the vendored frameworks' Swift interfaces)
- External Dependency Manager for Unity (EDM4U) 1.2.189 from OpenUPM, for Android
- an OpenPush.ai App ID and public SDK key
- Android, Automatic delivery: the Firebase project id, application id and API key in
  `OpenPushSettings` (written as manifest meta-data). A `google-services.json` in `Assets/`
  only works when Firebase Unity (or the google-services Gradle plugin) is also present

The native SDKs ship inside the package: an in-package Maven repository for Android
(`Runtime/Plugins/Android/maven~`, resolved by EDM4U) and static XCFrameworks for iOS
(`Runtime/Plugins/iOS/Native~`, linked at export). No OpenPush artifact is fetched
from a public registry; EDM4U fetches only their third-party dependencies
(`firebase-messaging` 24.0.2, Kotlin and AndroidX) from Maven Central and Google Maven.

The Editor and desktop players run an in-memory stub: calls are logged, nothing touches
the network, and `OpenPush.SdkVersion` reads `stub`.

iOS hosts can call `OpenPush.LiveActivities.SetupDefault()` after initialization
on every launch, then `StartDefault`, `Enter`, `Exit`, and the push-to-start token
methods. Remote start/update/end still happen in OP Console or the REST API.

## Installation

Install Git 2.14 or newer and make sure the `git` executable is available on
your system `PATH`. Unity Package Manager uses the system Git client when it
resolves Git dependencies.

No GitHub account or credential is needed: Unity fetches the package over plain HTTPS.

### Install from Package Manager

Open `Window → Package Manager`, select `+ → Add package from git URL`, and enter:

```text
https://github.com/SuperTuned/openpush-unity.git#1.2.2
```

The `#1.2.2` suffix pins the release tag. For a production project, keep the package pinned
to a release tag or a full commit SHA instead of following a moving branch.

### Install from the project manifest

Add the package to the `dependencies` object in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "ai.openpush.unity": "https://github.com/SuperTuned/openpush-unity.git#1.2.2"
  }
}
```

Unity records the resolved revision in `Packages/packages-lock.json`. Commit
both package files so every developer and CI build resolves the same package.

### Import the optional sample

After installation, select `OpenPush` in Package Manager, open its `Samples`
section, and choose `Import` beside **Feature Lab**. The sample is copied into
the project's `Assets/Samples` folder and is not compiled until it is imported.
When a loadable `OpenPushSettings` asset contains both the App ID and SDK key,
Feature Lab starts in Live mode and its permission button opens the platform
prompt. Without that configuration it starts in Demo mode, where the button is
labelled **Simulate notification permission** and does not call the OS.
The sample forwards no tokens: the native SDKs obtain them.

If Unity reports that Git cannot be found, restart Unity after installing Git
or adding it to `PATH`.

## One-click setup

The Setup window (**Window → OpenPush → Setup**, also **Project Settings → OpenPush**) opens by
itself the first time the package is imported. Copy the app id and SDK key from the OpenPush
console, then press **Run all**. It works down the checklist, re-checking every row after each
fix:

- creates the settings asset in `Assets/Resources`, or moves it there;
- **Paste** fills the app id and SDK key from the clipboard while the key is still empty. It accepts
  an `openpush://setup?app=…&sdk=…[&server=…]` link, JSON (`{"app_id": …, "sdk_key": …}`), or the
  two raw values as the console shows them. Raw values are taken only when they are an app id and
  its own `op_sdk_…` key, so a stray clipboard is never pasted. A server other than the default is logged, because it is passed
  in code: `OpenPush.Initialize(appId, sdkKey, serverUrl)`;
- adds EDM4U, resolves the Android dependencies and raises the Android API levels;
- **Fill** copies the three Firebase values from `Assets/**/google-services.json`, from the client
  whose package name is your Android application id (a file with no such client is left alone);
- answers every [existing push setup](#existing-push-setup-reuse-or-create-new) question with the
  answer stored earlier, or **Reuse**.

It stops at the first row it cannot fix by itself and says why, for example an empty clipboard,
or a previous push SDK that is still installed (removing it needs your confirmation).

Nothing starts OpenPush until your code calls `OpenPush.Initialize`, or a scene has the
**OpenPush Bootstrap** component. The **Startup** row's **Add Bootstrap** button puts one in your
first build scene. Run all never presses it, because a game that initialises after consent must not
get one.

Batch and CI runs use the same steps without prompts:

```sh
OPENPUSH_SETUP='openpush://setup?app=<app id>&sdk=<sdk key>' \
  Unity -batchmode -quit -projectPath . -executeMethod OpenPushSDK.Editor.OpenPushSetupCli.RunAll
Unity -batchmode -quit -projectPath . -executeMethod OpenPushSDK.Editor.OpenPushSetupCli.Report
```

`RunAll` exits with code 1 at the first row only a person can fix. When it has just added EDM4U, it
stops and asks for a second run, because Unity loads the new package on its next start. In the editor,
Run all waits for the package instead. `Report` writes
`ProjectSettings/OpenPushSetup.json`. Its `found` list holds what the project and its last iOS
export already have, in the same `openpush-setup/1` format the iOS and Android setup tools use.
Its `checks` list holds every row. Commit the file: it also stores your Reuse / Create new answers
under `choices`.

## Existing push setup: Reuse or Create new

A project that ran another push SDK, or has push code of its own, keeps what it set up. The first
rows of the checklist list what was found, one row each, and ask once:

| Found | Reuse (default) | Create new |
| --- | --- | --- |
| A notification service extension made by your previous push SDK | **TakeOver**: at export its code becomes OpenPush's handler (the original is backed up to `.openpush-backup/` in the export). Its bundle id, App Group, entitlements and provisioning profile are kept. | Not possible beside it: iOS runs one service extension per app. **Off** leaves it alone and loses images, buttons and delivery receipts for OpenPush pushes. |
| A notification service extension with your own code | **Share**: OpenPush's lines are added first in its `didReceive` and `serviceExtensionTimeWillExpire`, and everything else is kept byte for byte (see below). | **Off**, as above. |
| An App Group on the app (and its extension) | OpenPush shares it; nothing new to provision. | `group.<bundle id>.openpush`, added beside the existing groups (never instead of them); the App IDs need it before a manual-signing build. |
| Another Android `FirebaseMessagingService` (yours, or Firebase Unity Messaging) | **Forward** delivery: it keeps receiving every push and forwards to OpenPush. | **Automatic**: OpenPush's own service receives pushes; remove the other one, or pushes are split between the two. |
| Notification icons (`ic_stat_*`) | They stay the status-bar icon of OpenPush notifications. | OpenPush's own names (`ic_stat_openpush_default`). |

The answers are fields of `OpenPushSettings` (`IosExtensionMode`, `IosExtensionTarget`,
`AppGroupSource`, `IconsSource`, `AndroidPushDelivery`, `AndroidNotificationIconName`), so builds
never prompt. **Change** on an answered row asks again. Reuse also fills the
[identifier fields](#ios-extension-identifiers-and-signing) from the extension it reuses. Then a later
clean export, after the previous SDK is removed, signs OpenPush's own extension with the same App ID
and profile.

At export the real Xcode project always wins. The iOS step reads the exported project and
records what it found in `ProjectSettings/OpenPushSetup.json`. When the export does not match the
stored answer (no answer yet, or a different extension), an interactive build asks once and stores
the answer. A batch build never asks: when nothing was ever answered and the export already has its
own service extension, it adds none and goes on, as earlier OpenPush builds did, with a warning in the
build log; any other mismatch fails the batch build, naming both. A build that would end up with two
service extensions stops.

**Share mode** inserts whole lines and changes nothing else:

```swift
import OpenPushNSE

// in the class body
private let openPush = OpenPushNotificationServiceHandler(options: {
    var options = OpenPushNotificationServiceHandler.Options()
    options.appGroup = "group.com.example.game.shared"
    return options
}())

// first line of didReceive(_:withContentHandler:)
if openPush.handleIfOpenPush(request, withContentHandler: contentHandler) { return }

// first line of serviceExtensionTimeWillExpire(), or a whole override that then calls super
if openPush.serviceExtensionTimeWillExpireIfHandling() { return }
```

OpenPush handles pushes that carry its message id and hands every other push to your code
unchanged. It edits Swift only. When it cannot place its lines safely, it stops the build and
writes them to `.openpush/share.patch` in the export for you to add by hand. That happens with an
Objective-C class, with `#if` blocks in the class body, or with a method it cannot find.

**Removing your previous push SDK.** While its package or files are still in the project, the
**Previous push SDK** row offers **Remove**. It lists every package, scoped-registry scope, folder
and file it will delete, and only deletes after you confirm. Files under `Assets` go to the trash.
`Packages/manifest.json` is edited only when it parses as JSON. The SDK's notification icons follow
your icons answer: Reuse keeps them, Create new removes them. Scripts that call the old SDK are
listed, never edited. **Use compat** references the compatibility assembly from their asmdefs, so
changing the using line is the only edit left; the package's migration guide lists what carries over.
After a TakeOver, the build log prints any Podfile lines that still add the old SDK's pods to the
reused extension.

## Android setup

1. **Install EDM4U.** Open **Window → OpenPush → Setup** and press **Add EDM4U** on the
   **External Dependency Manager (Android)** row: it adds the OpenUPM scoped registry and
   `com.google.external-dependency-manager` `1.2.189` to `Packages/manifest.json`. EDM4U
   creates and patches the Gradle templates itself; there is nothing to tick. OpenPush
   generates `Assets/Plugins/OpenPush/Editor/OpenPushDependencies.xml` for EDM4U; commit it
   with your project. It is also written, and resolved, before every Android build, so CI
   builds need no manual resolve step.
2. **Resolve.** The row **Android dependencies resolved** runs a forced resolve. When the
   Editor loads with Android as the build target, OpenPush also forces one after a package
   upgrade or a delivery-mode change, because an upgraded package lives in a new
   `Library/PackageCache` folder that the patched Gradle settings must point at.
3. **Pick a delivery mode** (`OpenPushSettings.AndroidPushDelivery`) in **Project Settings →
   OpenPush → Android delivery**:
   - **Automatic** (default, including in a settings asset that never stored this
     field; a project with its own FCM service and no stored value must set **Forward** before upgrading): the OpenPush FCM module fetches the token, renders
     notifications and reports taps. Your app adds no code. Fill the three Firebase fields:
     the meta-data is the path. `google-services.json` in `Assets/` only works when Firebase
     Unity (or the google-services Gradle plugin) is also present, because a plain Unity export
     never turns it into a Firebase app. `firebase-messaging` 24.0.2 comes with the module.
     Without a configuration the device never gets an FCM token and the SDK only logs it; the
     setup row **Firebase configuration** checks for this.
   - **Forward** (also accepted under its older name, **HostOwned**): your own
     `FirebaseMessagingService` receives every push, for example with Firebase Unity Messaging.
     OpenPush's messaging service is removed from the merged manifest, the FCM module
     is not resolved, and the manifest carries `ai.openpush.fcm.delivery = forward`, so the native
     module disables its own service should it ever be merged in. Call `OpenPush.Register(token)` whenever Firebase gives you a token,
     `OpenPush.Notifications.ProcessForeground(data)` for foreground messages,
     `OpenPush.Notifications.ProcessClick(data)` for taps, and
     `OpenPush.Notifications.ProcessDisplayed(data)` after your service posts a
     notification.
4. **API levels:** Minimum 24 (the SDK floor; Unity 6000.x raises it to 25 by itself),
   Target 34 or Automatic (Unity 2022.3 compiles against the
   target level; androidx.startup 1.2.0 and androidx.browser 1.8.0 need 34). The setup
   row **Android API levels** fixes them.

The adapter library ships its own R8 keep rules, so Minify builds need no
project ProGuard entries.

## iOS setup

Nothing to forward. The package observes Unity's APNs token notifications and hands the
token to the native SDK (a token that arrives before `Initialize` is held and applied
after it), and it captures notification taps and foreground arrivals through the
notification delegate Unity installs. The iOS export copies the vendored XCFrameworks into
the Xcode project, links them into UnityFramework (static, not embedded) and raises the
deployment target to 16.0 when it is lower (a project on a newer iOS keeps its own). See [iOS export capabilities](#ios-export-capabilities) and the
[notification service extension](#ios-notification-service-extension) for the rest.

The SDK asks iOS for a push token only once notification permission is granted
(fully or provisionally). Until then there is no token and so no subscription:
no in-app messages, no Live Activity tokens, and no custom events attributed to
this install (they wait on the device unless the user is logged in). Show your own
screen explaining what notifications are for before you call
`OpenPush.Notifications.RequestPermission`, so the system prompt appears when the
user is ready to accept it.

Static frameworks are never copied into the app, so their privacy manifests would not reach
it either. The export puts each native module's `PrivacyInfo.xcprivacy`, unchanged, into a
resource bundle (`OpenPushPrivacy_<module>.bundle`) in Copy Bundle Resources of the target
that links it: the app, the notification service extension and the Live Activity widget.
Xcode's privacy report for an archive then lists OpenPush. The export only adds these
bundles; a manifest the app or another SDK already has is left alone.

## App setup

To connect a player to OpenPush you need an OpenPush App ID and public SDK key.

There is no token step. Android (Automatic delivery) fetches the FCM token itself, and iOS
forwards the APNs token that Unity's app controller receives. Keep calling `Register(token)`
only in Android Forward mode. Initialize once during startup; the credential closure must
return the currently linked installation credential, or `null` before linking:

```csharp
using OpenPushSDK;
using OpenPushSDK.Core;

var credentialStore = new DeviceCredentialStore("your.app.device");

OpenPush.Initialize(AppID, credential: () => credentialStore.Load());
```

Put your public SDK key in an `OpenPushSettings` asset: **Assets → Create →
OpenPush → Settings**, saved inside a `Resources` folder. `Initialize` then needs
only the app id. To keep the key in code instead, call
`Initialize(appId, sdkKey)`; an explicit key always wins over the asset. With no
key in either place, `Initialize(appId)` refuses through `OnLog` and
`OnRegistrationFailed` rather than leaving the SDK half-configured. A null or blank
key passed to `Initialize(appId, sdkKey)` throws.

**Window → OpenPush → Setup** (also **Project Settings → OpenPush**) checks existing push
setup, a previous push SDK, the Unity version, the settings asset, its Resources location, keys,
startup, EDM4U and the Android resolve, Android API levels, the Firebase configuration, the Android
delivery mode, the iOS export switches and extension identifiers, and the sample. **Run all** fixes
what it can; see [One-click setup](#one-click-setup).

`OpenPush.OnLog` is the one diagnostic sink, and `OpenPush.LogLevel` decides how
much of it arrives: `Verbose` by default, down to `Error` for a shipping build
or `None` for silence. Native SDK log lines arrive through the same sink. To
listen from several places, subscribe to the `OpenPush.Logged` event (and
`Registered` / `RegistrationFailed` beside `OnRegistered` /
`OnRegistrationFailed`); the assignable properties still work and run first. On
Android they are also copied to logcat in a debuggable build only; a release
build writes none.

To start the SDK without writing any code, use **Add Component → OpenPush →
Bootstrap** on any object in your first scene and set **App Id** (or leave it
blank to use `OpenPushSettings.AppId`). The component also offers **Request
Permission On Start**, **Log To Console** and **Log Level**. Projects that
already call `OpenPush.Initialize` themselves do not need it.

`OpenPush.DefaultServerUrl` is `https://app.openpush.ai` and is used when
`serverUrl` is omitted or blank; trailing slashes are trimmed.

**Call `Initialize` first.** Every call with a side effect (`Register`, `Login`,
`TrackEvent`, `User.*` writes, `PushSubscription.OptIn`/`OptOut`,
`Notifications.RequestPermission`, in-app and live-feature calls) throws
`InvalidOperationException` until `Initialize` has run. `LogLevel`, `OnLog`,
`OnRegistered`, `OnRegistrationFailed`, event subscriptions and read-only getters work at
any time, so a host may subscribe during `Awake` and initialize later. Each event keeps up
to ten values raised before its first subscriber and replays them to it, except
`Notifications.ForegroundWillDisplay`.

`OpenPush.SdkVersion` is the native SDK version and `OpenPush.WrapperVersion` the version
of this package.

## VoIP pushes

VoIP pushes (native iOS `OpenPushVoIP`) have no Unity API: iOS requires each one to be reported to
CallKit from native code, so add `OpenPushVoIP` in a native plugin of your own if the game needs
calls.

## In-app messages

On Android and iOS the native SDK fetches in-app messages, evaluates triggers, persists
dismissals and redisplay state, presents messages in a native web view over the game, and
posts impression, click and page receipts. Prompt, tag and outcome actions inside a
message are carried out by the native in-app module. In the Editor and desktop players
nothing is fetched; Feature Lab Demo mode renders a bundled fixture message instead.

```csharp
OpenPush.InAppMessages.AddTrigger("level", 12);
OpenPush.InAppMessages.AddTriggers(new Dictionary<string, object> {
    ["plan"] = "pro", ["onboarded"] = true
});
OpenPush.InAppMessages.Paused = false;
OpenPush.InAppMessages.Fetch();            // asks the native SDK for a fresh check; it throttles itself
OpenPush.InAppMessages.Lifecycle += (messageId, phase) =>
    Debug.Log($"{messageId} {phase}");     // willDisplay/didDisplay/willDismiss/didDismiss
OpenPush.InAppMessages.Clicked += (messageId, action) =>
    Debug.Log($"{messageId} {action.Kind} {action.ActionId}");
```

`Clicked` reports `Kind` `url`, `custom` or `close` (a plain `click` when the action is
none of these). `ActionId` is set only for `custom` clicks. Prompt, tag and outcome
actions do not reach C# as their own kinds.

`OpenPush.InAppMessages.ResetDisplayState()` is an obsolete no-op: display
state lives in the native SDK, which has no reset yet.

## Notifications and receipts

With Android Automatic delivery and on iOS, the native SDK renders notifications (with
their action buttons), posts the `received`, `confirmed` and `clicked` receipts, and
raises the click and foreground events. The host forwards nothing.

```csharp
OpenPush.Notifications.Clicked += click => { /* click.Notification, click.ActionId, click.Url, click.ActionUrl */ };
OpenPush.Notifications.ForegroundWillDisplay += n => { /* n.PreventDefault() */ };
OpenPush.Notifications.BackgroundMessageReceived += m => { /* Android: m.Data, never displayed */ };
```

Both events carry `Notification`, an `OpenPushNotification` naming what the
payload holds (title, body, image, launch URL, TTL, priority, action buttons),
while `Data` stays the raw map. A click also reports `ActionId`, the action
button that was tapped, `Url`, the message's deep link, and `ActionUrl`, the tapped button's
own link when it has one. A tap that launched the app is replayed to the first
`Clicked` subscriber.

```csharp
OpenPush.Notifications.Clicked += click =>
{
    if (!click.LaunchUrlOpened) Route(click.ActionUrl ?? click.Url);
};
```

**Background pushes (Android).** A push sent as a background push for Android carries
`op_background` = `"1"` and no title or body. It is never displayed; its data arrives through
`BackgroundMessageReceived` on the main thread, after the `received` receipt is posted. While the
game is paused in the background the handler runs when it resumes, and a push that starts a killed
app (before `Initialize`) never reaches C#. For work that must run at that moment, declare a native
Kotlin or Java `ai.openpush.android.OpenPushBackgroundMessageHandler` in the game's manifest
(`<meta-data android:name="ai.openpush.background_message_handler" android:value="com.example.SyncOnPush" />`);
the vendored Android SDK instantiates it for every background push, before `Initialize` has run. iOS background pushes (`content-available`) are not raised here.

**Payload features with no API.** A push with a badge increment raises the iOS badge in
the injected service extension, counting up from the badge count stored in the App Group
(`SetBadgeCount` and `ClearAll` update that count). A push with Android replace on replaces the
notification already shown under the same collapse key instead of stacking (Automatic delivery).

`ForegroundWillDisplay` is answered once all handlers return: the notification is
displayed unless a handler called `PreventDefault()`. With no handler it is displayed at
once and is not replayed later. Android waits up to one second for the handlers' answer
and then displays. On iOS, `Display()` after `PreventDefault()` presents the notification
within the native 25-second window; on Android it is logged and ignored.

Android Forward hosts forward from their own messaging service:

```csharp
OpenPush.Register(token);                                          // every new FCM token
bool suppressed = OpenPush.Notifications.ProcessForeground(data);  // posts "received"
OpenPush.Notifications.ProcessDisplayed(data);                     // after your notify() call
OpenPush.Notifications.ProcessClick(data);                         // taps your service renders
```

`ProcessForeground` runs the `ForegroundWillDisplay` handlers on the calling thread, posts
the `received` receipt, and returns `true` when a handler called `PreventDefault()`; the
host then shows its own UI or nothing. `ProcessClick` posts `clicked` and raises `Clicked`
once. `ProcessDisplayed` posts `confirmed`. Read the action buttons to render from
`OpenPushNotification.From(data).Actions`; `ActionCategory` is the matching iOS category.
OpenPush has already resolved Liquid and selected the A/B arm before this data reaches
Unity, so display the values as received.

`OpenPush.Receipt(type, messageId)` and `OpenPush.MessageId(data)` remain for custom paths.
`MessageId` reads `op_message_id` from an `IReadOnlyDictionary<string, string>`; stringify a
numeric message id before building that dictionary.

`Notifications.ClearAll()` removes this app's delivered notifications, and
`Notifications.Remove(messageId)` removes those of one OpenPush message (on Android, the
ones the OpenPush renderer posted). `Notifications.SetBadgeCount(count)` sets the iOS app
icon badge (and the stored count badge increments add to) and is a no-op elsewhere.

## Notification permission

```csharp
OpenPush.Notifications.RequestPermission(fallbackToSettings: true, done: granted =>
    Debug.Log(granted ? "notifications allowed" : "notifications denied"));

bool granted = await OpenPush.Notifications.RequestPermissionAsync(true);

bool permitted = OpenPush.Notifications.Permission; // cached, never blocking
int types = OpenPush.Notifications.NotificationTypes;

OpenPush.Notifications.PermissionChanged += allowed =>
    Debug.Log($"permission changed: {allowed}");
```

Ask at a moment the player understands. On Android 13+ the request goes through Unity's
runtime-permission API and the native SDK is told the result; with `fallbackToSettings`, a
request after a permanent denial opens the app's notification settings. Below Android 13
there is no runtime prompt and the call reports the current permission. The native SDK
refreshes the cached permission and re-registers when it changes.
`RequestProvisionalPermission` asks for iOS provisional authorization and reports `false`,
changing nothing, elsewhere.

The adapter library's manifest declares `android.permission.POST_NOTIFICATIONS`, so the
Android 13+ prompt needs nothing in the host manifest.

A host that owns its pause lifecycle can call `OpenPush.Notifications.OnAppForegrounded()`;
the native SDKs observe the app lifecycle themselves.

## Identity

```csharp
OpenPush.Login(externalId, authHash, result =>
{
    if (result.Success) { /* linked */ }
    else if (result.IdentityRejectedNote != null) { /* registered anonymously */ }
    else { /* result.Error */ }
});

OpenPush.Logout();
```

Compute the identity-verification auth hash **on your server**, never in the
client. When the server refuses an external id it still registers the device
anonymously and says why: that arrives as `Success == false` with a non-null
`IdentityRejectedNote`, and it is logged through `OnLog`. The `done` callback runs on the
Unity main thread. `OpenPush.User.Changed` fires when the external id or the subscription
id changes.

`OnIdentityVerificationRequired(externalId)` is raised when the app requires identity
verification and the SDK holds an external id without an auth hash (for example one
migrated from an earlier OpenPush SDK build). Answer it with `Login(externalId, authHash)`.

## Journey events

```csharp
OpenPush.TrackEvent("onboarding_started",
    new Dictionary<string, object> { ["source"] = "welcome" });
```

The native SDK validates, persists and batches custom events and attributes each to the
current external id or subscription.

## Tags

```csharp
OpenPush.User.AddTag("tier", "gold");
OpenPush.User.AddTags(new Dictionary<string, string> { ["tier"] = "gold", ["level"] = "12" });
OpenPush.User.RemoveTag("trial");
OpenPush.User.RemoveTags("trial", "legacy");
var tags = OpenPush.User.GetTags();
```

Tags are device-level state kept by the native SDK. An empty key is ignored with a log
line. When identity verification is enabled for the app, a tag write needs a valid auth
hash like `external_id`; a rejected write is logged.

## Aliases

```csharp
OpenPush.User.AddAlias("crm_id", "c-42");
OpenPush.User.AddAliases(new Dictionary<string, string> { ["crm_id"] = "c-42", ["loyalty_id"] = "l-7" });
OpenPush.User.RemoveAlias("crm_id");
OpenPush.User.RemoveAliases("crm_id", "loyalty_id");
```

An alias is a second label the server can address this user by, alongside
`external_id`. The server caps aliases at 10 per user and owns that rule; a refused
label (over the cap, or the reserved `external_id`) is logged. Alias writes pass the same
identity gate as tags.

## Phone numbers and codes

A user can carry phone numbers, for SMS and WhatsApp messages, and the phone codes that
confirm them. Both go through the native SDK with the app's SDK key and server, like every other
request.

```csharp
OpenPush.User.AddSms("+14155550123");       // or AddWhatsApp
OpenPush.User.RemoveSms("+14155550123");    // or RemoveWhatsApp: opts the number out
```

Numbers are normalised exactly as the server does it: every character that is not a digit or
`+` is removed, so `+1 (415) 555-0123` is `+14155550123`, and a leading `00` is read as `+`. What
is left must be E.164: `+`, the country code, and 8 to 15 digits in all, the first of which is not
`0`. A number without its country code (`415 555 0123`) or a second `+` is refused: the call
returns `false`, logs why through `OnLog` (without the number) and sends nothing. An accepted
write returns
`true`; the native SDK stores it for the user logged in at the time of the call and retries it
like a tag write.

**A new number is added not verified, and receives nothing until it is confirmed** with a phone
code. An approved code marks the number's SMS and WhatsApp subscriptions subscribed on the server:

```csharp
try
{
    var verification = await OpenPush.Verify.StartAsync("+14155550123");   // OpenPushVerifyChannel.Auto
    // … the player types the code …
    verification = await OpenPush.Verify.CheckAsync(verification, typedCode);
    switch (verification.Status)
    {
        case OpenPushVerificationStatus.Approved: break;      // subscribed
        case OpenPushVerificationStatus.Pending: break;       // wrong code; try again
        default: break;                                       // Expired or MaxAttempts: start a new one
    }
}
catch (OpenPushVerifyException e)
{
    // e.Code: RateLimited, TooSoon, NotPending, Provider, Refused, InvalidNumber, Network,
    // OptedOut (the number replied STOP; never retry), NoCredit (no text credit left) or
    // NoRate (the number's country is not priced or allowed); e.Message says why.
}
```

`StartAsync(phone, channel, locale)` sends the code by `Auto`, `Sms`, `WhatsApp` or `Call`, in
the language `locale` names (`"en"`, `"pt-BR"`; null leaves it to the server). The number is
normalised the same way, and one that is not E.164 after that faults with `InvalidNumber` before
anything is sent. `ResendAsync(verification, channel)`
sends it again, on the channel the code last went out on when `channel` is null; the server
refuses a resend within 30 seconds of the last send with `TooSoon`. A wrong code is not an error:
`CheckAsync` returns the verification still `Pending` with `Attempts` counted up.

`OpenPushVerification` carries the server's `Id`, the masked number (`MaskedNumber`), `Channel`,
`Status`, `Attempts` and `ExpiresAt`. The full number never comes back to C#: the native SDK keeps
each verification in memory for `CheckAsync` and `ResendAsync`, and nothing stores it, so a
verification does not survive the app process. The calls are not retried, since the player is
waiting; the exception is the game's to show. Every task completes on the Unity main thread.

In the Editor and desktop players nothing is sent: the stub answers at once, sends codes by SMS
when asked for `Auto`, and accepts `123456`. Any other code counts an attempt, and the fifth wrong
one ends the verification as `MaxAttempts`.

On iOS give the code field the `oneTimeCode` text content type in a native view and iOS offers a
code it received by SMS above the keyboard. Android's SMS Retriever hash
(`OpenPush.Verify.smsRetrieverHashFor(context)` natively) is not wrapped in C#. WhatsApp and voice
codes are typed by hand.

## Email addresses

A user can carry email addresses. They go through the native SDK with the app's SDK key and
server, like every other request.

```csharp
OpenPush.User.AddEmail("player@example.com");
OpenPush.User.RemoveEmail("player@example.com");   // removes the subscription
```

The address is trimmed and lower-cased, and must then be at most 254 characters with one `@`,
something before it and a dot in the domain. Anything else is refused: the call returns `false`,
logs why through `OnLog` (without the address) and sends nothing. An accepted write returns `true`;
the native SDK stores it for the user logged in at the time of the call and retries it like a tag
write.

`AddEmail` subscribes the address at once, with no confirmation step, unless this app's suppression
list has it (the address unsubscribed, bounced or complained): then it is stored but never
emailed. **`RemoveEmail` is not an unsubscribe.** It removes the subscription and writes no
suppression, so a later `AddEmail` subscribes the address again. In the Editor and desktop players
nothing is sent.

## Subscription

```csharp
var subscription = OpenPush.User.PushSubscription; // live Id, Token, OptedIn
var state = subscription.Snapshot();                // immutable value

OpenPush.PushSubscription.OptOut();   // status -2: the server stops sending to this device
OpenPush.PushSubscription.OptIn();
bool receiving = OpenPush.PushSubscription.OptedIn;

OpenPush.PushSubscription.Changed += state =>
    Debug.Log($"subscription changed: {state.OptedIn}");
OpenPush.PushSubscription.ChangedWithPrevious += (previous, current) =>
    Debug.Log($"{previous?.OptedIn} -> {current.OptedIn}");
```

`OptOut()` does not delete the subscription or the provider token: it stops sends,
survives process restart, and is reversible with `OptIn()`. `OptedIn` is true when the OS
permission is granted, a provider token is present, and `OptOut()` was not called.

`PushSubscriptionState.Status` is the native wire status: `1` subscribed, `-2` opted out,
`0` no permission, `-18` never prompted, `-19` prompt unanswered. On iOS the permitted
value is the authorization bitmask (any positive value is subscribed). Android reports
only the not-answered values (`-18`/`-19`) and `null` otherwise; the Editor reports `null`.

## Batched writes and retry

The native SDKs persist tags, aliases, phone numbers, email addresses, custom events and receipts and retry them
on their own. Phone codes are the exception: each `OpenPush.Verify` call is one request. `OpenPush.Flush()` is an obsolete no-op and only logs.

## Threading

Every event and callback runs on the Unity main thread. Native callbacks are posted
through Unity's main-thread `SynchronizationContext`; nothing blocks a native thread on
Unity. The exception is `ProcessForeground`, whose handlers run on the calling thread, so
call it from the main thread. `Notifications.Permission`, `PushSubscription.OptedIn` and
`User.PushSubscription` are plain reads of state the native SDK caches.

## Android Live Updates

Android builds include the native live-updates module. With Automatic delivery
the native FCM service renders Live Updates, including while the app is killed; nothing to
forward. To hand a payload over from C# (for example in Forward mode while Unity runs),
forward the provider's data map and collapse ID before the host's ordinary display path:

```csharp
var result = OpenPush.LiveUpdates.HandleAndroidPayload(data, collapseId);
if (result.IsHandled()) return;
```

The `progress` renderer handles `start`, `update`, and `end` (`dismiss` is accepted as an
end alias), reuses one stable notification, drops repeated or out-of-order frames
(`Stale`), remembers Unpin/dismissal, and expires old state. Android 16 requests promoted
ongoing treatment and uses `ProgressStyle`; earlier versions show a quiet ongoing
fallback.

```json
{"collapse_id":"delivery-42","ttl":120,"data":{"live_notification":{"key":"progress","instance_id":"delivery-42","event":"update","sequence":2,"event_attributes":{"title":"Delivery"},"event_updates":{"current_progress":70,"max_progress":100,"short_text":"70%"}}}}
```

`OpenPush.LiveUpdates.Capability` distinguishes fallback, eligible, user-disabled, and
unsupported states. `OpenPromotionSettings()` opens Android's app-specific setting when
available.

In Forward mode your `FirebaseMessagingService` calls
`ai.openpush.android.liveupdates.OpenPushLiveUpdates.handle(context, data, collapseKey)`
from the native live-updates module to render while the app is killed. A force-stopped
app cannot receive FCM until the user launches it again.

`OpenPush.IsInitialized`, `OpenPush.SdkVersion`, `OpenPush.WrapperVersion` and
`OpenPush.User.ExternalId` expose live SDK state.

Migrating hosts that use the compatibility facade: see the compatibility migration guide
in the package root.

The optional `Samples~/FeatureLab` sample can be imported from Package Manager.
It contains the complete adaptive Demo/Live app, sanitized host wiring, and
explicit platform boundaries without compiling sample code into every install.
It forwards no tokens itself.

## Credential storage and sign-out

`DeviceCredentialStore` uses Android Keystore (AES-GCM) on Android and the
Keychain on iOS. In the Unity Editor it is an in-memory, session-only store for
play-mode testing: there is no hardware keystore, so a credential saved in the
Editor does not survive a domain reload or an Editor restart. On unsupported
platforms, and whenever secure storage is unavailable, saving fails closed:
`Save` returns `false` and `Load` returns `null`, and credentials are never
written as plaintext.

### Android release builds with Minify enabled

No action needed: `OpenPushUnity.androidlib/consumer-proguard.pro` keeps everything the
C# layer calls by name, and the natives ship keep rules for their public API.

The host app owns account linking and unlinking. Retire the server-side
delivery address before clearing local state. If unlink cannot complete,
securely queue that credential and block another account from linking until
the queue drains.

### iOS export capabilities

Exporting an iOS project adds the Push Notifications capability and the
remote-notification background mode to the Xcode project, so a fresh export
registers for push with no trip to the Signing & Capabilities tab. The
`aps-environment` entitlement follows Unity's **Development Build** checkbox:
`development` when it is ticked, `production` when it is not.

An App Group is added when you fill in `OpenPushSettings.AppGroupIdentifier` or
when the notification service extension is enabled. The extension is enabled by
default in newly created settings assets and supplies
`group.<your bundle id>.openpush` when no group is configured. Add that group to
the App ID before building with manual signing.

### iOS extension identifiers and signing

By default the extensions get `<bundle id>.OpenPushNotificationService` and
`<bundle id>.LiveActivityWidget`, and nothing about signing is written. A project that signs
manually, or already has App IDs and profiles for its extensions, sets them in
**Project Settings → OpenPush → iOS extension identifiers and signing**:

| `OpenPushSettings` field | Blank | Filled |
| --- | --- | --- |
| `AppGroupIdentifier` | `group.<bundle id>.openpush` (service extension only) | used on the app and the extension |
| `ServiceExtensionBundleIdentifier` | `<bundle id>.OpenPushNotificationService` | the extension's `PRODUCT_BUNDLE_IDENTIFIER`; must start with `<bundle id>.` |
| `ServiceExtensionProvisioningProfile` | nothing written | `PROVISIONING_PROFILE_SPECIFIER`, with `CODE_SIGN_STYLE = Manual` |
| `LiveActivityWidgetBundleIdentifier`, `LiveActivityWidgetProvisioningProfile` | `<bundle id>.LiveActivityWidget`, nothing | the same for the widget |

For example, a game `com.example.game` whose previous push SDK used the extension App ID
`com.example.game.LegacyNotificationServiceExtension` and the group `group.com.example.game.legacy`,
signed manually:

```text
AppGroupIdentifier                   group.com.example.game.legacy
ServiceExtensionBundleIdentifier     com.example.game.LegacyNotificationServiceExtension
ServiceExtensionProvisioningProfile  Example Game NSE AppStore
```

A clean export then signs with the App IDs and profiles you already have, and needs no post-build
script.

When Player Settings' automatic signing is off and the app has a profile, each extension OpenPush
creates gets, per build configuration, the app target's `CODE_SIGN_STYLE` (Manual when it has none)
and `CODE_SIGN_IDENTITY`. A filled profile field always signs that extension manually. Signing values
already on a target, from an append export or another build step, are never overwritten, and the
build log lists what was kept. A batch build that signs manually without an extension profile fails
before anything is written, naming the field to fill; an interactive build warns.

### iOS notification service extension

`OpenPushSettings.InjectServiceExtension` is **on by default** in newly created
settings assets, providing rich images and receipts from pushes nobody taps. It
adds an App Group capability your App ID has to carry. iOS runs one service extension per app, so
when the export already has one, `OpenPushSettings.IosExtensionMode` decides what happens to it
(TakeOver, Share or Off; see [Existing push setup](#existing-push-setup-reuse-or-create-new)).

With it on, the export gains an `OpenPushNotificationService` target. The extension is
the native `OpenPushNotificationServiceHandler` in three lines, linked against the
vendored `OpenPushCore` and `OpenPushNSE` frameworks. iOS launches it for every OpenPush
alert, including while the app is not running, and it attaches rich media (`image_url`)
and posts the `received` and `confirmed` receipts, so a push that is never tapped is still
counted as delivered and displayed.
Every export gives the injected extensions the app's display name (`CFBundleDisplayName`),
version and build number, which App Store validation requires.

Set up the App Group before the first build with it on: add
`group.<your bundle id>.openpush` (or whatever you put in
`OpenPushSettings.AppGroupIdentifier`) to the App ID in your developer account,
or manual signing will fail with a profile that does not carry it.

The app mirrors its context into the App Group under the native `openpush.shared.*` keys
on `initialize`. **No secret
crosses that boundary**: the receipt route authenticates on the public SDK key, so the
device credential stays in the app's keychain, and the extension posts its receipts
without one.

## Security boundary

- The SDK key is public and may ship in the app.
- Device credentials and provider tokens must not enter analytics or
  diagnostic logs (use `OpenPush.ShortToken` for display).
- The SDK exposes no send or console-management surface.
- Workspace switching must retire the prior delivery address rather than
  transferring its history.

Report vulnerabilities using [SECURITY.md](SECURITY.md).

## Verification

The C# layer is tested without a Unity license:

```sh
dotnet test "Tests~/OpenPush.Bridge.Tests.csproj"
dotnet test "Tests~/OpenPush.Compat.Tests.csproj"
dotnet test "Tests~/OpenPush.Editor.Tests.csproj"
```

The vendored natives and the exported builds are checked with:

```sh
scripts/verify-android-artifacts.sh
scripts/verify-ios-artifacts.sh
scripts/build-android-export.sh   # needs UNITY2022; OPENPUSH_INSTALL=tarball, OPENPUSH_MINIFY=1
scripts/build-ios-export.sh       # needs UNITY2022 and Xcode; OPENPUSH_IOS_SDK=device
```

The Feature Lab also includes Unity EditMode tests beside the sample source
(`scripts/test-feature-lab.sh`).

See [API.md](API.md) for the complete public surface.

## License and brand

The source is licensed under the [Apache License 2.0](LICENSE). Attributions
and trademark notices are in [NOTICE](NOTICE). The license does not grant
permission to use the OpenPush name, marks, or branding; forks and derivatives
must use their own name and branding.

Contributions are covered by [CONTRIBUTING.md](CONTRIBUTING.md).

## UTM link tracking

Link tracking is configured on the OpenPush server. After personalization, it adds
missing `utm_source`, `utm_medium`, and `utm_campaign` parameters to HTTP(S)
`deep_link` URLs. Existing values (including blank values), query parameters and
fragments are preserved; custom app schemes are unchanged.

The SDK already delivers the complete URL through `click.Url` / `click.Notification.LaunchUrl`.
Use that URL as received when routing a click: do not rebuild it from its host/path,
decode and re-encode its query, or append another set of UTM tags. By default the SDK
opens a tapped push's launch URL itself (Android without starting the game, iOS as the
game comes up) — or the tapped button's own link, when it has one — and the click arrives
with `click.LaunchUrlOpened` set, so a handler should not open it again. Turn off `OpenPushSettings.OpenLaunchUrls` to route every URL
yourself. This is on in every settings asset that does not turn it off, including one made before
the setting existed, so a game that routes its own taps sets `OpenLaunchUrls` off. The SDK does not send UTM fields as separate custom events. HTTPS is recommended for
web destinations; the store samples intentionally reject plain HTTP.

The Feature Lab custom notification demonstrates a tagged web link with a
fragment. Regression tests cover encoded query values, duplicate/blank parameters
and click forwarding.
