# Fish Speech TTS service
FROM python:3.10-slim
WORKDIR /app
# Install dependencies
COPY requirements-base.txt ./requirements.txt
RUN pip install --no-cache-dir -r requirements.txt
# Copy TTS sources
COPY speech/TTS/openaudio-s1-mini/ .
EXPOSE 8081
CMD ["python", "fish_speech_api.py", "--listen", "0.0.0.0:8081"]
