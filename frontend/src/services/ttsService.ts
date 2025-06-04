import apiService from './apiService';

const ttsService = {
  async getSettings(userId?: string): Promise<{ apiKey: string; voice: string }> {
    const url = userId ? `/api/tts/settings?userId=${userId}` : '/api/tts/settings';
    return apiService.get(url);
  },

  async updateSettings(apiKey: string, voice: string, userId?: string): Promise<boolean> {
    const response = await apiService.put('/api/tts/settings', {
      apiKey,
      voice,
      userId
    });
    return !!response;
  }
};

export default ttsService;
