#!/usr/bin/env bash
set -euo pipefail

# String ownership across the IL2CPP boundary. IL2CPP frees every char* a
# P/Invoke returns with free(), so each native function returning a C string
# must hand back memory it no longer owns: strdup'd in the Swift bridge, a
# malloc'd copy in the credential store. A pointer into a Swift String or an
# NSString buffer would be freed twice or read after release.

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
plugins="$repo_root/Runtime/Plugins/iOS"
status=0

# Every @_cdecl that returns a C string: its declaration line (and the body that
# follows on the same or the next line) must go through strdup or openPushDup.
for file in "$plugins"/OpenPushUnityBridge*.swift; do
  while IFS= read -r hit; do
    line="${hit%%:*}"
    body="$(sed -n "${line},$((line + 3))p" "$file")"
    if ! grep -q "strdup(\|openPushDup(" <<<"$body"; then
      echo "✗ $(basename "$file"):$line returns a C string without strdup/openPushDup"
      status=1
    fi
  done < <(grep -n -- "-> UnsafeMutablePointer<CChar>?" "$file" | grep "public func openpush_unity_" || true)
done

count="$(cat "$plugins"/OpenPushUnityBridge*.swift | grep -c -- "public func openpush_unity_.*-> UnsafeMutablePointer<CChar>?" || true)"
[ "$count" -gt 0 ] || { echo "✗ found no string-returning bridge functions to check"; exit 1; }

# The credential store returns its load result as a malloc'd copy.
grep -q "return OpenPushCopyCString(" "$plugins/OpenPushCredentialStore.m" \
  || { echo "✗ OpenPushCredentialStore.m no longer returns a malloc'd copy"; status=1; }

# OpenPushNotifications.mm returns no strings; keep it that way.
if grep -Eq "^[[:space:]]*(const[[:space:]]+)?char[[:space:]]*\*[[:space:]]*_openpush_" "$plugins/OpenPushNotifications.mm"; then
  echo "✗ OpenPushNotifications.mm returns a C string; add it to this check"; status=1
fi

[ "$status" -eq 0 ] && echo "✓ every C string returned to IL2CPP is caller-owned ($count bridge functions + credential store)"
exit "$status"
