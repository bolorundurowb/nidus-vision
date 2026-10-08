#!/usr/bin/env bash
# Stamps a pushed tag's version into the API and UI version files: <Version> in
# Directory.Build.props (what the Settings page reports) and the Angular
# package.json / package-lock.json. Run it from the repository root.
#
# Usage: .github/scripts/stamp-version.sh <tag>
#
# A leading "v" is stripped. Tags that are not semantic versions are skipped
# with a warning so non-release tags keep the checked-in versions.

set -euo pipefail

tag="${1:?usage: stamp-version.sh <tag>}"
version="${tag#v}"

if [[ ! "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+(-[0-9A-Za-z]+(\.[0-9A-Za-z]+)*)?(\+[0-9A-Za-z.-]+)?$ ]]; then
  echo "::warning::Tag '${tag}' is not a semantic version; keeping the checked-in versions."
  exit 0
fi

sed -i "s|<Version>.*</Version>|<Version>${version}</Version>|" Directory.Build.props
grep -q "<Version>${version}</Version>" Directory.Build.props

(
  cd src/NidusVision.Client
  npm version "${version}" --no-git-tag-version --allow-same-version
)

echo "Stamped ${version} (from ${tag}) into Directory.Build.props and the client package files."
