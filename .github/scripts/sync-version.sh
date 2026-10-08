#!/usr/bin/env bash
# Pushes a "chore: bump version" commit to main so the checked-in API and UI
# versions track the latest release tag. Runs in the Publish workflow's
# sync-versions job, which checks out main rather than the tag. Run it from
# the repository root.
#
# Usage: .github/scripts/sync-version.sh <tag>
#
# Skips when the checked-in version already matches or is newer than the tag,
# or when the tag is not a semantic version (stamp-version.sh is a no-op then).
# If the push races with another change on main, rebases and retries, up to
# three attempts.

set -euo pipefail

tag="${1:?usage: sync-version.sh <tag>}"
version="${tag#v}"

current="$(sed -n 's|^[[:space:]]*<Version>\(.*\)</Version>[[:space:]]*$|\1|p' Directory.Build.props | head -n 1)"
if [[ -z "$current" ]]; then
  echo "::error::No <Version> property in Directory.Build.props."
  exit 1
fi
if [[ "$current" == "$version" ]]; then
  echo "Checked-in version ${current} already matches ${tag}; nothing to sync."
  exit 0
fi
if [[ "$(printf '%s\n' "$current" "$version" | sort -V | tail -n 1)" != "$version" ]]; then
  echo "::warning::Checked-in version ${current} is newer than ${tag}; nothing to sync."
  exit 0
fi

bash .github/scripts/stamp-version.sh "$tag"

git config user.name "github-actions[bot]"
git config user.email "41898282+github-actions[bot]@users.noreply.github.com"
git add Directory.Build.props src/NidusVision.Client/package.json src/NidusVision.Client/package-lock.json
if git diff --cached --quiet; then
  echo "Nothing to commit for ${tag}; keeping the checked-in versions."
  exit 0
fi
git commit -m "chore: bump version to ${version}"

for attempt in 1 2 3; do
  if git push origin HEAD:main; then
    echo "Pushed the version bump to main."
    exit 0
  fi
  echo "Push failed (attempt ${attempt} of 3); rebasing on the latest main."
  git fetch origin main
  if ! git rebase origin/main; then
    echo "::error::Rebase conflict while syncing the version bump; resolve it manually."
    exit 1
  fi
done

echo "::error::Could not push the version bump to main after 3 attempts."
exit 1
