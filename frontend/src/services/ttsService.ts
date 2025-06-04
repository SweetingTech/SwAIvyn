import apiService from './apiService';

const ttsService = {
  async getSettings() {
    return await apiService.get('/api/tts/settings');
  },

  async updateSettings(apiKey: string, voiceId: string) {
    return await apiService.post('/api/tts/settings', { apiKey, voiceId });
  },

  async synthesize(text: string) {
    const response = await fetch('/api/tts/synthesize', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ text })
    });
    if (!response.ok) {
      throw new Error('Failed to synthesize');
    }
    return await response.blob();
  }
};

export default ttsService;
