#!/usr/bin/env bash
# Rebuilds Runtime/Plugins/iOS/Native~ from openpush-ios (version 1.2.2). Unity 2022.3 has no
# .xcframework importer and would link each slice as a separate framework, so the folder is
# tilde-hidden and OpenPushIOSBuildSettings copies and links the frameworks at export.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
native_dir="${OPENPUSH_IOS_DIR:?set OPENPUSH_IOS_DIR to the openpush-ios checkout at version 1.2.2}"
# Fail closed: tracked files must be clean (the native script builds a `git archive HEAD` copy, so
# uncommitted edits would silently not ship) and its SDK version must be the one this package pins.
expected_version=1.2.2
[ -z "$(git -C "$native_dir" status --porcelain --untracked-files=no)" ] \
  || { echo "openpush-ios has uncommitted changes"; exit 1; }
source_version="$(git -C "$native_dir" show HEAD:Sources/OpenPushCore/OpenPushSDKVersion.swift \
  | sed -n 's/.*static let current = "\([0-9][0-9.]*\)".*/\1/p' || true)"
[ "$source_version" = "$expected_version" ] \
  || { echo "openpush-ios HEAD is version '${source_version}', expected $expected_version"; exit 1; }
out="$package_dir/Runtime/Plugins/iOS/Native~"
"$native_dir/scripts/build-ios-xcframeworks.sh" --output "$out"
cp "$native_dir/scripts/build-ios-xcframeworks.sh" "$out/build-ios-xcframeworks.sh"
# The source is identified by its public release tag and version, not by a local commit.
sed -i '' -e '/^source_commit=/d' -e '/^source_tag=/d' "$out/openpush-ios.version.txt"
cat >> "$out/openpush-ios.version.txt" <<EOF
source_tag=$source_version
source_version=$source_version
build_command=OPENPUSH_IOS_DIR=<openpush-ios checkout> scripts/build-ios-artifacts.sh
EOF
echo "vendored into $out"
