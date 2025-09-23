# GPU-Accelerated TTS Setup Guide for SwAIvyn

## Overview
This guide provides step-by-step instructions for setting up GPU-accelerated Fish Speech TTS service in a standalone Docker container, bypassing Docker Swarm limitations while maintaining integration with the SwAIvyn application stack.

## Prerequisites
- Windows 11 with NVIDIA RTX GPU (3090 or similar)
- Latest NVIDIA drivers installed
- Docker Desktop with NVIDIA runtime support
- SwAIvyn project cloned to `D:/project/SwAIvyn`

## Step 1: Verify GPU Support in Docker

First, confirm Docker can access your GPU:

```powershell
# Test basic GPU access
docker run --rm --gpus all nvidia/cuda:12.0-base-ubuntu20.04 nvidia-smi
```

If this fails, your Docker Desktop doesn't have proper GPU support configured.

## Step 2: Stop Existing Fish Speech Service in Swarm

```powershell
# Navigate to project directory
cd D:/project/SwAIvyn

# Remove the fishspeech service from the stack (keep others running)
docker service rm swaivyn_fishspeech-runtime
```

## Step 3: Create GPU-Enabled Fish Speech Container

Run the Fish Speech service as a standalone container with GPU access:

```powershell
docker run -d \
  --name fishspeech-runtime-gpu \
  --gpus all \
  --restart unless-stopped \
  --network swaivyn_default \
  -p 8000:8000 \
  -e NVIDIA_VISIBLE_DEVICES=all \
  -e NVIDIA_DRIVER_CAPABILITIES=compute,utility \
  -v "D:/project/SwAIvyn/speech/TTS/openaudio-s1-mini/fish_speech_model:/opt/fish-speech/checkpoints/openaudio-s1-mini:ro" \
  swai/fish-speech:cuda \
  python tools/api_server.py --listen 0.0.0.0:8000 --device cuda --half
```

### Command Breakdown:
- `--name fishspeech-runtime-gpu`: Container name for easy management
- `--gpus all`: Grants access to all GPUs
- `--restart unless-stopped`: Auto-restart container on boot
- `--network swaivyn_default`: Join the existing Swarm network
- `-p 8000:8000`: Expose port 8000 for API access
- Environment variables: Configure NVIDIA GPU access
- Volume mount: Mount model files from host
- `--device cuda --half`: Use GPU with half precision

## Step 4: Verify GPU Detection

Check if the container can see your GPU:

```powershell
# Check container logs for GPU detection
docker logs fishspeech-runtime-gpu

# Should see:
# - No "NVIDIA Driver was not detected" warnings
# - "CUDA is available" messages
# - Much faster model loading times
```

## Step 5: Test TTS API Directly

Verify the GPU-accelerated service works:

```powershell
# Test TTS endpoint directly
curl -X POST "http://localhost:8000/v1/tts" \
  -H "Content-Type: application/json" \
  -d '{"text": "Hello world, this is GPU-accelerated speech", "voice": "default"}' \
  --output test_gpu_tts.wav
```

## Step 6: Update TTS Proxy Configuration

The existing TTS proxy service should automatically connect to the new container since it's on the same network and using the same hostname (`fishspeech-runtime`). However, if needed, you can update the proxy:

```powershell
# Check if TTS proxy can reach the new service
docker exec swaivyn_tts.1.$(docker service ps -q swaivyn_tts) \
  curl -f http://fishspeech-runtime-gpu:8000/health
```

## Step 7: Alternative - Update TTS Proxy Environment

If the TTS proxy can't find the service, update its upstream URL:

```powershell
# Update the TTS service environment variable
docker service update \
  --env-rm UPSTREAM_TTS \
  --env-add UPSTREAM_TTS=http://fishspeech-runtime-gpu:8000 \
  swaivyn_tts
```

## Step 8: Integration Verification

Test the full pipeline through your SwAIvyn application:

```powershell
# Test through the TTS proxy
curl -X POST "http://localhost:8081/tts" \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "text=Testing GPU acceleration through SwAIvyn" \
  --output test_swaivyn_gpu_tts.wav

# Check response time - should be much faster with GPU
```

## Step 9: Monitor GPU Usage

While testing, monitor GPU utilization:

```powershell
# In a separate terminal, monitor GPU usage
nvidia-smi -l 1
```

You should see GPU utilization spike during TTS generation.

## Troubleshooting

### Issue: "NVIDIA Driver was not detected"
**Solution**: Ensure Docker Desktop has GPU support enabled:
1. Open Docker Desktop Settings
2. Go to Resources → WSL Integration
3. Enable integration with your WSL distro
4. Restart Docker Desktop

### Issue: Container can't join Swarm network
**Solution**: Create the network manually if it doesn't exist:
```powershell
docker network create --driver overlay --attachable swaivyn_default
```

### Issue: Model files not found
**Solution**: Verify the model path exists:
```powershell
ls "D:/project/SwAIvyn/speech/TTS/openaudio-s1-mini/fish_speech_model"
```

### Issue: Port 8000 already in use
**Solution**: The old Swarm service might still be running:
```powershell
docker service ls | grep fishspeech
docker service rm swaivyn_fishspeech-runtime
```

## Container Management Commands

### Start/Stop the GPU TTS container:
```powershell
# Stop
docker stop fishspeech-runtime-gpu

# Start
docker start fishspeech-runtime-gpu

# View logs
docker logs -f fishspeech-runtime-gpu

# Remove (if needed)
docker rm -f fishspeech-runtime-gpu
```

### Performance Monitoring:
```powershell
# Check container resource usage
docker stats fishspeech-runtime-gpu

# Check container info
docker inspect fishspeech-runtime-gpu
```

## Expected Performance Improvements

With GPU acceleration, you should see:
- **Model Loading**: 10-30 seconds (vs 2-5 minutes on CPU)
- **TTS Generation**: 3-10 tokens/sec (vs 0.3 tokens/sec on CPU)
- **Response Time**: 5-15 seconds for typical phrases (vs 55-129 seconds on CPU)
- **GPU Utilization**: 30-80% during generation
- **VRAM Usage**: 2-8GB depending on model size

## Integration with SwAIvyn Services

The GPU-enabled Fish Speech container integrates seamlessly with your existing SwAIvyn stack:

1. **TTS Proxy Service** (`swaivyn_tts`) automatically forwards requests to the GPU container
2. **Frontend Application** continues to use `/tts` endpoints normally
3. **Voice Management** works unchanged with the existing voices directory
4. **Health Checks** should show improved response times

## Maintenance

### Daily:
- Monitor GPU memory usage with `nvidia-smi`
- Check container logs for any errors

### Weekly:
- Restart the container if memory usage grows excessively:
  ```powershell
  docker restart fishspeech-runtime-gpu
  ```

### As Needed:
- Update the Fish Speech image:
  ```powershell
  docker pull swai/fish-speech:cuda
  docker stop fishspeech-runtime-gpu
  docker rm fishspeech-runtime-gpu
  # Re-run the container creation command from Step 3
  ```

## Conclusion

This setup provides GPU acceleration for your TTS service while maintaining full compatibility with the existing SwAIvyn application stack. The GPU-accelerated container runs independently but integrates seamlessly with your Swarm-based services through Docker networking.
