#!/usr/bin/env bash
set -euo pipefail

# Type-checks the Swift the package ships against the vendored native frameworks:
# the Unity bridge (in both Swift language modes, since hosts pick SWIFT_VERSION)
# and the two extension templates, which live as .txt so Unity does not compile
# them and are copied verbatim into an exported Xcode project.

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This type-check requires macOS and the iOS SDK." >&2
  exit 1
fi

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-swift-typecheck.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

native="$repo_root/Runtime/Plugins/iOS/Native~"
sim="ios-arm64_x86_64-simulator"
[ -d "$native/OpenPush.xcframework" ] || { echo "missing vendored XCFrameworks (run scripts/build-ios-artifacts.sh)"; exit 1; }
simsdk="$(xcrun --sdk iphonesimulator --show-sdk-path)"
frameworks=(-F "$native/OpenPushCore.xcframework/$sim" -F "$native/OpenPushLiveActivities.xcframework/$sim"
            -F "$native/OpenPush.xcframework/$sim" -F "$native/OpenPushNSE.xcframework/$sim")

# Behavioural probe for the held pre-initialize queue (Foundation only, so it runs on the host
# whatever Swift reads the vendored interfaces): bounded at its cap, keeps the newest, drains once.
cat > "$scratch/HeldProbe.swift" <<'SWIFT'
@main struct HeldProbe {
    static func main() {
        let queue = OpenPushUnityHeldQueue<Int>(cap: 10)
        for value in 1...12 { queue.hold(value) }
        let drained = queue.drain()
        precondition(drained == Array(3...12), "held queue must keep the newest 10, got \(drained)")
        precondition(queue.drain().isEmpty, "a drained queue must be empty")
        queue.hold(99)
        precondition(queue.drain() == [99], "a queue must hold again after a drain")
        print("✓ held queue: cap 10, newest kept, drained exactly once")
    }
}
SWIFT
echo "→ held pre-initialize queue probe"
xcrun swiftc -parse-as-library "$repo_root/Runtime/Plugins/iOS/OpenPushUnityBridgeHeld.swift" \
  "$scratch/HeldProbe.swift" -o "$scratch/held-probe"
"$scratch/held-probe"

# The interfaces record the compiler that wrote them; an older Swift cannot read them. That is a
# toolchain gap on this machine (or runner), not a source error, so skip rather than fail.
needed="$(grep -h -o 'swiftlang-[0-9.]*\|Apple Swift version [0-9.]*' "$native"/OpenPush.xcframework/*/OpenPush.framework/Modules/OpenPush.swiftmodule/*.swiftinterface 2>/dev/null | head -1 || true)"
have="$(xcrun swiftc --version 2>&1 | head -1)"
echo "interfaces: $needed · compiler: $have"
if ! xcrun -sdk iphonesimulator swiftc -typecheck -target arm64-apple-ios16.0-simulator -sdk "$simsdk" \
     -F "$native/OpenPushCore.xcframework/$sim" - <<<'import OpenPushCore' >/dev/null 2>&1; then
  echo "skipped: this Swift cannot read the vendored interfaces (need the Xcode recorded in openpush-ios.version.txt or newer)"
  exit 0
fi

bridge_sources=("$repo_root"/Runtime/Plugins/iOS/OpenPushUnityBridge*.swift)
# Warnings are errors: the bridge compiles inside every host's Xcode project, so a native deprecation
# it still calls (register(token:externalID:), the currentToken setter) must not reach them as a warning.
for mode in 5 6; do
  echo "→ Unity bridge (Swift $mode)"
  xcrun -sdk iphonesimulator swiftc -typecheck -warnings-as-errors -swift-version "$mode" -target arm64-apple-ios16.0-simulator \
    -sdk "$simsdk" "${frameworks[@]}" -module-name UnityFramework -parse-as-library "${bridge_sources[@]}"
done

for symbol in openpush_unity_process_click openpush_unity_process_foreground openpush_unity_complete_foreground \
              openpush_unity_display_foreground openpush_unity_request_permission openpush_unity_la_enter \
              openpush_unity_iam_add_triggers openpush_unity_set_presenter openpush_unity_set_badge_count \
              openpush_unity_process_foreground_data openpush_unity_process_click_data openpush_unity_process_displayed_data \
              openpush_unity_add_sms openpush_unity_remove_sms openpush_unity_add_whatsapp openpush_unity_remove_whatsapp \
              openpush_unity_add_email openpush_unity_remove_email \
              openpush_unity_verify_start openpush_unity_verify_check openpush_unity_verify_resend; do
  grep -q "@_cdecl(\"$symbol\")" "${bridge_sources[@]}" || { echo "missing @_cdecl $symbol"; exit 1; }
done

# Cold-start taps and deliveries captured before initialize are replayed right after it.
grep -q 'openPushReplayHeldNotifications()' <(sed -n '/@_cdecl("openpush_unity_initialize")/,/^}/p' "${bridge_sources[@]}") ||
  { echo "openpush_unity_initialize does not replay held notifications"; exit 1; }

cp "$repo_root/Editor/ServiceTemplate/OpenPushNotificationService.swift.txt" "$scratch/OpenPushNotificationService.swift"
echo "→ service extension template"
xcrun -sdk iphonesimulator swiftc -typecheck -swift-version 5 -target arm64-apple-ios16.0-simulator \
  -sdk "$simsdk" -F "$native/OpenPushCore.xcframework/$sim" -F "$native/OpenPushNSE.xcframework/$sim" \
  -module-name OpenPushNotificationService -parse-as-library "$scratch/OpenPushNotificationService.swift"

# What TakeOver and Share write into an existing extension (pinned by OpenPushExtensionSwiftGoldenTests).
for golden in "$repo_root"/Tests~/Fixtures/ios/expected/*.swift; do
  echo "→ existing-extension output $(basename "$golden")"
  for mode in 5 6; do
    xcrun -sdk iphonesimulator swiftc -typecheck -swift-version "$mode" -target arm64-apple-ios16.0-simulator \
      -sdk "$simsdk" -F "$native/OpenPushCore.xcframework/$sim" -F "$native/OpenPushNSE.xcframework/$sim" \
      -module-name ExistingExtension -parse-as-library "$golden"
  done
done

cp "$repo_root/Editor/WidgetTemplate/OpenPushDefaultWidget.swift.txt" "$scratch/OpenPushDefaultWidget.swift"
echo "→ Live Activity widget template"
xcrun -sdk iphonesimulator swiftc -typecheck -swift-version 5 -target arm64-apple-ios16.1-simulator \
  -sdk "$simsdk" -F "$native/OpenPushLiveActivities.xcframework/$sim" \
  -module-name OpenPushLiveActivityWidget -parse-as-library "$scratch/OpenPushDefaultWidget.swift"

echo "✓ the Unity bridge, both extension templates and the existing-extension output type-check against the vendored frameworks"
