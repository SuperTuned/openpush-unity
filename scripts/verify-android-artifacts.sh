#!/usr/bin/env bash
# Verifies the vendored Android natives: checksums, version stamp, the four artifacts, and their
# bytecode (Unity 2022.3's D8 rejects Java 17 classes): the highest class-file major in each AAR must
# be exactly 55 (52 for the Java-only live-updates module). The class headers are read with od, so
# no JDK is needed and the check never skips.
#
# It also holds the three places that name the native version to the one maven~ holds: the
# androidlib's build.gradle (what the adapter compiles against in the exported Gradle project) and
# OpenPushAndroidDependenciesXml.NativeVersion (what EDM4U resolves for the player). The in-package
# repository has no other version, so a pin left behind by a release fails every host's Android
# build with an unresolved dependency.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
repo="$package_dir/Runtime/Plugins/Android/maven~"
[ -d "$repo" ] || { echo "missing $repo (run scripts/build-android-artifacts.sh)"; exit 1; }
cd "$repo"
shasum -a 256 -c SHA256SUMS >/dev/null
version="$(sed -n 's/^version=//p' openpush-android.version.txt)"
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo "openpush-android.version.txt has no version= line"; exit 1; }
grep -q '^bytecode=java11$' openpush-android.version.txt
grep -q '^min_sdk=24$' openpush-android.version.txt
grep -q "^source_tag=$version\$" openpush-android.version.txt \
  || { echo "openpush-android.version.txt has no source_tag=$version line"; exit 1; }
! grep -qE '^(source_commit|source_branch)=' openpush-android.version.txt \
  || { echo "openpush-android.version.txt names a commit or branch; it must name the release tag only"; exit 1; }

gradle="$package_dir/Runtime/Plugins/Android/OpenPushUnity.androidlib/build.gradle"
gradle_pins="$(sed -n "s/.*['\"]ai\.openpush:\([a-z-]*\):\([0-9.]*\)['\"].*/\1:\2/p" "$gradle")"
[ -n "$gradle_pins" ] || { echo "no ai.openpush dependencies in $gradle"; exit 1; }
while IFS=: read -r artifact pinned; do
  [ "$pinned" = "$version" ] \
    || { echo "build.gradle pins ai.openpush:$artifact:$pinned but maven~ holds $version"; exit 1; }
done <<< "$gradle_pins"
xml_cs="$package_dir/Editor/Android/OpenPushAndroidDependenciesXml.cs"
xml_version="$(sed -n 's/.*NativeVersion = "\([0-9.]*\)".*/\1/p' "$xml_cs")"
[ "$xml_version" = "$version" ] \
  || { echo "OpenPushAndroidDependenciesXml.NativeVersion is '$xml_version' but maven~ holds $version"; exit 1; }

for a in openpush-android openpush-android-fcm openpush-android-in-app-messages openpush-android-live-updates; do
  for ext in aar pom; do
    test -f "ai/openpush/$a/$version/$a-$version.$ext" || { echo "missing $a-$version.$ext"; exit 1; }
  done
  other="$(find "ai/openpush/$a" -mindepth 1 -maxdepth 1 -type d ! -name "$version" | head -1)"
  [ -z "$other" ] || { echo "maven~ holds a second version of $a: $other"; exit 1; }
done
scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-verify-android.XXXXXX")"; trap 'rm -rf "$scratch"' EXIT
for pair in openpush-android:55 openpush-android-fcm:55 openpush-android-in-app-messages:55 \
            openpush-android-live-updates:52; do
  a="${pair%%:*}"; want="${pair##*:}"
  mkdir -p "$scratch/$a"
  unzip -qo "ai/openpush/$a/$version/$a-$version.aar" classes.jar -d "$scratch/$a"
  unzip -qo "$scratch/$a/classes.jar" -d "$scratch/$a/x"
  count="$(find "$scratch/$a/x" -name '*.class' | wc -l | tr -d ' ')"
  [ "$count" -gt 0 ] || { echo "$a: no classes in classes.jar"; exit 1; }
  max="$(find "$scratch/$a/x" -name '*.class' -exec sh -c 'for f; do od -An -tu1 -j6 -N2 "$f"; done' _ {} + \
    | awk '{ m = $1 * 256 + $2; if (m > max) max = m } END { print max + 0 }')"
  [ "$max" = "$want" ] || { echo "$a: highest class-file major is $max, expected $want"; exit 1; }
  min_sdk="$(unzip -p "ai/openpush/$a/$version/$a-$version.aar" AndroidManifest.xml \
    | sed -n 's/.*minSdkVersion="\([0-9]*\)".*/\1/p')"
  [ "$min_sdk" = 24 ] || { echo "$a: minSdkVersion is '$min_sdk', expected 24"; exit 1; }
done
echo "✓ vendored Android natives verified ($version; build.gradle and OpenPushAndroidDependenciesXml agree)"
