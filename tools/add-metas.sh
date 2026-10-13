#!/usr/bin/env bash
# Creates missing Unity .meta files (with fresh random GUIDs) for folders and .cs
# files under Assets/Relic, so code written outside the editor gets stable GUIDs
# that are committed with it. Without this, every worktree that opens Unity would
# invent different GUIDs for the same file and cause merge conflicts.
#
# Usage (from repo root):  bash tools/add-metas.sh
# Other asset types (scenes, materials, shaders, asmdefs) should be created or
# imported by Unity itself, which writes their .meta files.

set -euo pipefail
root="${1:-Assets/Relic}"

guid() { od -An -tx1 -N16 /dev/urandom | tr -d ' \n'; }

created=0
while IFS= read -r -d '' path; do
  meta="$path.meta"
  [ -e "$meta" ] && continue
  if [ -d "$path" ]; then
    printf 'fileFormatVersion: 2\nguid: %s\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(guid)" > "$meta"
  else
    case "$path" in
      *.cs)
        printf 'fileFormatVersion: 2\nguid: %s\nMonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(guid)" > "$meta" ;;
      *.asmdef)
        printf 'fileFormatVersion: 2\nguid: %s\nAssemblyDefinitionImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(guid)" > "$meta" ;;
      *.md|*.txt|*.json)
        printf 'fileFormatVersion: 2\nguid: %s\nTextScriptImporter:\n  externalObjects: {}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n' "$(guid)" > "$meta" ;;
      *) continue ;;
    esac
  fi
  echo "created $meta"
  created=$((created + 1))
done < <(find "$root" \( -type d -o -type f \) ! -name '*.meta' -print0)

echo "$created meta file(s) created"
