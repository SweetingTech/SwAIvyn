import type { AxiosRequestConfig } from 'axios';
import apiService from './apiService';

interface TtsProvider {
  id: string;
  name: string;
  available: boolean;
}

interface VoiceSettings {
  apiKey: string;
  voiceId: string;
  fishSpeechApiKey: string;
  ttsProvider: string;
  providers: TtsProvider[];
}

interface UpdateTtsSettingsRequest {
  userId?: string;
  apiKey?: string;
  voiceId?: string;
  fishSpeechApiKey?: string;
  ttsProvider?: string;
}

interface VoiceDetails {
  name: string;
  transcript: string;
  hasAudioFile: boolean;
  hasEmbedding: boolean;
  createdAt?: string;
  audioFileSize?: number;
}

interface UploadVoiceRequest {
  audioFile: File;
  transcript: string;
  voiceName: string;
}

interface SynthesizeRequest {
  text: string;
  userId?: string;
  voiceId?: string;
}

const ttsService = {
  /**
   * Fetch TTS settings including provider information.
   * If userId is provided, appends it as a query parameter.
   */  async getSettings(userId?: string): Promise<VoiceSettings> {
    const url = userId
      ? `/api/tts/settings?userId=${userId}`
      : '/api/tts/settings';
    return apiService.get(url);
  },
  /**
   * Update TTS settings including provider selection.
   * Sends settings to the backend.
   */
  async updateSettings(
    settings: UpdateTtsSettingsRequest
  ): Promise<boolean> {
    const response = await apiService.post('/api/tts/settings', settings);
    return !!response;
  },
  /**
   * Get available voices for a specific provider.
   */
  async getVoices(provider: string = 'elevenlabs', userId?: string): Promise<string[]> {
    const url = userId
      ? `/api/tts/voices?provider=${provider}&userId=${userId}`
      : `/api/tts/voices?provider=${provider}`;
    const response = await apiService.get(url);
    return response.voices || [];
  },

  /** Fetch TTS health (GPU/CPU upstreams) via backend */
  async getHealth(): Promise<{ status: string; upstream?: string; upstreams?: { url: string; status: string }[] }>{
    return apiService.get('/api/tts/health');
  },
  /**
   * Synthesize speech for the given text.
   * Returns an audio Blob.
   */
  async synthesize(text: string, userId?: string, voiceId?: string): Promise<Blob> {
    const payload: SynthesizeRequest = { text };
    if (userId) payload.userId = userId;
    if (voiceId) payload.voiceId = voiceId;

    // Use apiService to include Authorization header automatically
    const blobRequestConfig: AxiosRequestConfig = { responseType: 'blob' };
    const blob = await apiService.post('/api/tts/synthesize', payload, blobRequestConfig);
    return blob as unknown as Blob;
  },

  /**
   * Get detailed information about Fish Speech voices.
   */
  async getVoiceDetails(voiceName: string): Promise<VoiceDetails> {
    return apiService.get(`/api/tts/voices/${voiceName}/details`);
  },

  /**
   * Upload a new voice for Fish Speech TTS.
   * Accepts audio file and transcript.
   */
  async uploadVoice(request: UploadVoiceRequest): Promise<VoiceDetails> {
    const formData = new FormData();
    formData.append('audioFile', request.audioFile);
    formData.append('transcript', request.transcript);
    formData.append('voiceName', request.voiceName);

    const resp = await fetch('/api/tts/voices/upload', {
      method: 'POST',
      body: formData,
    });

    if (!resp.ok) {
      const errorText = await resp.text();
      throw new Error(`Failed to upload voice: ${errorText}`);
    }

    return await resp.json();
  },

  /**
   * Delete a voice from Fish Speech TTS.
   */
  async deleteVoice(voiceName: string): Promise<boolean> {
    const resp = await fetch(`/api/tts/voices/${voiceName}`, {
      method: 'DELETE',
    });

    if (!resp.ok) {
      const errorText = await resp.text();
      throw new Error(`Failed to delete voice: ${errorText}`);
    }

    return true;
  },

  /**
   * Validate voice file before upload.
   * Checks file type and size constraints.
   */
  validateVoiceFile(file: File): { valid: boolean; error?: string } {
    const maxSizeBytes = 50 * 1024 * 1024; // 50MB
    const allowedTypes = ['audio/wav', 'audio/mpeg', 'audio/mp3'];

    if (!allowedTypes.includes(file.type)) {
      return {
        valid: false,
        error: 'Please upload a WAV or MP3 audio file.',
      };
    }

    if (file.size > maxSizeBytes) {
      return {
        valid: false,
        error: 'File size must be less than 50MB.',
      };
    }

    return { valid: true };
  },
};

export default ttsService;
export type { TtsProvider, VoiceSettings, UpdateTtsSettingsRequest, VoiceDetails, UploadVoiceRequest };
