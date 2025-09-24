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
  } catch {
    alert('Failed to play audio.');
  }
}
