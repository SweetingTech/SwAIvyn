export async function transcribeAudio(blob: Blob, language = 'en'): Promise<string> {
  const form = new FormData();
  form.append('audio', blob, 'speech.webm');
  form.append('language', language);
  form.append('task', 'transcribe');
  const resp = await fetch('/api/stt/transcribe', { method: 'POST', body: form });
  if (!resp.ok) throw new Error(await resp.text());
  const data = await resp.json();
  // Try common shapes
  return data.text || data.transcription || data.result || '';
}

