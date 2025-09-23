import { createPrefixedLogger } from './logger';

const ttsLogger = createPrefixedLogger('TTS');

export async function playTts(blob: Blob): Promise<void> {
  try {
    const url = URL.createObjectURL(blob);
    const audio = new Audio(url);

    const cleanup = () => {
      URL.revokeObjectURL(url);
      audio.removeEventListener('ended', cleanup);
      audio.removeEventListener('error', cleanup);
    };

    audio.addEventListener('ended', cleanup);
    audio.addEventListener('error', cleanup);

    await audio.play();
  } catch (err) {
    ttsLogger.error('Error playing TTS', {
      error: err instanceof Error ? err.message : String(err),
    });
    alert('Failed to play audio.');
  }
}
