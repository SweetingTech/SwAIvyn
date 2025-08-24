#!/bin/sh
set -e

wait_for_url() {
  url="$1"
  name="$2"
  echo "Waiting for $name at $url..."
  for i in $(seq 1 30); do
    if curl -fs "$url" > /dev/null; then
      echo "$name is ready"
      return 0
    fi
    echo "$name not ready, retrying..."
    sleep 2
  done
  echo "Timeout waiting for $name"
}

wait_for_url "http://neo4j:7474" "Neo4j"
wait_for_url "http://weaviate:8080/v1/meta" "Weaviate"
wait_for_url "http://tts:8081/health" "TTS"
wait_for_url "http://stt:9000/docs" "STT"

echo "Starting SwAIvyn backend..."
exec dotnet SwAIvyn.dll
 
