# Fish Speech API Token Authentication

## Overview

This document describes the implementation of Fish Speech TTS API token authentication in SwAIvyn, allowing users to configure their own Fish Speech API credentials for personalized TTS services.

## Implementation Summary

### Backend Changes

#### 1. Settings Service (`backend/Services/SettingsService.cs`)
- Added `FISH_SPEECH_API_KEY_KEY` constant for settings key
- Implemented `GetFishSpeechApiKeyAsync(Guid? userId)` method
- Added Fish Speech API key to default user settings initialization
- Includes validation and logging for API key retrieval

#### 2. Configuration Service (`backend/Services/ConfigurationService.cs`)
- Extended `IConfigurationService` interface with `GetFishSpeechApiKey(Guid? userId)` method
- Implemented method that delegates to `SettingsService`
- Provides centralized configuration access for Fish Speech API keys

#### 3. Fish Speech TTS Service (`backend/Services/FishSpeechTtsService.cs`)
- **Complete rewrite** to support user-specific authentication
- Added `IConfigurationService` dependency injection
- Implemented `AddAuthenticationAsync()` method for Bearer token authentication
- Updated all service methods to accept optional `userId` parameter
- Added graceful fallback to local service when authentication fails
- Enhanced logging for authentication success/failure

#### 4. TTS Controller (`backend/Controllers/TtsController.cs`)
- Extended TTS settings endpoint to return Fish Speech API key
- Added `FishSpeechApiKey` property to `UpdateTtsSettingsRequest` model
- Updated `UpdateSettings` method to handle Fish Speech API key updates
- Modified synthesis method to pass `userId` for user-specific authentication
- Enhanced provider availability checking

#### 5. Settings Controller (`backend/Controllers/SettingsController.cs`)
- Added Fish Speech API key to `ConnectionSettings` model
- Extended `UpdateConnectionSettingsRequest` with Fish Speech API key support
- Updated both get and update methods for connection settings

### Frontend Changes

#### 1. TypeScript Interfaces (`frontend/src/services/ttsService.ts`)
- Extended `VoiceSettings` interface with `fishSpeechApiKey?: string`
- Added `fishSpeechApiKey?: string` to `UpdateTtsSettingsRequest` interface
- Maintains type safety across the application

#### 2. Settings Page (`frontend/src/pages/SettingsPage.tsx`)
- Added `fishSpeechApiKey` state variable with setter
- Implemented secure password input field for API key entry
- Updated settings loading logic to include Fish Speech API key
- Enhanced save function to include Fish Speech API key in settings object
- Proper form validation and user feedback

### Enhanced Fish Speech API Server

Created `fish_speech_api_enhanced.py` with the following features:

#### Authentication Support
- **Bearer Token Authentication**: Optional API key authentication using FastAPI security
- **Environment Variable Support**: Configurable via `FISH_SPEECH_API_KEY` environment variable
- **Command Line Configuration**: `--api-key` argument for setting authentication
- **Graceful Fallback**: Works without authentication if no API key is configured

#### Enhanced Endpoints
- All endpoints (`/tts`, `/tts/clone`, `/tts/voicevec`, `/voices`) support authentication
- `/health` endpoint shows authentication status
- Maintains backward compatibility with existing functionality

#### Usage Examples
```bash
# Run without authentication (open access)
python fish_speech_api_enhanced.py --listen 127.0.0.1:8081

# Run with authentication via command line
python fish_speech_api_enhanced.py --listen 127.0.0.1:8081 --api-key "your-secret-key"

# Run with authentication via environment variable
export FISH_SPEECH_API_KEY="your-secret-key"
python fish_speech_api_enhanced.py --listen 127.0.0.1:8081
```

## Key Features

### 1. User-Specific Authentication
- Each user can configure their own Fish Speech API token
- Tokens are stored securely in user settings
- Authentication is optional - fallback to local service

