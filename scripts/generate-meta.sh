#!/usr/bin/env bash
# Generates Unity .meta files for package assets. Idempotent: existing
# .meta files are left untouched so GUIDs stay stable across runs.
#
# Scope is deliberate: the Runtime, Editor, scripts, and release-notes trees plus
# the root assets Unity imports from a package. Samples~/ and Tests~/ are
# tilde-excluded, while .github/ is hidden from Unity's asset importer.
set -euo pipefail
cd "$(dirname "$0")/.."

guid() { uuidgen | tr -d '-' | tr '[:upper:]' '[:lower:]'; }

tail_block() {
  printf '  externalObjects: {}\n  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
}

# Android-only plugin importer, shared by the Java sources, the AAR, and the
# .androidlib folder Unity imports as a plugin rather than as a folder asset.
android_plugin_block() {
  printf 'PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n'
  printf '  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n'
  printf '  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n'
  printf '  validateReferences: 1\n  platformData:\n'
  printf '  - first:\n      Android: Android\n    second:\n      enabled: 1\n      settings: {}\n'
  printf '  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n'
  printf '  - first:\n      Editor: Editor\n    second:\n      enabled: 0\n      settings:\n        DefaultValueInitialized: true\n'
  printf '  userData:\n  assetBundleName:\n  assetBundleVariant:\n'
}

write_meta() { # $1 = asset path
  local target="$1.meta"
  [ -e "$target" ] && return 0
  {
    printf 'fileFormatVersion: 2\nguid: %s\n' "$(guid)"
    if [ -d "$1" ]; then
      case "$1" in
        # An .androidlib is an Android library project: Unity imports the whole
        # folder through the plugin importer, so it takes a PluginImporter meta
        # and never the folderAsset one. Its contents belong to the Gradle
        # module, not to the asset database, and get no metas of their own.
        *.androidlib)
          android_plugin_block ;;
        *)
          printf 'folderAsset: yes\nDefaultImporter:\n'; tail_block ;;
      esac
    else
      case "$1" in
        *.cs)
          printf 'MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n'
          printf '  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n'
          printf '  userData:\n  assetBundleName:\n  assetBundleVariant:\n' ;;
        *.asmdef)
          printf 'AssemblyDefinitionImporter:\n'; tail_block ;;
        package.json)
          printf 'PackageManifestImporter:\n'; tail_block ;;
        # Unity picks the importer by extension. Extensionless files such as
        # LICENSE fall through to DefaultImporter below, which is what the
        # editor reports importing them with.
        *.md)
          printf 'TextScriptImporter:\n'; tail_block ;;
        *.java|*.aar)
          android_plugin_block ;;
        *.m|*.mm|*.swift)
          printf 'PluginImporter:\n  externalObjects: {}\n  serializedVersion: 2\n'
          printf '  iconMap: {}\n  executionOrder: {}\n  defineConstraints: []\n'
          printf '  isPreloaded: 0\n  isOverridable: 0\n  isExplicitlyReferenced: 0\n'
          printf '  validateReferences: 1\n  platformData:\n'
          printf '  - first:\n      Any: \n    second:\n      enabled: 0\n      settings: {}\n'
          printf '  - first:\n      Editor: Editor\n    second:\n      enabled: 0\n      settings:\n        DefaultValueInitialized: true\n'
          printf '  - first:\n      iPhone: iOS\n    second:\n      enabled: 1\n      settings: {}\n'
          printf '  userData:\n  assetBundleName:\n  assetBundleVariant:\n' ;;
        *)
          printf 'DefaultImporter:\n'; tail_block ;;
      esac
    fi
  } > "$target"
  echo "meta: $target"
}

# Root assets Unity imports from a package.
for asset in package.json README.md API.md CHANGELOG.md MIGRATING-FROM-ONESIGNAL.md \
             LICENSE NOTICE SECURITY.md CONTRIBUTING.md CODE_OF_CONDUCT.md; do
  [ -e "$asset" ] && write_meta "$asset"
done

# Package trees, folders first. Unity imports scripts/ even though its contents
# are repository tooling, so those assets need committed metadata too.
for tree in Runtime Editor scripts release-notes; do
  [ -d "$tree" ] || continue
  # Paths under an .androidlib are skipped: the folder itself is the asset, and
  # metas inside it would be orphans Unity ignores.
  find "$tree" -type d ! -path '*.androidlib/*' ! -path '*~' ! -path '*~/*' | sort |
    while read -r dir; do write_meta "$dir"; done
  find "$tree" -type f ! -name '*.meta' ! -name '.DS_Store' ! -path '*.androidlib/*' ! -path '*~/*' | sort |
    while read -r file; do write_meta "$file"; done
done
