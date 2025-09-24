# GPU-Accelerated TTS Setup Script for SwAIvyn
# This script automates the setup of GPU-accelerated Fish Speech TTS service

param(
    [switch]$SkipGPUTest = $false,
    [switch]$Force = $false,
    [string]$ModelPath = "D:/project/SwAIvyn/speech/TTS/openaudio-s1-mini/fish_speech_model"
)

Write-Host "=== SwAIvyn GPU TTS Setup Script ===" -ForegroundColor Cyan
Write-Host "Setting up GPU-accelerated Fish Speech TTS service..." -ForegroundColor Green

# Step 1: Verify GPU support in Docker (unless skipped)
if (-not $SkipGPUTest) {
    Write-Host "`n[Step 1] Testing GPU access in Docker..." -ForegroundColor Yellow
    
    try {
        $gpuTest = docker run --rm --gpus all nvidia/cuda:12.0-base-ubuntu20.04 nvidia-smi 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "✓ GPU access confirmed in Docker" -ForegroundColor Green
        } else {
            Write-Host "✗ GPU test failed. Docker may not have NVIDIA runtime support." -ForegroundColor Red
            Write-Host "Error: $gpuTest" -ForegroundColor Red
            if (-not $Force) {
                Write-Host "Use -Force to continue anyway, or configure Docker Desktop GPU support first." -ForegroundColor Yellow
                exit 1
            }
        }
    } catch {
        Write-Host "✗ Could not test GPU access: $_" -ForegroundColor Red
        if (-not $Force) {
            Write-Host "Use -Force to continue anyway." -ForegroundColor Yellow
            exit 1
        }
    }
} else {
    Write-Host "[Step 1] Skipping GPU test..." -ForegroundColor Yellow
}

# Step 2: Check if model path exists
Write-Host "`n[Step 2] Verifying model files..." -ForegroundColor Yellow
if (Test-Path $ModelPath) {
    Write-Host "✓ Model path exists: $ModelPath" -ForegroundColor Green
} else {
    Write-Host "✗ Model path not found: $ModelPath" -ForegroundColor Red
    Write-Host "Please ensure the Fish Speech model is downloaded and extracted to the correct location." -ForegroundColor Yellow
    if (-not $Force) {
        exit 1
    }
}

# Step 3: Check if existing container exists
Write-Host "`n[Step 3] Checking for existing GPU TTS container..." -ForegroundColor Yellow
$existingContainer = docker ps -aq -f name=fishspeech-runtime-gpu
if ($existingContainer) {
    Write-Host "Found existing container. Removing..." -ForegroundColor Yellow
    docker stop fishspeech-runtime-gpu 2>$null
    docker rm fishspeech-runtime-gpu 2>$null
    Write-Host "✓ Existing container removed" -ForegroundColor Green
}

# Step 4: Stop existing Swarm service
Write-Host "`n[Step 4] Removing Fish Speech service from Swarm stack..." -ForegroundColor Yellow
try {
    $swarmService = docker service ls --format "{{.Name}}" | Where-Object { $_ -match "fishspeech-runtime" }
    if ($swarmService) {
        docker service rm $swarmService
        Write-Host "✓ Removed Swarm service: $swarmService" -ForegroundColor Green
        Start-Sleep -Seconds 5  # Wait for service to fully stop
    } else {
        Write-Host "No Fish Speech Swarm service found" -ForegroundColor Yellow
    }
} catch {
    Write-Host "Could not check/remove Swarm service: $_" -ForegroundColor Yellow
}

# Step 5: Ensure Swarm network exists
Write-Host "`n[Step 5] Ensuring Swarm network exists..." -ForegroundColor Yellow
$networkExists = docker network ls --format "{{.Name}}" | Where-Object { $_ -eq "swaivyn_default" }
if (-not $networkExists) {
    Write-Host "Creating swaivyn_default network..." -ForegroundColor Yellow
    docker network create --driver overlay --attachable swaivyn_default
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Created swaivyn_default network" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to create network. Continuing anyway..." -ForegroundColor Red
    }
} else {
    Write-Host "✓ swaivyn_default network exists" -ForegroundColor Green
}

# Step 6: Create GPU-enabled Fish Speech container
Write-Host "`n[Step 6] Creating GPU-enabled Fish Speech container..." -ForegroundColor Yellow

$dockerCommand = @(
    "docker", "run", "-d",
    "--name", "fishspeech-runtime-gpu",
    "--gpus", "all",
    "--restart", "unless-stopped",
    "--network", "swaivyn_default",
    "-p", "8000:8000",
    "-e", "NVIDIA_VISIBLE_DEVICES=all",
    "-e", "NVIDIA_DRIVER_CAPABILITIES=compute,utility",
    "-v", "${ModelPath}:/opt/fish-speech/checkpoints/openaudio-s1-mini:ro",
    "swai/fish-speech:cuda",
    "python", "tools/api_server.py", "--listen", "0.0.0.0:8000", "--device", "cuda", "--half"
)