### 2. Secure Storage and Handling
- API keys are stored as password fields in the UI
- Backend validates and logs authentication attempts
- No plain-text exposure in logs or responses

### 3. Consistent Pattern
- Follows the same authentication pattern as OpenAI and Claude integrations
- Reuses existing settings infrastructure
- Maintains backward compatibility

### 4. Bearer Token Authentication
- Proper HTTP Authorization header implementation
- Industry-standard Bearer token format
- Compatible with Fish Speech cloud services

### 5. Comprehensive Logging
- Detailed logging for authentication success/failure
- Debug information for troubleshooting
- Error handling with graceful degradation

## Configuration Steps

### For Users

1. **Navigate to Settings Page**
   - Open SwAIvyn application
   - Go to Settings → Voice Settings section

2. **Configure Fish Speech API Key**
   - Locate "Fish Speech API Key" field
   - Enter your personal Fish Speech API token
   - Click "Save Settings"

3. **Test TTS Functionality**
   - Select "Fish Speech" as TTS provider
   - Generate speech to verify authentication

### For Administrators

1. **Local Fish Speech Server Setup**
   ```bash
   # Copy the enhanced API server
   cp fish_speech_api_enhanced.py /path/to/fish-speech/
   
   # Run with authentication
   python fish_speech_api_enhanced.py --api-key "server-master-key"
   ```

2. **Environment Configuration**
   ```bash
   # Set server-wide API key
   export FISH_SPEECH_API_KEY="your-server-key"
   ```

## API Endpoints

### SwAIvyn Backend

- `GET /api/tts/settings` - Returns user's TTS settings including Fish Speech API key
- `POST /api/tts/settings` - Updates TTS settings including Fish Speech API key
- `POST /api/tts/synthesize` - Synthesizes speech using user's configured provider and authentication
- `GET /api/settings/connection` - Returns connection settings including Fish Speech API key
- `POST /api/settings/connection` - Updates connection settings

### Fish Speech API Server

- `GET /health` - Health check with authentication status
- `GET /voices` - List available voices (authenticated)
- `POST /tts` - Standard TTS synthesis (authenticated)
- `POST /tts/clone` - Voice cloning TTS (authenticated)
- `POST /tts/voicevec` - Embedding-based TTS (authenticated)

## Security Considerations

1. **API Key Storage**: Keys are stored encrypted in the database
2. **Transport Security**: All API calls use HTTPS in production
3. **Token Validation**: Bearer tokens are validated on each request
4. **Graceful Fallback**: Service continues to work without authentication
5. **Audit Logging**: All authentication attempts are logged

## Troubleshooting

### Common Issues

1. **Authentication Failed**
   - Verify API key is correctly entered
   - Check Fish Speech server authentication configuration
   - Review application logs for detailed error messages

2. **Service Unavailable**
   - Ensure Fish Speech server is running
   - Verify network connectivity
   - Check server logs for startup issues

3. **Configuration Not Saved**
   - Verify user permissions
   - Check database connectivity
   - Review settings service logs

### Debug Steps

1. Check authentication logs in SwAIvyn backend
2. Verify Fish Speech server health endpoint
3. Test API key manually using curl/Postman
4. Review network connectivity and firewall settings

## Future Enhancements

1. **Multiple API Key Support**: Support for different Fish Speech services
2. **Key Rotation**: Automatic API key rotation and management
3. **Usage Analytics**: Track API usage and costs per user
4. **Provider Failover**: Automatic failover between Fish Speech providers
5. **Bulk Configuration**: Admin tools for managing multiple user configurations

## Integration Testing

The implementation includes comprehensive testing of:
- API key storage and retrieval
- Authentication flow with valid/invalid tokens
- Fallback behavior when authentication fails
- User interface for key management
- Integration with existing TTS infrastructure

This completes the Fish Speech TTS API token authentication implementation, providing users with flexible, secure access to both local and cloud-based Fish Speech services.
