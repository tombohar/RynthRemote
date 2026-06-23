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

# Regenerate the SideStore source feed, PRESERVING the existing version history so EVERY build stays
# visible + installable in SideStore. The feed ACCUMULATES: prepend the new version (newest first), drop
# any prior entry for the same version, and keep all the rest — instead of overwriting with only the
# latest (which hid every prior build). Committed to the repo root; the raw URL is the SideStore source.
python3 - "$VERSION" "$DATE" "$URL" "$SIZE" "$REPO" <<'PY'
import json, os, sys
version, date, url, size, repo = sys.argv[1], sys.argv[2], sys.argv[3], int(sys.argv[4]), sys.argv[5]
entry = {"version": version, "date": date, "localizedDescription": "Automated build %s." % version,
         "downloadURL": url, "size": size, "minOSVersion": "15.0"}
app = {
    "name": "RynthRemote",
    "bundleIdentifier": "com.tombohar.rynthremote",
    "developerName": "Tom Bohar",
    "subtitle": "AC multibox monitor + remote",
    "localizedDescription": "Monitor and remote-control your Asheron's Call multi-boxes from your phone, via the RynthCore StatusAgent on your PC. Live health, kills/XP, components, equipped gear with appraisals, and one-tap toggles (nav/combat/buffing), profile switching, and utilities.",
    "iconURL": "https://raw.githubusercontent.com/%s/main/icon.png" % repo,
    "tintColor": "6366f1",
    "category": "utilities",
}
versions = []
if os.path.exists("apps.json"):
    try:
        prev = json.load(open("apps.json"))["apps"][0]
        versions = [v for v in prev.get("versions", []) if v.get("version") != version]
        for k in ("name", "bundleIdentifier", "developerName", "subtitle",
                  "localizedDescription", "iconURL", "tintColor", "category"):
            if k in prev:
                app[k] = prev[k]   # keep any hand-edited metadata across rebuilds
    except Exception:
        versions = []
versions = [entry] + versions          # newest first (SideStore installs versions[0])
app["versions"] = versions
app["version"] = version               # legacy top-level fields mirror the newest
app["versionDate"] = date
app["versionDescription"] = entry["localizedDescription"]
app["downloadURL"] = url
app["size"] = size
doc = {"name": "RynthRemote", "identifier": "com.tombohar.rynthremote.source", "apps": [app], "news": []}
json.dump(doc, open("apps.json", "w"), indent=2)
print("feed now lists %d version(s); newest %s" % (len(versions), version))
PY

# Commit the feed back to this repo's main. Path filters + [skip ci] prevent a build loop.
git config user.name "RynthRemote CI"
git config user.email "ci@users.noreply.github.com"
git add apps.json
git commit -m "Update source feed to ${VERSION} [skip ci]" || echo "no change to commit"
git push
echo "Published v${VERSION} (${SIZE} bytes) to ${REPO}."
