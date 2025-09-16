# Fish Speech TTS Integration Guide

This guide will help you set up and test the Fish Speech TTS integration in SwAIvyn.

## Prerequisites

1. **Fish Speech Setup**: Make sure you have Fish Speech installed and configured
2. **Model Files**: Ensure you have the required model files in the `speech/TTS/openaudio-s1-mini/` directory:
   - `model.pth`
   - `codec.pth`
   - `config.json`
   - `tokenizer.tiktoken`
   - `special_tokens.json`

## Quick Start

### 1. Start Fish Speech API Server

**Option A: Using the provided script (Windows)**
```powershell
.\start-fish-speech.ps1
```

**Option B: Manual start**
```bash
cd speech/TTS/openaudio-s1-mini
python fish_speech_api.py
```

### 2. Start SwAIvyn Backend

```powershell
cd backend
dotnet run
```

### 3. Test the Integration

Run the integration test script:
```bash
python test_fish_speech_integration.py
```

## API Endpoints

### Fish Speech Direct API (Port 8000)

- `GET /voices` - List available voices
- `POST /tts` - Default TTS synthesis
- `POST /tts/clone` - Voice cloning synthesis
- `POST /tts/voicevec` - Synthesis with saved embeddings

### SwAIvyn Backend API (Port 5000)

- `GET /api/tts/voices` - Get all TTS providers and their voices
- `POST /api/tts/synthesize` - Synthesize speech with selected provider

## Voice Management

### Adding Custom Voices

1. Create a `voices/` directory in `speech/TTS/openaudio-s1-mini/`
2. Add voice files in pairs:
   - `voice_name.wav` - Reference audio (3-10 seconds recommended)
   - `voice_name.txt` - Transcript of the reference audio

Example:
```
voices/
├── john_doe.wav
├── john_doe.txt
├── jane_smith.wav
└── jane_smith.txt
```

### Pre-generating Voice Embeddings

To improve performance, you can pre-generate speaker embeddings:

```bash
curl -X POST "http://localhost:8000/voices/save" -F "voice_name=john_doe"
```

This creates a `john_doe.pt` file containing the speaker embedding.

## Frontend Integration

The frontend automatically detects available TTS providers. When Fish Speech is running:

1. Go to Settings → Voice Settings
2. Select "FishSpeech" as the TTS Provider
3. Choose from available voices (including custom voices)
4. Test synthesis with the preview button

## Troubleshooting

### Fish Speech API Not Starting

**Error**: `ModuleNotFoundError: No module named 'fish_speech'`
- Make sure Fish Speech is properly installed
- Check your Python environment and activate the correct conda/venv

**Error**: `FileNotFoundError: config.json not found`
- Ensure all model files are in the correct directory
- Download the required model files from Fish Speech releases

### API Connection Issues

**Error**: `Cannot connect to Fish Speech API`
- Verify the API is running on port 8000
- Check firewall settings
- Ensure no other service is using port 8000

### Voice Cloning Not Working

**Error**: `Voice 'voice_name' not found`
- Check that both `.wav` and `.txt` files exist in `voices/`
- Verify file naming matches exactly
- Restart the Fish Speech API after adding new voices

### Performance Issues

- Pre-generate embeddings for frequently used voices
- Use GPU acceleration if available (CUDA)
- Consider shorter reference audio for faster processing

## Configuration

### SwAIvyn Configuration

In `backend/appsettings.json`, you can configure:

```json
{
  "FishSpeech": {
    "BaseUrl": "http://localhost:8000",
    "AutoStart": true,
    "ModelPath": "../speech/TTS/openaudio-s1-mini"
  }
}
```

### Fish Speech Configuration

In `speech/TTS/openaudio-s1-mini/config.json`:
- Adjust model parameters
- Configure device settings (CPU/GPU)
- Set audio quality options

## Advanced Usage

### Programmatic API Usage

```csharp
// Using the Fish Speech service directly
var fishService = serviceProvider.GetService<FishSpeechTtsService>();

// Default voice
var audio = await fishService.SynthesizeAsync("Hello world");

// Custom voice
var audioCloned = await fishService.SynthesizeAsync("Hello world", "john_doe");

// Get available voices
var voices = await fishService.GetAvailableVoicesAsync();
```

### HTTP API Examples

**Default TTS:**
```bash
curl -X POST "http://localhost:8000/tts" \
  -F "text=Hello, this is Fish Speech TTS"
```

**Voice Cloning:**
```bash
curl -X POST "http://localhost:8000/tts/clone" \
  -F "text=Hello with cloned voice" \
  -F "voice_name=john_doe"
```

## Performance Tips

1. **Use GPU**: Enable CUDA for faster synthesis
2. **Pre-generate Embeddings**: Save speaker embeddings for frequently used voices
3. **Optimize Reference Audio**: Use clear, 3-10 second samples
4. **Batch Processing**: Process multiple texts in sequence rather than parallel

## Support and Resources

- [Fish Speech Documentation](https://speech.fish.audio/)
- [SwAIvyn TTS Documentation](./docs/tts_integration.md)
- [Troubleshooting Guide](./docs/troubleshooting.md)

## Recent Updates

- ✅ Added automatic Fish Speech API startup
- ✅ Integrated with SwAIvyn settings page
- ✅ Added comprehensive error handling
- ✅ Created integration tests
- ✅ Added voice management features
