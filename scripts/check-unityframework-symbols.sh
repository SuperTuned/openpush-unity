#!/usr/bin/env bash
# Unity links UnityFramework with -undefined dynamic_lookup, so a missing framework or slice shows up
# at runtime, not at link time. Any undefined OpenPush symbol here is a broken build.
set -euo pipefail
binary="${1:?usage: $0 <UnityFramework binary>}"
[ -f "$binary" ] || { echo "no binary at $binary"; exit 1; }
undefined="$(nm -u "$binary" | grep -i openpush || true)"
if [ -n "$undefined" ]; then
  echo "undefined OpenPush symbols in UnityFramework:"; echo "$undefined" | head -20; exit 1
fi
# grep -q would close the pipe early and, under pipefail, turn nm's SIGPIPE into a failure.
defined="$(nm "$binary")"
grep -q "_openpush_unity_initialize" <<<"$defined" || { echo "bridge symbols missing from UnityFramework"; exit 1; }
echo "✓ UnityFramework resolves every OpenPush symbol and exports the bridge"
