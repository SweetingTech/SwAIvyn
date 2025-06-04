import apiService from './apiService';

const ttsService = {
  async synthesize(text: string, voiceId?: string): Promise<Blob> {
    const arrayBuffer = await apiService.post('/api/tts/synthesize', { text, voiceId }, { responseType: 'arraybuffer' });
    return new Blob([arrayBuffer], { type: 'audio/mpeg' });
  }
};

export default ttsService;
