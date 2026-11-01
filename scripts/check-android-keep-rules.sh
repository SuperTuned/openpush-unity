#!/usr/bin/env bash
# The adapter's consumer rules must keep everything C# reaches by name over JNI (a
# Minify build without them fails with ClassNotFoundException: ai.openpush.android.OpenPush).
set -euo pipefail
lib="$(cd "$(dirname "$0")/.." && pwd)/Runtime/Plugins/Android/OpenPushUnity.androidlib"
rules="$lib/consumer-proguard.pro"
[ -f "$rules" ] || { echo "missing $rules"; exit 1; }
grep -q "consumerProguardFiles 'consumer-proguard.pro'" "$lib/build.gradle" || { echo "build.gradle does not apply consumer-proguard.pro"; exit 1; }
status=0
for rule in \
  '-keep class ai.openpush.** { public *; }' \
  '-keep class ai.openpush.unity.** { *; }' \
  '-keep interface kotlin.jvm.functions.Function* { *; }' \
  '-keep class kotlin.Unit { public static final kotlin.Unit INSTANCE; }' \
  '-keep interface ai.openpush.unity.OpenPushUnityListener { *; }' \
  '-keep interface ai.openpush.unity.OpenPushUnityCallback { *; }' \
  '-keep interface ai.openpush.unity.OpenPushUnityCompletion { *; }' \
  '-dontwarn android.app.Notification$ProgressStyle' \
  '-dontwarn android.app.Notification$ProgressStyle$Segment'; do
  grep -qF -- "$rule" "$rules" || { echo "missing rule: $rule"; status=1; }
done
[ "$status" -eq 0 ] && echo "✓ adapter keep rules present and applied"
exit "$status"
