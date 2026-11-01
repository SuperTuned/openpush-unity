#!/usr/bin/env bash
set -euo pipefail

# Runs the iOS Keychain credential store (Runtime/Plugins/iOS/OpenPushCredentialStore.m) on this Mac
# against an in-memory Keychain: in-place writes, and the migration of an item an earlier build
# wrote with another accessibility class. A test process has no Keychain entitlement, which is why
# the store's four Keychain calls sit behind a table this test replaces.

if [ "$(uname -s)" != "Darwin" ]; then
  echo "This check requires macOS." >&2
  exit 1
fi

repo_root="$(cd "$(dirname "$0")/../.." && pwd)"
scratch="$(mktemp -d "${TMPDIR:-/tmp}/openpush-credential-store.XXXXXX")"
trap 'rm -rf "$scratch"' EXIT

xcrun clang -fobjc-arc -Wall -Wextra -Werror -DOPENPUSH_CREDENTIAL_STORE_TESTING \
  -framework Foundation -framework Security \
  "$repo_root/Runtime/Plugins/iOS/OpenPushCredentialStore.m" \
  "$repo_root/Tests~/Native/OpenPushCredentialStoreTest.m" -o "$scratch/credential-store-test"
"$scratch/credential-store-test"
