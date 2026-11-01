#!/usr/bin/env bash
# Exports an iOS project that signs manually, with the extension identifiers and
# profiles set in OpenPushSettings and two App Groups a host build step put on the app first, then
# checks the generated pbxproj and entitlements: each extension's bundle id, profile and Manual style
# in every configuration, and that the host's groups survive beside OpenPush's. Export only: no
# provisioning profiles exist on this machine, so nothing is signed or built.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity="${UNITY2022:?set UNITY2022 to a Unity 2022.3 editor binary}"
work="${OPENPUSH_EXPORT_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/openpush-ios-manual.XXXXXX")}"
export OPENPUSH_EXPORT_DIR="$work"
project="$work/Host"
rm -rf "$project" "$work/Build/ios-manual"; mkdir -p "$project/Packages" "$project/Assets/Editor" "$project/ProjectSettings"
editor_version="$(basename "$(cd "$(dirname "$unity")/../../.." && pwd)")"
printf 'm_EditorVersion: %s\n' "$editor_version" > "$project/ProjectSettings/ProjectVersion.txt"
cat > "$project/Packages/manifest.json" <<JSON
{ "dependencies": {
    "ai.openpush.unity": "file:$package_dir",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0" } }
JSON
cp "$package_dir/Tests~/UnityHost/Editor/OpenPushCiBuild.cs" "$project/Assets/Editor/"
"$unity" -batchmode -nographics -quit -projectPath "$project" -buildTarget iOS \
  -executeMethod OpenPushCiBuild.BuildIOSManualSigning -logFile "$work/unity-ios-manual.log" || { tail -80 "$work/unity-ios-manual.log"; exit 1; }
grep -q "OpenPushCiBuild: iOS manual-signing export succeeded" "$work/unity-ios-manual.log" || { tail -80 "$work/unity-ios-manual.log"; exit 1; }
xcode="$work/Build/ios-manual"
/usr/bin/python3 - "$xcode" <<'PY'
import json, plistlib, subprocess, sys
xcode = sys.argv[1]
project = json.loads(subprocess.check_output(["plutil", "-convert", "json", "-o", "-", xcode + "/Unity-iPhone.xcodeproj/project.pbxproj"]))
objects = project["objects"]
def target(name):
    return next(o for o in objects.values() if o.get("isa") == "PBXNativeTarget" and o.get("name") == name)
def configs(name):
    return [objects[g] for g in objects[target(name)["buildConfigurationList"]]["buildConfigurations"]]
failures = []
def expect(what, actual, wanted):
    if actual != wanted: failures.append(f"{what}: {actual!r}, expected {wanted!r}")
for name, bundle, profile in [("OpenPushNotificationService", "ai.openpush.cihost.LegacyNotificationServiceExtension", "CI Host NSE AppStore"),
                              ("OpenPushLiveActivityWidget", "ai.openpush.cihost.Widgets", "CI Host Widget AppStore")]:
    for config in configs(name):
        s = config["buildSettings"]; where = f"{name} {config['name']}"
        expect(where + " PRODUCT_BUNDLE_IDENTIFIER", s.get("PRODUCT_BUNDLE_IDENTIFIER"), bundle)
        expect(where + " PROVISIONING_PROFILE_SPECIFIER", s.get("PROVISIONING_PROFILE_SPECIFIER"), profile)
        expect(where + " CODE_SIGN_STYLE", s.get("CODE_SIGN_STYLE"), "Manual")
def groups(path):
    with open(xcode + "/" + path, "rb") as f:
        return plistlib.load(f).get("com.apple.security.application-groups", [])
expect("app App Groups", groups("Unity-iPhone/Unity-iPhone.entitlements"),
       ["group.ai.openpush.cihost.shared", "group.ai.openpush.cihost.analytics", "group.ai.openpush.cihost.legacy"])
expect("extension App Groups", groups("OpenPushNotificationService/OpenPushNotificationService.entitlements"), ["group.ai.openpush.cihost.legacy"])
if failures:
    print("\n".join(failures)); sys.exit(1)
print("✓ manual-signing export: extension ids, profiles and Manual style in every configuration; host App Groups kept")
PY
