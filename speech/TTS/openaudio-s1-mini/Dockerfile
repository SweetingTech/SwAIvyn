# Fish Speech TTS service
FROM python:3.10-slim
WORKDIR /app

# Install dependencies
COPY requirements-base.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt

# Create and switch to non-root user
RUN useradd --system --create-home appuser

# Copy TTS sources and set ownership
COPY speech/TTS/openaudio-s1-mini/ ./
RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8081
CMD ["python", "fish_speech_api.py", "--listen", "0.0.0.0:8081"]
