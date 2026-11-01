#!/usr/bin/env bash
set -euo pipefail

# Syntax-checks every Objective-C iOS plugin source against the real iOS SDK.
# The Swift bridge is type-checked by verify-swift-sources-compile.sh; these are
# the delegate hooks and small helpers that call it through extern "C".
#
# No linking and no running: these files call into UIKit and Unity's player
# hooks, so compiling them is the whole of what can be checked off-device.

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This compile check requires macOS and the iOS SDK." >&2
  exit 1
fi

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
sample_plugins="$repo_root/Samples~/FeatureLab/OpenPushFeatureLab/Plugins/iOS"
sdk="$(xcrun --sdk iphoneos --show-sdk-path)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-ios-compile.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

# Unity's AppDelegateListener header exists only inside an exported project, so
# the listener compiles against a stub declaring the three constants and the
# registration call it uses.
stub="$scratch/AppDelegateListener.h"
cat > "$stub" <<'STUB'
#import <Foundation/Foundation.h>
extern "C" NSString* const kUnityDidRegisterForRemoteNotificationsWithDeviceToken;
extern "C" NSString* const kUnityDidFailToRegisterForRemoteNotificationsWithError;
extern "C" NSString* const kUnityDidReceiveRemoteNotification;
@protocol AppDelegateListener <NSObject>
@end
extern "C" void UnityRegisterAppDelegateListener(id<AppDelegateListener> obj);
STUB

check() { # $1 = source, $2 = deployment target
  echo "→ $(basename "$1")"
  xcrun -sdk iphoneos clang -fsyntax-only -fobjc-arc -fmodules -Wall -Wextra -Werror -I "$scratch" \
    -target "arm64-apple-ios$2" -isysroot "$sdk" "$1"
}

for source in OpenPushUnityAppDelegateListener.mm OpenPushNotificationClick.mm OpenPushNotificationForeground.mm \
              OpenPushNotifications.mm OpenPushCredentialStore.m; do
  check "$repo_root/Runtime/Plugins/iOS/$source" 16.0
done

# The FeatureLab sample keeps its own, older deployment target.
for file in "$sample_plugins"/*.mm; do
  check "$file" 13.0
done

if grep -q "_openpush_take_notification_click_json\|_openpush_take_foreground_json\|OpenPushPendingClicks" \
     "$repo_root"/Runtime/Plugins/iOS/OpenPushNotification{Click,Foreground}.mm; then
  echo "delegate capture still queues for C# polling"; exit 1
fi
grep -q "openpush_unity_process_click" "$repo_root/Runtime/Plugins/iOS/OpenPushNotificationClick.mm" || { echo "click capture does not call the bridge"; exit 1; }
grep -q "openpush_unity_process_foreground" "$repo_root/Runtime/Plugins/iOS/OpenPushNotificationForeground.mm" || { echo "foreground capture does not call the bridge"; exit 1; }

for gone in OpenPushLiveActivities.swift OpenPushLiveActivitiesShim.mm OpenPushExtensionContext.m OpenPushDeviceInfo.m \
            OpenPushInAppMessagePresenter.mm OpenPushPermission.m; do
  [ ! -e "$repo_root/Runtime/Plugins/iOS/$gone" ] || { echo "$gone must be deleted (the native SDK owns it)"; exit 1; }
done

echo "✓ iOS plugin sources compile"
