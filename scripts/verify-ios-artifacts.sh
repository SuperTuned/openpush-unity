#!/usr/bin/env bash
# Verifies the vendored iOS natives: checksums, version, both slices, static linkage.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
native="$package_dir/Runtime/Plugins/iOS/Native~"
[ -d "$native" ] || { echo "missing $native (run scripts/build-ios-artifacts.sh)"; exit 1; }
cd "$native"
shasum -a 256 -c SHA256SUMS >/dev/null
grep -q '^version=1.2.2$' openpush-ios.version.txt
grep -q '^source_version=1.2.2$' openpush-ios.version.txt
grep -q '^linkage=static$' openpush-ios.version.txt
grep -q '^source_tag=1.2.2$' openpush-ios.version.txt
! grep -qE '^(source_commit|source_branch|script_commit)=' openpush-ios.version.txt \
  || { echo "openpush-ios.version.txt names a commit or branch; it must name the release tag only"; exit 1; }
for m in OpenPushCore OpenPush OpenPushLiveActivities OpenPushNSE; do
  for slice in ios-arm64 ios-arm64_x86_64-simulator; do
    bin="$m.xcframework/$slice/$m.framework/$m"
    test -f "$bin" || { echo "missing $bin"; exit 1; }
    file "$bin" | grep -q 'ar archive' || { echo "$bin is not a static archive"; exit 1; }
  done
done
echo "✓ vendored iOS natives verified"
