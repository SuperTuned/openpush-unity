# OpenPush Feature Lab

Import this sample from Package Manager, open any scene, and enter Play mode. The lab installs itself without changing your scene.

It starts in **Live** mode when `OpenPushSettings` carries both the App ID and the public SDK key, and in **Demo** mode otherwise, so every workspace can be explored without credentials or network calls. Use **Settings** to switch modes and enter the App ID and public SDK key from your OpenPush app. Never enter a device credential or server secret in the sample.

## What it covers

- Permission, subscription, initialization, and device registration snapshots, with the
  native SDK version and the Unity package version
- Simple, Image, Button, Custom, and Sound notification fixtures
- Editable custom notification title, description, and data
- Host-owned consent gating before SDK initialization
- Login, logout, tags, and session-tracked alias writes
- Foreground, click, permission, and subscription observers
- In-app messages: fetch (Demo mode renders the bundled fixture in the Editor), add a
  trigger, and a lifecycle line in the event stream for every display phase
- Android Live Updates with capability and promotion-settings checks
- Explicit iOS ActivityKit and native notification-host integration boundaries
- System, Light, and Dark appearance across the page and every modal

The Unity SDK is receive-only. On Android the native SDK fetches the FCM token and renders
notifications (Automatic delivery; fill the Firebase fields in `OpenPushSettings`, or keep
`google-services.json` when Firebase Unity is also in the project). On iOS the package forwards the APNs token Unity receives to the native SDK.
The sample calls no `Register` of its own. Sending stays in OpenPush Console or your backend.

### Setting up a project to try it on a device

Before a device build, open **Window → OpenPush → Setup** and press **Run all**. It creates
the settings asset, pastes the App ID and SDK key from the console's setup link on the
clipboard, adds EDM4U, resolves the Android libraries and fixes the API levels, then stops
at the first row that needs you.

If the project already had push set up for another framework, the **Existing push setup**
rows offer **Reuse** or **Create new** for its iOS notification service extension, App
Group, Android messaging service and icons. Reuse keeps the extension's bundle ID and
provisioning profile, so a manually signed project needs no new App ID. Under manual
signing, fill the extension's bundle ID and profile in the iOS section of
**Project Settings → OpenPush** when you create a new one. The package README covers both
in "One-click setup" and "Existing push setup: Reuse or Create new".

On the device, the lab's subscription panel shows the subscription id and token to target
from the console. Tapping a push sent to it adds a "Click listener" line with its message id to the event stream.

### UTM verification

Enable link tracking in the console and send a push whose launch URL is
`https://openpush.ai/?utm_source=owned#overview`. The received URL should retain
`utm_source=owned` and `#overview`, with the server adding medium and campaign.
The sample preserves the full URL; it does not generate or overwrite UTM values.
Custom `openpush://` routes are not tagged.
