#!/usr/bin/env bash
# Exports an Xcode project from a throwaway Unity 2022.3 host, builds it with xcodebuild, and checks
# UnityFramework and both injected extensions for unresolved OpenPush symbols.
# OPENPUSH_IOS_SDK=device builds for generic iOS with signing off; the default is the simulator.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity="${UNITY2022:?set UNITY2022 to a Unity 2022.3 editor binary}"
work="${OPENPUSH_EXPORT_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/openpush-ios-export.XXXXXX")}"
export OPENPUSH_EXPORT_DIR="$work"
export OPENPUSH_IOS_SDK="${OPENPUSH_IOS_SDK:-simulator}"
project="$work/Host"
rm -rf "$project" "$work/Build/ios" "$work/dd"; mkdir -p "$project/Packages" "$project/Assets/Editor" "$project/ProjectSettings"
# An existing ProjectVersion.txt keeps Unity from adding its default package set to a new project.
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
  -executeMethod OpenPushCiBuild.BuildIOS -logFile "$work/unity-ios.log" || { tail -80 "$work/unity-ios.log"; exit 1; }
grep -q "OpenPushCiBuild: iOS export succeeded" "$work/unity-ios.log" || { tail -80 "$work/unity-ios.log"; exit 1; }
xcode="$work/Build/ios"
if [ "$OPENPUSH_IOS_SDK" = device ]; then
  sdk=iphoneos; dest='generic/platform=iOS'; products=Release-iphoneos
else
  sdk=iphonesimulator; dest='generic/platform=iOS Simulator'; products=Release-iphonesimulator
fi
( cd "$xcode" && xcodebuild -project Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -sdk "$sdk" \
    -destination "$dest" -derivedDataPath "$work/dd" CODE_SIGNING_ALLOWED=NO build > "$work/xcodebuild.log" 2>&1 ) \
  || { grep -E "error:" "$work/xcodebuild.log" | head -20; tail -40 "$work/xcodebuild.log"; exit 1; }
built="$work/dd/Build/Products/$products"
"$package_dir/scripts/check-unityframework-symbols.sh" "$built/UnityFramework.framework/UnityFramework"
# The managed platform must survive stripping: its P/Invokes then appear in the IL2CPP output.
grep -rqs "openpush_unity_set_event_callback" "$xcode/Il2CppOutputProject/Source/il2cppOutput" \
  || { echo "OpenPush.iOS was stripped from the player (no bridge P/Invokes in the IL2CPP output)"; exit 1; }
echo "✓ OpenPush.iOS survives managed stripping"
grep -q "OpenPushNotificationService" "$xcode/Unity-iPhone.xcodeproj/project.pbxproj" || { echo "service extension target missing"; exit 1; }
# The extensions link their own static frameworks; nothing of ours may stay unresolved there either.
for appex in "$built"/*.app/PlugIns/OpenPush*.appex; do
  name="$(basename "$appex" .appex)"
  if grep -qi openpush <<<"$(nm -u "$appex/$name")"; then echo "undefined OpenPush symbols in $name"; exit 1; fi
done
echo "✓ iOS export builds ($sdk)"
