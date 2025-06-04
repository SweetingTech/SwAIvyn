import apiService from './apiService';

interface VoiceSettings {
  apiKey: string;
  voice: string;
}

const ttsService = {
  /**
   * Fetch the ElevenLabs TTS settings (API key and default voice).
   * If userId is provided, appends it as a query parameter.
   */
  async getSettings(userId?: string): Promise<VoiceSettings> {
    const url = userId
      ? `/api/tts/settings?userId=${userId}`
      : '/api/tts/settings';
    return apiService.get<VoiceSettings>(url);
  },

  /**
   * Update the ElevenLabs TTS settings.
   * Sends apiKey and voice (and optional userId) to the backend.
   */
  async updateSettings(
    apiKey: string,
    voice: string,
    userId?: string
  ): Promise<boolean> {
    const payload: Record<string, unknown> = { apiKey, voice };
    if (userId) {
      payload.userId = userId;
    }
    const response = await apiService.put('/api/tts/settings', payload);
    return !!response;
  },

  /**
   * Synthesize speech for the given text.
   * Returns an audio Blob (audio/mpeg).
   */
  async synthesize(text: string): Promise<Blob> {
    const resp = await fetch('/api/tts/synthesize', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text }),
    });
    if (!resp.ok) {
      throw new Error('Failed to synthesize');
    }
    return await resp.blob();
  },
};

export default ttsService;
