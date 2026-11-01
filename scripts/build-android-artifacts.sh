#!/usr/bin/env bash
# Rebuilds Runtime/Plugins/Android/maven~ from openpush-android. The trailing ~ hides the folder
# from Unity's importer; EDM4U hands the path to Gradle, which reads it as a Maven repository.
# Requires JDK 17 (javac 21 output breaks Unity 2022.3's D8) and a clean native checkout.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
native_dir="${OPENPUSH_ANDROID_DIR:?set OPENPUSH_ANDROID_DIR to the openpush-android checkout}"
jdk="${JDK17:?set JDK17 to a JDK 17 home}"
: "${ANDROID_HOME:?set ANDROID_HOME}"
"$jdk/bin/java" -version 2>&1 | grep -q '"17\.' || { echo "JDK17 is not a JDK 17"; exit 1; }
[ -z "$(git -C "$native_dir" status --porcelain)" ] || { echo "openpush-android has uncommitted changes"; exit 1; }
expected_version=1.2.2
expected_min_sdk=24
source_version="$(sed -n 's/^OPENPUSH_VERSION=//p' "$native_dir/gradle.properties")"
[ "$source_version" = "$expected_version" ] \
  || { echo "openpush-android OPENPUSH_VERSION is '${source_version}', expected $expected_version"; exit 1; }
repo="$package_dir/Runtime/Plugins/Android/maven~"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-android-repo.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT
( cd "$native_dir" && JAVA_HOME="$jdk" ./gradlew -q -Porg.gradle.java.installations.paths="$jdk" \
    -PopenpushRepoDir="$scratch" \
    :openpush:publishAllPublicationsToOpenpushDirRepository \
    :openpush-fcm:publishAllPublicationsToOpenpushDirRepository \
    :openpush-in-app-messages:publishAllPublicationsToOpenpushDirRepository \
    :openpush-live-updates:publishAllPublicationsToOpenpushDirRepository )
# Bytecode gate before anything is replaced: the Kotlin modules target Java 11 (major 55) and
# the Java-only live-updates module Java 8 (major 52). Anything newer breaks Unity 2022.3's D8.
for pair in openpush-android:55 openpush-android-fcm:55 openpush-android-in-app-messages:55 \
            openpush-android-live-updates:52; do
  a="${pair%%:*}"; want="${pair##*:}"
  classes="$scratch/bytecode/$a"
  mkdir -p "$classes"
  unzip -qo "$scratch/ai/openpush/$a/$expected_version/$a-$expected_version.aar" classes.jar -d "$classes"
  unzip -qo "$classes/classes.jar" -d "$classes/x"
  max="$(find "$classes/x" -name '*.class' -exec sh -c 'for f; do od -An -tu1 -j6 -N2 "$f"; done' _ {} + \
    | awk '{ m = $1 * 256 + $2; if (m > max) max = m } END { print max + 0 }')"
  [ "$max" = "$want" ] || { echo "$a: highest class-file major is $max, expected $want"; exit 1; }
  min_sdk="$(unzip -p "$scratch/ai/openpush/$a/$expected_version/$a-$expected_version.aar" AndroidManifest.xml \
    | sed -n 's/.*minSdkVersion="\([0-9]*\)".*/\1/p')"
  [ "$min_sdk" = "$expected_min_sdk" ] || { echo "$a: minSdkVersion is '$min_sdk', expected $expected_min_sdk"; exit 1; }
  echo "$a: class-file major $max, minSdkVersion $min_sdk"
done
rm -rf "$repo"
for a in openpush-android openpush-android-fcm openpush-android-in-app-messages openpush-android-live-updates; do
  mkdir -p "$repo/ai/openpush/$a/$expected_version"
  for ext in aar pom module; do
    cp "$scratch/ai/openpush/$a/$expected_version/$a-$expected_version.$ext" "$repo/ai/openpush/$a/$expected_version/"
  done
  cp "$scratch/ai/openpush/$a/maven-metadata.xml" "$repo/ai/openpush/$a/"
done
( cd "$repo" && find ai -type f | LC_ALL=C sort | xargs shasum -a 256 > SHA256SUMS )
# The stamp names the public release tag the natives are built for, not a local branch or commit.
cat > "$repo/openpush-android.version.txt" <<EOF
version=$source_version
source_repository=openpush-android
source_tag=$source_version
jdk=$("$jdk/bin/java" -version 2>&1 | head -1)
kotlin=2.0.21
agp=8.12.2
bytecode=java11
min_sdk=$expected_min_sdk
built_at=$(date -u +%Y-%m-%dT%H:%M:%SZ)
build_command=OPENPUSH_ANDROID_DIR=<openpush-android checkout> JDK17=<JDK 17 home> scripts/build-android-artifacts.sh
EOF
echo "vendored into $repo"
