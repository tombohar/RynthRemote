#!/usr/bin/env bash
# Publishes a built .ipa to THIS repo's GitHub Releases and regenerates apps.json
# (the SideStore source) in the repo root. Uses the workflow's built-in GITHUB_TOKEN
# (GH_TOKEN) — no personal access token / secret needed. Repo must be PUBLIC so
# SideStore can fetch the feed + asset without auth.
#   usage: publish-release.sh <version> <path-to-ipa>
set -euo pipefail

VERSION="$1"
IPA="$2"
REPO="${GITHUB_REPOSITORY}"      # e.g. tombohar/RynthRemote
ASSET="RynthRemote.ipa"

cp "$IPA" "$ASSET"
SIZE=$(stat -f%z "$ASSET" 2>/dev/null || stat -c%s "$ASSET")
DATE=$(date +%Y-%m-%d)
URL="https://github.com/${REPO}/releases/download/v${VERSION}/${ASSET}"

# Create the release on this repo (or replace the asset if the tag already exists).
gh release create "v${VERSION}" "$ASSET" --repo "$REPO" \
    --title "RynthRemote ${VERSION}" --notes "Automated build ${VERSION}." \
  || gh release upload "v${VERSION}" "$ASSET" --repo "$REPO" --clobber

# Regenerate the SideStore source feed (committed to the repo root; raw URL is the source).
cat > apps.json <<JSON
{
  "name": "RynthRemote",
  "identifier": "com.tombohar.rynthremote.source",
  "apps": [
    {
      "name": "RynthRemote",
      "bundleIdentifier": "com.tombohar.rynthremote",
      "developerName": "Tom Bohar",
      "subtitle": "AC multibox monitor + remote",
      "localizedDescription": "Monitor and remote-control your Asheron's Call multi-boxes from your phone, via the RynthCore StatusAgent on your PC. Live health, kills/XP, components, equipped gear with appraisals, and one-tap toggles (nav/combat/buffing), profile switching, and utilities.",
      "iconURL": "https://raw.githubusercontent.com/${REPO}/main/icon.png",
      "tintColor": "6366f1",
      "category": "utilities",
      "version": "${VERSION}",
      "versionDate": "${DATE}",
      "versionDescription": "Automated build ${VERSION}.",
      "downloadURL": "${URL}",
      "size": ${SIZE},
      "versions": [
        { "version": "${VERSION}", "date": "${DATE}", "localizedDescription": "Automated build ${VERSION}.", "downloadURL": "${URL}", "size": ${SIZE}, "minOSVersion": "15.0" }
      ]
    }
  ],
  "news": []
}
JSON

# Commit the feed back to this repo's main. Path filters + [skip ci] prevent a build loop.
git config user.name "RynthRemote CI"
git config user.email "ci@users.noreply.github.com"
git add apps.json
git commit -m "Update source feed to ${VERSION} [skip ci]" || echo "no change to commit"
git push
echo "Published v${VERSION} (${SIZE} bytes) to ${REPO}."
