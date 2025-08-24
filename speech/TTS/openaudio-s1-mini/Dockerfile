# Fish Speech TTS service
FROM python:3.10-slim
WORKDIR /app

# Optional build args for downloading large model weights at build time.
# If MODEL_URL is provided as a build-arg, the image will download it into /app/models.
ARG MODEL_URL=""
ARG MODEL_DEST="/app/models"

# Install Python dependencies
COPY requirements-base.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# If a MODEL_URL is provided we download it during build; otherwise skip.
# This keeps model weights out of the repository and the build context small.
RUN if [ -n "${MODEL_URL}" ]; then \
			apt-get update && DEBIAN_FRONTEND=noninteractive apt-get install -y --no-install-recommends curl ca-certificates && \
			mkdir -p ${MODEL_DEST} && \
			curl -fSL "${MODEL_URL}" -o ${MODEL_DEST}/model.pth && \
			rm -rf /var/lib/apt/lists/*; \
		fi

# Create and switch to non-root user
RUN useradd --system --create-home appuser

# Copy TTS sources and set ownership (large files are excluded by .dockerignore)
COPY speech/TTS/openaudio-s1-mini/ ./
RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8081
CMD ["python", "fish_speech_api.py", "--listen", "0.0.0.0:8081"]
