#!/usr/bin/env bash
set -euo pipefail

# One opener per tap on iOS. The Unity bridge opens a tapped push's URL itself, from the native tap
# (OpenPushUnityLaunchURL, governed by OpenPushSettings.OpenLaunchUrls through the Info.plist key
# OpenPushSuppressLaunchURLs), so a cold start does not wait for C#. The native SDK has its own
# opener (OpenPush.Notifications.openLaunchURLs, on by default), so the bridge must turn
# it off before the native SDK initializes, and never back on: with both on, every link would open
# twice. A tap on an action button with its own link opens that link instead of the launch URL.
# The bridge runs only inside an exported Xcode app, so this is a static check of its source.

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
plugins="$repo_root/Runtime/Plugins/iOS"
bridge="$plugins/OpenPushUnityBridge.swift"
notifications="$plugins/OpenPushUnityBridgeNotifications.swift"
status=0

# The body of openpush_unity_initialize: from its declaration to the next @_cdecl.
init_body="$(awk '/^public func openpush_unity_initialize\(/{on=1} on&&/^@_cdecl/{exit} on{print}' "$bridge")"
[ -n "$init_body" ] || { echo "✗ openpush_unity_initialize not found in $(basename "$bridge")"; exit 1; }
off_line="$(grep -n "OpenPush.Notifications.openLaunchURLs = false" <<<"$init_body" | head -1 | cut -d: -f1 || true)"
native_init_line="$(grep -n "OpenPush.initialize(" <<<"$init_body" | head -1 | cut -d: -f1 || true)"
if [ -z "$off_line" ]; then
  echo "✗ openpush_unity_initialize does not set OpenPush.Notifications.openLaunchURLs = false"; status=1
elif [ -z "$native_init_line" ] || [ "$off_line" -gt "$native_init_line" ]; then
  echo "✗ openLaunchURLs is not turned off before OpenPush.initialize"; status=1
fi

if grep -n "openLaunchURLs *= *true" "$plugins"/*.swift "$plugins"/*.mm "$plugins"/*.m 2>/dev/null; then
  echo "✗ the bridge turns the native launch-URL opener on"; status=1
fi

# The bridge's own opener prefers the tapped button's link over the push's launch URL.
grep -q 'guard let url = buttonURL ?? notification.launchURL else' "$notifications" \
  || { echo "✗ OpenPushUnityLaunchURL.openIfEnabled no longer prefers the tapped button's url"; status=1; }
grep -q '"action_url": OpenPushUnityJSON.null(actionURL?.absoluteString)' "$notifications" \
  || { echo "✗ the click event no longer carries action_url"; status=1; }

[ "$status" -eq 0 ] && echo "✓ the bridge owns iOS link opening: native openLaunchURLs off before initialize, button links preferred"
exit "$status"
