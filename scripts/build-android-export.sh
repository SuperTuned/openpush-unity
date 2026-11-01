#!/usr/bin/env bash
# Builds an Android APK from a throwaway Unity 2022.3 project that installs this package, resolves the
# vendored natives with EDM4U 1.2.189 from OpenUPM and builds with Unity's Gradle.
# OPENPUSH_INSTALL=tarball installs from a .tgz (Library/PackageCache path); OPENPUSH_MINIFY=1 builds
# a minified release (the R8 keep-rule check). OPENPUSH_MIN_SDK (default 24) sets the player's
# Minimum API Level; 26 builds against natives vendored before their floor moved to 24.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity="${UNITY2022:?set UNITY2022 to a Unity 2022.3 editor binary}"
work="${OPENPUSH_EXPORT_DIR:-$(mktemp -d "${TMPDIR:-/tmp}/openpush-android-export.XXXXXX")}"
export OPENPUSH_EXPORT_DIR="$work"
export OPENPUSH_MIN_SDK="${OPENPUSH_MIN_SDK:-24}"
project="$work/Host"
rm -rf "$project"; mkdir -p "$project/Packages" "$project/Assets/Editor" "$project/ProjectSettings"
# An existing ProjectVersion.txt keeps Unity from treating the folder as a new project and adding
# its default package set (ads, purchasing, analytics, ...), which this build does not want.
editor_version="$(basename "$(cd "$(dirname "$unity")/../../.." && pwd)")"
printf 'm_EditorVersion: %s\n' "$editor_version" > "$project/ProjectSettings/ProjectVersion.txt"
source_ref="file:$package_dir"
if [ "${OPENPUSH_INSTALL:-local}" = tarball ]; then
  stage="$work/stage/package"; rm -rf "$work/stage"; mkdir -p "$stage"
  rsync -a --exclude '.git' --exclude 'Tests~/bin' --exclude 'Tests~/obj' "$package_dir/" "$stage/"
  tar -czf "$work/ai.openpush.unity-1.2.2.tgz" -C "$work/stage" package
  source_ref="file:$work/ai.openpush.unity-1.2.2.tgz"
fi
cat > "$project/Packages/manifest.json" <<EOF
{
  "scopedRegistries": [
    { "name": "package.openupm.com", "url": "https://package.openupm.com", "scopes": ["com.google.external-dependency-manager"] }
  ],
  "dependencies": {
    "ai.openpush.unity": "$source_ref",
    "com.google.external-dependency-manager": "1.2.189",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0"
  }
}
EOF
cp "$package_dir/Tests~/UnityHost/Editor/OpenPushCiBuild.cs" "$project/Assets/Editor/"
"$unity" -batchmode -nographics -quit -projectPath "$project" -buildTarget Android \
  -executeMethod OpenPushCiBuild.BuildAndroid -logFile "$work/unity-android.log" \
  || { tail -80 "$work/unity-android.log"; exit 1; }
grep -q "OpenPushCiBuild: Android build succeeded" "$work/unity-android.log" || { tail -80 "$work/unity-android.log"; exit 1; }
if [ "${OPENPUSH_INSTALL:-local}" = tarball ]; then
  grep -rq "Library/PackageCache/ai.openpush.unity@" "$project/Assets/Plugins/Android/settingsTemplate.gradle" \
    || { echo "settingsTemplate.gradle does not point at the PackageCache copy"; exit 1; }
fi
# The managed platform must survive stripping (Unity ignores link.xml inside packages): the linker's
# output keeps OpenPush.Android.dll and IL2CPP generates code for the platform registration.
stripped="$(find "$project/Library/Bee/artifacts" -type d -name ManagedStripped 2>/dev/null | head -1)"
[ -n "$stripped" ] || { echo "no ManagedStripped folder in the build artifacts"; exit 1; }
[ -f "$stripped/OpenPush.Android.dll" ] \
  || { echo "OpenPush.Android was stripped from the player (no OpenPush.Android.dll in $stripped)"; exit 1; }
grep -rqs "AndroidPlatformRegistration" "$project/Library/Bee/artifacts/Android/il2cppOutput" \
  || { echo "OpenPush.Android was stripped from the player (no AndroidPlatformRegistration in the IL2CPP output)"; exit 1; }
echo "✓ OpenPush.Android survives managed stripping"
echo "✓ Android APK: $work/Build/openpush-host.apk"
