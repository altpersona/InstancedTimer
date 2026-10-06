#!/bin/sh
# Publish the packaged Thunderstore zip via the Thunderstore API (no web UI).
#
# Auth: service-account token (Bearer) from ~/.config/thunderstore/api_token
# (generate under thunderstore.io -> team -> Service Accounts).
#
# Uses the legacy single-call endpoint POST /api/experimental/submission/upload/
# (multipart: file + metadata) which publishes immediately - no separate
# submit step. Marked deprecated upstream but functional; if it ever goes
# away, migrate to the usermedia initiate-upload -> submit/ flow.
#
# Refuses to overwrite: Thunderstore rejects re-uploading an existing version,
# so a version can only be published once (bump versions first).
set -e
cd "$(dirname "$0")"

TOKEN_FILE="${TS_TOKEN_FILE:-$HOME/.config/thunderstore/api_token}"
TEAM="StandardVibeware"
COMMUNITY="valheim"
CATEGORIES="client-side"
API="https://thunderstore.io/api/experimental/submission/upload/"

if ! [ -f "$TOKEN_FILE" ]; then
    echo "token missing: $TOKEN_FILE (create it with a service-account token)" >&2
    exit 1
fi
TOKEN=$(cat "$TOKEN_FILE")

VERSION=$(grep -o '"version_number": *"[^"]*"' InstancedTimer/manifest.json |
    head -1 | sed 's/.*: *"//;s/"$//')
ZIP="InstancedTimer-v${VERSION}.zip"
if ! [ -f "$ZIP" ]; then
    echo "package missing: $ZIP (run ./package.sh first)" >&2
    exit 1
fi

METADATA=$(printf '{"author_name":"%s","communities":["%s"],"has_nsfw_content":false,"categories":["%s"]}' \
    "$TEAM" "$COMMUNITY" "$CATEGORIES")

echo "publishing $ZIP as $TEAM-InstancedTimer-$VERSION ($COMMUNITY, $CATEGORIES)..."
# Cloudflare 502s this POST with curl's default User-Agent and also
# mishandles Expect: 100-continue - both headers below are required.
RESPONSE=$(curl -sS -w '\n%{http_code}' -X POST "$API" \
    -H "Authorization: Bearer $TOKEN" \
    -H 'User-Agent: tcli/1.0' \
    -H 'Expect:' \
    -F "file=@$ZIP" \
    -F "metadata=$METADATA")

HTTP_CODE=$(printf '%s' "$RESPONSE" | tail -1)
BODY=$(printf '%s' "$RESPONSE" | sed '$d')

# Documented as 201; the live endpoint returns 200 on success.
if [ "$HTTP_CODE" != "200" ] && [ "$HTTP_CODE" != "201" ]; then
    echo "publish FAILED (HTTP $HTTP_CODE):" >&2
    printf '%s\n' "$BODY" >&2
    exit 1
fi

echo "published:"
printf '%s\n' "$BODY"
