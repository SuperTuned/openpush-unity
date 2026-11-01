#!/usr/bin/env bash
set -euo pipefail

# Compiles the OpenPush.Runtime sources (Core, Contract, Events, Stub, Facade, Migration,
# Unity) and the Feature Lab runtime under
# UNITY_IOS and under UNITY_ANDROID, which is the one thing the dotnet test
# projects cannot do:
# they compile against Unity-free stand-ins with no platform define set, so
# every `#if UNITY_IOS` / `#if UNITY_ANDROID` branch in the package — the
# native presenter, the notification bridges, the permission bridge — is dead
# text to them. A missing `using` in one of those branches reaches a device
# build and nothing earlier.
#
# It needs Unity's own managed assemblies, so it runs where Unity is installed
# and skips cleanly everywhere else (GitHub's runners have no Unity). Keeping
# it wired into CI anyway is deliberate: the step is then one install away from
# being real, and it never silently stops existing.

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"

# UNITY_EDITOR_PATH (an editor folder, its Unity.app, or the Unity binary inside
# it) picks one install. Otherwise the newest install wins; the glob sorts
# lexically, which is good enough because any of them compiles this package.
managed=""
if [ -n "${UNITY_EDITOR_PATH:-}" ]; then
  for candidate in "$UNITY_EDITOR_PATH/Unity.app/Contents/Managed" \
                   "$UNITY_EDITOR_PATH/Contents/Managed" \
                   "$(dirname "$UNITY_EDITOR_PATH")/../Managed"; do
    [ -d "$candidate/UnityEngine" ] && { managed="$(cd "$candidate" && pwd)"; break; }
  done
else
  for candidate in /Applications/Unity/Hub/Editor/*/Unity.app/Contents/Managed; do
    [ -d "$candidate/UnityEngine" ] || continue
    managed="$candidate"
  done
fi

if [ -z "$managed" ]; then
  echo "skipped: no Unity install under /Applications/Unity/Hub/Editor — the platform compile needs Unity's managed assemblies"
  exit 0
fi

contents="$(dirname "$managed")"
compiler="$contents/DotNetSdkRoslyn/csc.dll"
netstandard="$contents/NetStandard/ref/2.1.0/netstandard.dll"

if [ ! -f "$compiler" ] || [ ! -f "$netstandard" ] || ! command -v dotnet >/dev/null 2>&1; then
  echo "skipped: this Unity install ships no Roslyn/netstandard pair this script can drive ($contents)"
  exit 0
fi

scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-platform-compile.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

# References and sources go in a compiler response file rather than an argument
# list: there are over a hundred of them, and a response file also keeps this
# readable when a compile does fail.
inputs="$scratch/inputs.rsp"
refs="$scratch/refs.rsp"
: > "$inputs"
: > "$refs"

# UnityEngine.dll itself is left out on purpose: it forwards to — and so
# duplicates — every module type, and every source file then fails CS0433.
modules=0
for module in "$managed"/UnityEngine/UnityEngine.*Module.dll; do
  [ -f "$module" ] || continue
  echo "-r:\"$module\"" >> "$refs"
  modules=$((modules + 1))
done

if [ "$modules" -eq 0 ]; then
  echo "skipped: no UnityEngine modules under $managed/UnityEngine"
  exit 0
fi

sources=0
while IFS= read -r file; do
  echo "\"$file\"" >> "$inputs"
  sources=$((sources + 1))
done < <(find "$repo_root/Runtime/AssemblyInfo.cs" "$repo_root/Runtime/Core" \
              "$repo_root/Runtime/Contract" "$repo_root/Runtime/Events" \
              "$repo_root/Runtime/Stub" "$repo_root/Runtime/Facade" "$repo_root/Runtime/Migration" "$repo_root/Runtime/Unity" \
              "$repo_root/Samples~/FeatureLab/OpenPushFeatureLab/Runtime" \
              -name '*.cs' | sort)

if [ "$sources" -eq 0 ]; then
  echo "no C# sources under Runtime or Feature Lab" >&2
  exit 1
fi

echo "Unity managed assemblies: $managed ($modules modules, $sources sources)"

for define in UNITY_IOS UNITY_ANDROID; do
  echo "→ compiling SDK + Feature Lab runtime with $define"
  # CS0618 is the Android branch's obsolete PermissionCallbacks member, a known
  # and deliberate call. Everything else is an error: the package builds with
  # TreatWarningsAsErrors.
  dotnet exec "$compiler" \
    -nologo \
    -noconfig \
    -nostdlib+ \
    -target:library \
    -langversion:9.0 \
    -warnaserror+ \
    -nowarn:618 \
    "-define:$define;UNITY_2019_4_OR_NEWER" \
    "-r:$netstandard" \
    "-out:$scratch/OpenPush.$define.dll" \
    "@$refs" \
    "@$inputs"
done

# The platform assemblies compile on their own, against the runtime built under their define,
# exactly as Unity builds them: OpenPush.Android (includePlatforms: Android) sees the runtime's
# internals through InternalsVisibleTo and nothing else.
android_inputs="$scratch/android.rsp"
: > "$android_inputs"
if [ -d "$repo_root/Runtime/Platforms/Android" ]; then
  find "$repo_root/Runtime/Platforms/Android" -name '*.cs' | sort | sed 's/.*/"&"/' >> "$android_inputs"
