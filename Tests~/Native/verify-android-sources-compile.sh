#!/usr/bin/env bash
set -euo pipefail

# Compiles the androidlib's Java adapter against an installed Android platform
# and the vendored native AARs, then runs its plain-JVM tests. Nothing else in
# the repository compiles these files: Unity hands them to Gradle at export
# time, so a signature drift against the natives would otherwise first show up
# in a host's Android build.
#
# It downloads nothing. It needs an Android SDK, a JDK 11 or newer and
# kotlin-stdlib 2.0.21 (from the Gradle cache, or the jar named by
# KOTLIN_STDLIB_JAR); a machine missing one is told which and the check passes.
# CI supplies all three on its macOS job, so the check runs there for real.

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"

manifest="$repo_root/Runtime/Plugins/Android/OpenPushUnity.androidlib/AndroidManifest.xml"
if ! grep -q 'android.permission.POST_NOTIFICATIONS' "$manifest"; then
  echo "Android library manifest must declare POST_NOTIFICATIONS" >&2
  exit 1
fi
sources_dir="$repo_root/Runtime/Plugins/Android/OpenPushUnity.androidlib/src/main/java"

sdk="${ANDROID_HOME:-${ANDROID_SDK_ROOT:-}}"
if [ -z "$sdk" ]; then
  echo "skipped: ANDROID_HOME is not set — the androidlib needs an android.jar to compile against"
  exit 0
fi

# Newest installed platform: these sources guard every API level at runtime, so
# they compile against any recent one.
android_jar=""
for candidate in "$sdk"/platforms/android-*/android.jar; do
  [ -f "$candidate" ] || continue
  android_jar="$candidate"
done

if [ -z "$android_jar" ]; then
  echo "skipped: no platforms/android-*/android.jar under $sdk"
  exit 0
fi

# First working compiler wins, in preference order: JAVA_HOME, then Unity's own
# OpenJDK (which a host building for Android already has), then PATH. "Working"
# means it runs AND takes `--release 11`: macOS ships a javac stub that only
# tells you to install Java, and an older Unity ships a JDK 8 that has no
# --release flag at all.
javac_bin=""
for candidate in "${JAVA_HOME:+$JAVA_HOME/bin/javac}" \
                 /Applications/Unity/Hub/Editor/*/PlaybackEngines/AndroidPlayer/OpenJDK/bin/javac \
                 "$(command -v javac || true)"; do
  [ -n "$candidate" ] && [ -x "$candidate" ] || continue
  "$candidate" --release 11 -version >/dev/null 2>&1 || continue
  javac_bin="$candidate"
  break
done

if [ -z "$javac_bin" ]; then
  echo "skipped: no working javac (set JAVA_HOME, or install a JDK)"
  exit 0
fi
java_bin="${javac_bin%/javac}/java"

scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-androidlib-compile.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

# The adapter compiles against the vendored natives: classes.jar out of each AAR in maven~, plus
# kotlin-stdlib 2.0.21 from the Gradle cache (present after any native build; skipped if absent).
repo="$repo_root/Runtime/Plugins/Android/maven~"
native_version="$(sed -n 's/^version=//p' "$repo/openpush-android.version.txt" 2>/dev/null || true)"
classpath="$android_jar"
for aar in "$repo"/ai/openpush/*/"${native_version:-none}"/*.aar; do
  [ -f "$aar" ] || { echo "skipped: no vendored AARs (run scripts/build-android-artifacts.sh)"; exit 0; }
  name="$(basename "$aar" .aar)"
  unzip -qo "$aar" classes.jar -d "$scratch/$name"
  classpath="$classpath:$scratch/$name/classes.jar"
done
# A fresh machine (every hosted runner) has no Gradle cache: `find` on the missing
# path exits 1, and under pipefail that would kill the script silently.
stdlib_dir="$HOME/.gradle/caches/modules-2/files-2.1/org.jetbrains.kotlin/kotlin-stdlib/2.0.21"
stdlib="${KOTLIN_STDLIB_JAR:-}"
if [ -n "$stdlib" ] && [ ! -f "$stdlib" ]; then echo "KOTLIN_STDLIB_JAR names a missing file: $stdlib" >&2; exit 1; fi
if [ -z "$stdlib" ] && [ -d "$stdlib_dir" ]; then
  stdlib="$(find "$stdlib_dir" -name 'kotlin-stdlib-2.0.21.jar' 2>/dev/null | head -1 || true)"
fi
if [ -z "$stdlib" ]; then echo "skipped: kotlin-stdlib 2.0.21 not in the Gradle cache"; exit 0; fi
classpath="$classpath:$stdlib"

echo "Android platform: $android_jar"
echo "Compiler: $javac_bin"
mkdir -p "$scratch/out"
"$javac_bin" --release 11 -Xlint:all -Xlint:-options -Werror -cp "$classpath" -d "$scratch/out" \
  "$sources_dir"/ai/openpush/unity/*.java \
  "$repo_root/Tests~/Native/OpenPushUnityBridgeForegroundTest.java" \
  "$repo_root/Tests~/Native/OpenPushUnityJsonRulesTest.java" \
  "$repo_root/Tests~/Native/OpenPushUnityBridgeLogcatTest.java"

"$java_bin" -cp "$scratch/out" ai.openpush.unity.OpenPushUnityBridgeForegroundTest
# android.jar on the run classpath only so the verifier can resolve org.json; the rules never call it.
"$java_bin" -cp "$scratch/out:$android_jar" ai.openpush.unity.OpenPushUnityJsonRulesTest
"$java_bin" -cp "$scratch/out:$android_jar" ai.openpush.unity.OpenPushUnityBridgeLogcatTest
# Release builds stay quiet: an info, debug or verbose logcat line in the adapter must sit behind the
# debuggable switch (native lines can carry the external id).
if grep -n 'Log\.[ivd](' "$sources_dir"/ai/openpush/unity/*.java | grep -v 'if (logcat) Log\.'; then
  echo "unconditional Log.i/d/v in the androidlib adapter: gate it on the debuggable build" >&2
  exit 1
fi
# Signature probe: the static surface the C# platform calls, compiled against the adapter.
cat > "$scratch/Probe.java" <<'EOF'
import ai.openpush.unity.OpenPushUnityBridge;
final class Probe {
    static void touch() {
        OpenPushUnityBridge.setListener((kind, json) -> { });
        OpenPushUnityBridge.initialize(null, "app", "key", "https://app.openpush.ai", "unity/1.2.2", null);
        OpenPushUnityBridge.foregroundCompletion().complete("fg-1", true);
        String tags = OpenPushUnityBridge.getTags();
        String sub = OpenPushUnityBridge.subscription();
        OpenPushUnityBridge.processForeground("{}");
        OpenPushUnityBridge.onPermissionResult(true);
        String result = OpenPushUnityBridge.handleLiveUpdate(null, "{}", "");
    }
}
EOF
"$javac_bin" --release 11 -Xlint:all -Xlint:-options -Werror -cp "$classpath:$scratch/out" -d "$scratch/out" "$scratch/Probe.java"
echo "✓ the androidlib adapter compiles against the vendored natives"