Write-Host "Running command: $($dockerCommand -join ' ')" -ForegroundColor Gray

try {
    & $dockerCommand[0] $dockerCommand[1..($dockerCommand.Length-1)]
} catch {
    Write-Host "✗ Error creating container: $_" -ForegroundColor Red
    exit 1
}

if ($LASTEXITCODE -eq 0) {
    Write-Host "✓ GPU TTS container created successfully" -ForegroundColor Green

    # Wait for container to start
    Write-Host "Waiting for container to initialize..." -ForegroundColor Yellow
    Start-Sleep -Seconds 10

    # Check container status
    $containerStatus = docker ps --format "{{.Status}}" -f name=fishspeech-runtime-gpu
    if ($containerStatus -match "Up") {
        Write-Host "✓ Container is running: $containerStatus" -ForegroundColor Green
    } else {
        Write-Host "✗ Container may have issues. Status: $containerStatus" -ForegroundColor Red
    }
} else {
    Write-Host "✗ Failed to create container" -ForegroundColor Red
    exit 1
}

# Step 7: Check container logs for GPU detection
Write-Host "`n[Step 7] Checking container logs for GPU detection..." -ForegroundColor Yellow
Start-Sleep -Seconds 5
$logs = docker logs fishspeech-runtime-gpu --tail 20 2>&1

$cudaAvailable = $logs | Select-String "CUDA is.*available" | Select-Object -First 1
$nvidiaWarning = $logs | Select-String "NVIDIA Driver was not detected"

if ($nvidiaWarning) {
    Write-Host "✗ WARNING: NVIDIA Driver not detected in container" -ForegroundColor Red
    Write-Host "The container is running on CPU. Check Docker Desktop GPU configuration." -ForegroundColor Yellow
} elseif ($cudaAvailable) {
    Write-Host "✓ CUDA detected in container!" -ForegroundColor Green
    Write-Host "  $($cudaAvailable.Line.Trim())" -ForegroundColor Gray
} else {
    Write-Host "? GPU detection status unclear. Container may still be initializing..." -ForegroundColor Yellow
}

# Step 8: Update TTS proxy service to point to new container
Write-Host "`n[Step 8] Updating TTS proxy service..." -ForegroundColor Yellow
$ttsService = docker service ls --format "{{.Name}}" | Where-Object { $_ -match "tts" -and $_ -notmatch "stt" }
if ($ttsService) {
    Write-Host "Found TTS service: $ttsService" -ForegroundColor Gray
    docker service update --env-rm UPSTREAM_TTS --env-add UPSTREAM_TTS=http://fishspeech-runtime-gpu:8000 $ttsService
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✓ Updated TTS proxy to use GPU container" -ForegroundColor Green
    } else {
        Write-Host "✗ Failed to update TTS proxy. You may need to update manually." -ForegroundColor Yellow
    }
} else {
    Write-Host "No TTS proxy service found in Swarm" -ForegroundColor Yellow
}

# Step 9: Display setup summary
Write-Host "`n=== Setup Complete ===" -ForegroundColor Cyan
Write-Host "GPU-accelerated Fish Speech TTS container is now running!" -ForegroundColor Green
Write-Host ""
Write-Host "Container Details:" -ForegroundColor White
Write-Host "  Name: fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host "  API Endpoint: http://localhost:8000" -ForegroundColor Gray
Write-Host "  Network: swaivyn_default" -ForegroundColor Gray
Write-Host "  Model Path: $ModelPath" -ForegroundColor Gray
Write-Host ""
Write-Host "Management Commands:" -ForegroundColor White
Write-Host "  View logs:    docker logs -f fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host "  Check status: docker ps -f name=fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host "  Stop:         docker stop fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host "  Start:        docker start fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host "  Remove:       docker rm -f fishspeech-runtime-gpu" -ForegroundColor Gray
Write-Host ""
Write-Host "Testing Commands:" -ForegroundColor White
Write-Host "  Monitor GPU:  nvidia-smi -l 1" -ForegroundColor Gray
Write-Host "  Test API:     curl -X POST http://localhost:8000/v1/health" -ForegroundColor Gray
Write-Host "  Test TTS:     curl -X POST http://localhost:8081/tts -d 'text=Hello GPU world'" -ForegroundColor Gray
Write-Host ""

if ($nvidiaWarning) {
    Write-Host "⚠️  IMPORTANT: GPU was not detected in the container." -ForegroundColor Yellow
    Write-Host "   The service will run on CPU, which is much slower." -ForegroundColor Yellow
    Write-Host "   Please check Docker Desktop GPU configuration and try again." -ForegroundColor Yellow
} else {
    Write-Host "🚀 GPU acceleration should now be active!" -ForegroundColor Green
    Write-Host "   Expected performance: 5-10x faster TTS generation" -ForegroundColor Green
    Write-Host "   Monitor GPU usage with: nvidia-smi -l 1" -ForegroundColor Green
}

Write-Host "`nSetup script completed successfully!" -ForegroundColor Cyan
