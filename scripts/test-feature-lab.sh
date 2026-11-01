#!/usr/bin/env bash
# Runs the Feature Lab sample's EditMode tests in a throwaway Unity 2022.3 host that installs the package
# and copies the sample in (samples are not compiled inside the package itself).
# Keep TMPDIR short (the macOS default /var/folders/... is fine): Unity's Bee build IPC uses a Unix
# domain socket under it, and a path over 104 characters fails compilation with an ArgumentOutOfRangeException.
set -euo pipefail
package_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity="${UNITY2022:?set UNITY2022 to a Unity 2022.3 editor binary}"
work="$(mktemp -d "${TMPDIR:-/tmp}/openpush-featurelab.XXXXXX")"
project="$work/Host"
mkdir -p "$project/Packages" "$project/Assets/Samples" "$project/ProjectSettings"
# An existing ProjectVersion.txt keeps Unity from adding its default package set to a new project.
editor_version="$(basename "$(cd "$(dirname "$unity")/../../.." && pwd)")"
printf 'm_EditorVersion: %s\n' "$editor_version" > "$project/ProjectSettings/ProjectVersion.txt"
cat > "$project/Packages/manifest.json" <<EOF
{ "dependencies": {
    "ai.openpush.unity": "file:$package_dir",
    "com.unity.test-framework": "1.1.33",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0" },
  "testables": ["ai.openpush.unity"] }
EOF
cp -R "$package_dir/Samples~/FeatureLab" "$project/Assets/Samples/FeatureLab"
"$unity" -batchmode -nographics -projectPath "$project" -runTests -testPlatform EditMode \
  -testResults "$work/results.xml" -logFile "$work/unity-tests.log" || true
[ -f "$work/results.xml" ] || { tail -60 "$work/unity-tests.log"; exit 1; }
failed="$(grep -o 'failed="[0-9]*"' "$work/results.xml" | head -1 | tr -dc '0-9')"
total="$(grep -o 'total="[0-9]*"' "$work/results.xml" | head -1 | tr -dc '0-9')"
[ "${failed:-1}" = 0 ] && [ "${total:-0}" -gt 0 ] || { echo "Feature Lab tests: $failed failed of $total"; exit 1; }
echo "✓ Feature Lab EditMode tests: $total passed"
