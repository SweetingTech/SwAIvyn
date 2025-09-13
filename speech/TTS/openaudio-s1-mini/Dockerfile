FROM python:3.11-slim

WORKDIR /app
# When compose build context is repo root, copy using full relative paths
COPY speech/TTS/openaudio-s1-mini/requirements.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Voices will be mounted here at runtime
RUN mkdir -p /app/voices

COPY speech/TTS/openaudio-s1-mini/proxy_server.py /app/proxy_server.py

ENV UPSTREAM_TTS=http://host.docker.internal:8081 \
    VOICES_DIR=/app/voices

EXPOSE 8081
CMD ["uvicorn", "proxy_server:app", "--host", "0.0.0.0", "--port", "8081"]