fi
if [ -s "$android_inputs" ]; then
  echo "→ compiling OpenPush.Android against the UNITY_ANDROID runtime"
  dotnet exec "$compiler" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 \
    -warnaserror+ -nowarn:618 "-define:UNITY_ANDROID;UNITY_2019_4_OR_NEWER" \
    "-r:$netstandard" "-r:$scratch/OpenPush.UNITY_ANDROID.dll" \
    "-out:$scratch/OpenPush.Android.dll" "@$refs" "@$android_inputs"
fi
[ -f "$scratch/OpenPush.Android.dll" ] || { echo "OpenPush.Android did not compile"; exit 1; }
grep -q "PermissionCallbacks" "$repo_root/Runtime/Platforms/Android/AndroidPermissionRequester.cs" \
  || { echo "AndroidPermissionRequester still uses the placeholder"; exit 1; }

# OpenPush.iOS (includePlatforms: iOS) the same way, against the UNITY_IOS runtime.
ios_inputs="$scratch/ios.rsp"
: > "$ios_inputs"
if [ -d "$repo_root/Runtime/Platforms/iOS" ]; then
  find "$repo_root/Runtime/Platforms/iOS" -name '*.cs' | sort | sed 's/.*/"&"/' >> "$ios_inputs"
fi
if [ -s "$ios_inputs" ]; then
  echo "→ compiling OpenPush.iOS against the UNITY_IOS runtime"
  dotnet exec "$compiler" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 \
    -warnaserror+ -nowarn:618 "-define:UNITY_IOS;UNITY_2019_4_OR_NEWER" \
    "-r:$netstandard" "-r:$scratch/OpenPush.UNITY_IOS.dll" \
    "-out:$scratch/OpenPush.iOS.dll" "@$refs" "@$ios_inputs"
fi
[ -f "$scratch/OpenPush.iOS.dll" ] || { echo "OpenPush.iOS did not compile"; exit 1; }
# Every entry point the iOS platform imports must be a @_cdecl in the Swift bridge.
for fn in $(grep -o 'static extern [^(]* \(openpush_unity_[a-z_]*\)' "$repo_root/Runtime/Platforms/iOS/IOSNative.cs" | awk '{print $NF}'); do
  grep -q "@_cdecl(\"$fn\")" "$repo_root"/Runtime/Plugins/iOS/OpenPushUnityBridge*.swift || { echo "no @_cdecl for $fn"; exit 1; }
done

grep -q "AndroidPushDelivery" "$repo_root/Editor/OpenPushSettingsProvider.cs" || { echo "settings provider lacks the Android delivery section"; exit 1; }

# OpenPush.Editor compiles against UnityEditor plus the iOS and Android editor extensions, over a
# runtime built with UNITY_EDITOR, under each build-target define the Editor code branches on.
editor_root="$(dirname "$contents")/.."
editor_refs="$scratch/editor-refs.rsp"
: > "$editor_refs"
for dll in "$managed"/UnityEngine/UnityEditor.*Module.dll \
           "$editor_root"/PlaybackEngines/iOSSupport/UnityEditor.iOS.Extensions.Xcode.dll \
           "$editor_root"/PlaybackEngines/iOSSupport/UnityEditor.iOS.Extensions.Common.dll \
           "$editor_root"/PlaybackEngines/AndroidPlayer/UnityEditor.Android.Extensions.dll; do
  [ -f "$dll" ] && echo "-r:\"$dll\"" >> "$editor_refs"
done
editor_inputs="$scratch/editor.rsp"
find "$repo_root/Editor" -name '*.cs' | sort | sed 's/.*/"&"/' > "$editor_inputs"
for define in UNITY_IOS UNITY_ANDROID; do
  echo "→ compiling OpenPush.Editor with UNITY_EDITOR;$define"
  mkdir -p "$scratch/editor-$define"
  dotnet exec "$compiler" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 \
    -warnaserror+ -nowarn:618 "-define:UNITY_EDITOR;$define;UNITY_2019_4_OR_NEWER" \
    "-r:$netstandard" "-out:$scratch/editor-$define/OpenPush.Runtime.dll" "@$refs" "@$editor_refs" "@$inputs"
  dotnet exec "$compiler" -nologo -noconfig -nostdlib+ -target:library -langversion:9.0 \
    -warnaserror+ -nowarn:618 "-define:UNITY_EDITOR;$define;UNITY_2019_4_OR_NEWER" \
    "-r:$netstandard" "-r:$scratch/editor-$define/OpenPush.Runtime.dll" \
    "-out:$scratch/editor-$define/OpenPush.Editor.dll" "@$refs" "@$editor_refs" "@$editor_inputs"
done

echo "✓ SDK + Feature Lab runtime compile under UNITY_IOS and UNITY_ANDROID; OpenPush.Android, OpenPush.iOS and OpenPush.Editor compile"
