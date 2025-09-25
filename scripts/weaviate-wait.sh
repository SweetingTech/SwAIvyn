#!/bin/sh
set -euo pipefail

TARGET_URL="${1:-http://weaviate:8080/v1/.well-known/live}"
MAX_ATTEMPTS="${MAX_ATTEMPTS:-60}"
SLEEP_SECONDS="${SLEEP_SECONDS:-5}"

printf 'Waiting for Weaviate at %s...\n' "$TARGET_URL"

if command -v curl >/dev/null 2>&1; then
    http_cmd() {
        curl -fsS --max-time 2 "$1" >/dev/null
    }
elif command -v wget >/dev/null 2>&1; then
    http_cmd() {
        wget -qO- "$1" >/dev/null
    }
else
    echo "Neither curl nor wget is available for health checks." >&2
    exit 1
fi

attempt=1
while [ "$attempt" -le "$MAX_ATTEMPTS" ]; do
    if http_cmd "$TARGET_URL"; then
        printf 'Weaviate is ready after %s attempt(s).\n' "$attempt"
        exit 0
    fi
    printf 'Attempt %s/%s failed; retrying in %ss...\n' "$attempt" "$MAX_ATTEMPTS" "$SLEEP_SECONDS"
    sleep "$SLEEP_SECONDS"
    attempt=$((attempt + 1))
done

echo "Timed out waiting for Weaviate at ${TARGET_URL}." >&2
exit 1
