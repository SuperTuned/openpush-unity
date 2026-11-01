# Changelog

All notable changes to `openpush-unity` will be documented in this file.

The project uses [Semantic Versioning](https://semver.org/).

## 1.2.2

First public release.

- A thin Unity layer over the native OpenPush iOS and Android 1.2.2 SDKs, which
  ship inside the package: static XCFrameworks for iOS and an in-package Maven
  repository for Android, resolved by External Dependency Manager for Unity.
- Push registration: the FCM token is fetched automatically on Android and the
  APNs token Unity receives is captured on iOS. Notification permission,
  opt-in and opt-out, and push subscription state with change events.
- Notification click, foreground and Android background-message events, action
  buttons with their own links, launch URLs opened natively (on by default),
  and delivery receipts posted by the native SDKs.
- Identity and targeting: external ID login with optional identity
  verification, tags, aliases, and custom events for journeys.
- SMS and WhatsApp phone numbers with phone-code verification, and email
  addresses.
- In-app messages, iOS Live Activities and Android Live Updates.
- iOS export post-processing: notification service extension and Live Activity
  widget injection, App Group and push entitlements, and explicit extension
  bundle ids and provisioning profiles for manually signed apps.
- A Setup window (**Window → OpenPush → Setup**) whose **Run all** finds push
  setup the project already has and asks once whether to reuse it or create
  new.
- An opt-in compatibility layer for projects migrating from another push SDK,
  with a migration guide.
- The Feature Lab sample.

Upgrading from an earlier OpenPush Unity build needs no code or settings
change: every public member keeps its signature, the device credential and
subscription carry over, and existing settings assets keep their behaviour.

- A tapped push's launch URL opens natively by default (Android without
  starting the game, iOS as the game comes up), and the click reaches
  `Clicked` with `LaunchUrlOpened` set. This holds for every settings asset
  that does not turn it off, including one made before `OpenLaunchUrls`
  existed: an asset from 1.1.6 to 1.2.1 behaves as before, and an asset from
  1.1.5 or earlier now opens launch URLs too. To keep routing every URL from
  your own `Clicked` handler, set `OpenLaunchUrls` off in the
  `OpenPushSettings` asset (Inspector, or **Project Settings → OpenPush**).
- Other settings added in later builds never change an existing
  `OpenPushSettings` asset: a field the asset does not hold behaves as before
  the field existed. An asset without `AndroidPushDelivery` reads as `Automatic`,
  as on 1.1.5 to 1.2.1 (the OpenPush FCM service receives pushes). A project that
  brings its own FCM service and has no `AndroidPushDelivery` in its settings asset
  must set it to Forward (`AndroidPushDelivery: 1`) before upgrading, or pushes
  arrive twice. A value the asset holds always wins. New assets
  get `Automatic` delivery and the Live Activity widget off.
- An iOS export of a project with no `OpenPushSettings` asset no longer gets
  the Live Activity widget extension (an export that already has OpenPush's
  widget target keeps it). An existing asset keeps its
  `InjectLiveActivityWidget` value. The Setup window's **Live Activity widget**
  row turns it on.
- A batch iOS build of a project that already has its own notification
  service extension, and never answered the Setup question about it, adds no
  OpenPush extension and goes on (with a warning), as earlier builds did. Only
  an interactive build asks.
- Injected iOS extensions carry the app's `CFBundleShortVersionString` and
  `CFBundleVersion` from the moment they are created, in their Info.plist and
  as the target's `MARKETING_VERSION` and `CURRENT_PROJECT_VERSION`, instead of
  a template's 1.0 / 1.
- iOS exports carry the native privacy manifests: each module's
  `PrivacyInfo.xcprivacy` goes into a resource bundle in the app, the
  notification service extension and the Live Activity widget. The manifests
  now also declare email addresses, phone numbers, product interaction and the
  App Group defaults reason.
- `DeviceCredentialStore` on iOS stores the credential this-device-only and
  updates it in place instead of deleting it first. A credential an earlier
  build stored is kept and moved to the new class the first time it is loaded.
- `OpenPush.Register(token, externalId)` is deprecated (a compiler warning,
  same signature): a new external id is now logged in first, as
  `Login(externalId)` without an auth hash, so an identity is only ever
  attached through login and the device stays identified as before. Use
  `Login` and the new `Register(token)`.
- New events `OpenPush.Logged`, `OpenPush.Registered` and
  `OpenPush.RegistrationFailed` let several subscribers listen. The assignable
  `OnLog`, `OnRegistered` and `OnRegistrationFailed` are unchanged and run
  first.
- Android release builds no longer copy native log lines to logcat; debuggable
  builds still do. `OnLog` is unchanged.
- The bundled native SDKs deliver their callbacks on the main thread. Every
  OpenPush event already reached C# on the Unity main thread, so C# code sees
  no difference.
- Fixed on Android: the dim behind a center, full-screen or carousel in-app
  message now covers the camera cutout and the status and navigation bars,
  while the message itself stays clear of them. Banners are unchanged.
