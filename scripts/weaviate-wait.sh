#!/bin/sh
set -e

# Simple readiness wrapper for Weaviate remote modules.
# Waits for each module's /.well-known/ready endpoint before launching Weaviate.

WAIT_URLS="\
http://multi2vec-clip:8080/.well-known/ready clip\n\
http://qna-transformers:8080/.well-known/ready qna\n\
http://sum-transformers:8080/.well-known/ready sum\n\
http://text-spellcheck:8080/.well-known/ready spellcheck\n\
http://reranker-transformers:8080/.well-known/ready reranker\n"

RETRIES=${RETRIES:-60}
SLEEP=${SLEEP:-3}

check_url() {
  url="$1"; name="$2"
  i=1
  while [ $i -le $RETRIES ]; do
    if wget -qO- "$url" >/dev/null 2>&1 || curl -fsS "$url" >/dev/null 2>&1; then
      echo "[$(date -u +%H:%M:%S)] $name ready"
      return 0
    fi
    echo "[$(date -u +%H:%M:%S)] waiting for $name ($i/$RETRIES) ..."
    i=$((i+1))
    sleep "$SLEEP"
  done
  echo "[$(date -u +%H:%M:%S)] $name did not become ready in time" >&2
  exit 1
}

# Iterate pairs: url name
# shellcheck disable=SC2034
printf "%b" "$WAIT_URLS" | while read -r url name; do
  [ -z "$url" ] && continue
  check_url "$url" "$name"
done

# Launch Weaviate with default entrypoint/command if available
# Most official images have /weaviate binary on PATH.
exec weaviate

