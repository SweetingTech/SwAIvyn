import { useState, useRef, useCallback, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { Mic, MicOff, Upload, Play, Square, X, UserCheck } from 'lucide-react';
import ttsService from '../../services/ttsService';

type RecordingState = 'idle' | 'recording' | 'recorded' | 'uploading' | 'done' | 'error';

// Detect the best audio MIME type supported by the current browser's MediaRecorder.
// We prefer webm/opus, then ogg/opus. The empty string at the end means "let the
// browser pick its own default" — MediaRecorder constructed without a mimeType option.
function getSupportedMimeType(): string {
  const candidates = ['audio/webm;codecs=opus', 'audio/webm', 'audio/ogg;codecs=opus', 'audio/ogg'];
  for (const type of candidates) {
    if (MediaRecorder.isTypeSupported(type)) return type;
  }
  // No explicit preference matched; let the browser use its built-in default.
  return '';
}

// Derive a file extension from a MIME type string (e.g. "audio/webm;codecs=opus" → "webm").
function extFromMime(mime: string): string {
  if (!mime) return 'audio';
  const base = mime.split(';')[0].split('/')[1] ?? 'audio';
  return base;
}

const VoiceProfileTrainer = () => {
  const [recordingState, setRecordingState] = useState<RecordingState>('idle');
  const [audioBlob, setAudioBlob] = useState<Blob | null>(null);
  const [audioUrl, setAudioUrl] = useState<string | null>(null);
  const [recordingMime, setRecordingMime] = useState<string>('audio/webm');
  const [recordingExt, setRecordingExt] = useState<string>('webm');
  const [transcript, setTranscript] = useState('');
  const [voiceName, setVoiceName] = useState('');
  const [errorMsg, setErrorMsg] = useState('');

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const streamRef = useRef<MediaStream | null>(null);

  // Revoke object URL when it changes or component unmounts to avoid memory leaks
  useEffect(() => {
    return () => {
      if (audioUrl) URL.revokeObjectURL(audioUrl);
    };
  }, [audioUrl]);

  // ── Recording controls ─────────────────────────────────────────────────────

  const startRecording = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      streamRef.current = stream;
      audioChunksRef.current = [];

      const mimeType = getSupportedMimeType();
      const mediaRecorder = mimeType
        ? new MediaRecorder(stream, { mimeType })
        : new MediaRecorder(stream);
      mediaRecorderRef.current = mediaRecorder;

      mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) audioChunksRef.current.push(e.data);
      };

      mediaRecorder.onstop = () => {
        const actualMime = mediaRecorder.mimeType || mimeType || 'audio/webm';
        const ext = extFromMime(actualMime);
        const blob = new Blob(audioChunksRef.current, { type: actualMime });
        const url = URL.createObjectURL(blob);
        setAudioBlob(blob);
        setAudioUrl(url);
        setRecordingMime(actualMime);
        setRecordingExt(ext);
        setRecordingState('recorded');
        stream.getTracks().forEach((t) => t.stop());
        streamRef.current = null;
      };

      mediaRecorder.start();
      setRecordingState('recording');
    } catch (err) {
      console.error('Microphone error:', err);
      setErrorMsg('Microphone access denied.');
      setRecordingState('error');
    }
  }, []);

  const stopRecording = useCallback(() => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      mediaRecorderRef.current.stop();
    }
  }, []);

  const playback = useCallback(() => {
    if (audioUrl) new Audio(audioUrl).play();
  }, [audioUrl]);

  const reset = useCallback(() => {
    // Note: audioUrl revocation is handled by the useEffect above
    setAudioBlob(null);
    setAudioUrl(null);
    setRecordingMime('audio/webm');
    setRecordingExt('webm');
    setRecordingState('idle');
    setErrorMsg('');
  }, []);

  // ── Upload ─────────────────────────────────────────────────────────────────

  const handleUpload = useCallback(async () => {
    if (!audioBlob || !voiceName.trim() || !transcript.trim()) {
      setErrorMsg('Please provide a voice name, a transcript, and a recording.');
      return;
    }

    // Build a File with the actual recorded MIME type and extension
    const fileName = `${voiceName.trim()}.${recordingExt}`;
    const file = new File([audioBlob], fileName, { type: recordingMime });
    const validation = ttsService.validateVoiceFile(file);
    if (!validation.valid) {
      setErrorMsg(validation.error ?? 'Invalid file.');
      return;
    }

    setRecordingState('uploading');
    setErrorMsg('');

    try {
      await ttsService.uploadVoice({
        audioFile: file,
        transcript: transcript.trim(),
        voiceName: voiceName.trim(),
      });
      setRecordingState('done');
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : 'Upload failed. Please try again.';
      setErrorMsg(msg);
      setRecordingState('error');
    }
  }, [audioBlob, voiceName, transcript, recordingMime, recordingExt]);

  // ── File upload (alternative to recording) ────────────────────────────────

  const handleFileChange = useCallback((e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    const validation = ttsService.validateVoiceFile(file);
    if (!validation.valid) {
      setErrorMsg(validation.error ?? 'Invalid file.');
      return;
    }
    const url = URL.createObjectURL(file);
    setAudioBlob(file);
    setAudioUrl(url);
    setRecordingState('recorded');
    setErrorMsg('');
  }, []);

  return (
    <div className="space-y-4">
      {/* Voice name */}
      <div>
        <label className="block text-xs text-gray-400 mb-1 font-medium">Voice Profile Name</label>
        <input
          type="text"
          value={voiceName}
          onChange={(e) => setVoiceName(e.target.value)}
          placeholder="e.g. my-custom-voice"
          className="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded-lg text-sm text-gray-200 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-primary-500"
        />
      </div>

      {/* Transcript */}
      <div>
        <label className="block text-xs text-gray-400 mb-1 font-medium">
          Reference Transcript
          <span className="text-gray-600 ml-1 font-normal">(what you will say in the recording)</span>
        </label>
        <textarea
          value={transcript}
          onChange={(e) => setTranscript(e.target.value)}
          rows={3}
          placeholder="The quick brown fox jumps over the lazy dog. Hello, my name is…"
          className="w-full px-3 py-2 bg-gray-800 border border-gray-600 rounded-lg text-sm text-gray-200 placeholder-gray-500 focus:outline-none focus:ring-1 focus:ring-primary-500 resize-none"
        />
      </div>

      {/* Recording controls */}
      <div className="flex flex-wrap items-center gap-2">
        {recordingState === 'idle' && (
          <>
            <button
              onClick={startRecording}
              className="flex items-center gap-2 px-4 py-2 bg-red-600 hover:bg-red-500 text-white rounded-lg text-sm transition-colors"
            >
              <Mic size={14} /> Record
            </button>
            <label className="flex items-center gap-2 px-4 py-2 bg-gray-700 hover:bg-gray-600 text-gray-300 rounded-lg text-sm transition-colors cursor-pointer">
              <Upload size={14} /> Upload file
              <input
                type="file"
                accept="audio/wav,audio/mpeg,audio/mp3"
                className="hidden"
                onChange={handleFileChange}
              />
            </label>
          </>
        )}

        {recordingState === 'recording' && (
          <button
            onClick={stopRecording}
            className="flex items-center gap-2 px-4 py-2 bg-red-700 hover:bg-red-600 text-white rounded-lg text-sm animate-pulse transition-colors"
          >
            <Square size={14} /> Stop Recording
          </button>
        )}

        {recordingState === 'recorded' && (
          <>
            <button
              onClick={playback}
              className="flex items-center gap-2 px-3 py-2 bg-gray-700 hover:bg-gray-600 text-gray-200 rounded-lg text-sm transition-colors"
            >
              <Play size={14} /> Play
            </button>
            <button
              onClick={reset}
              className="flex items-center gap-2 px-3 py-2 bg-gray-700 hover:bg-gray-600 text-gray-400 rounded-lg text-sm transition-colors"
            >
              <MicOff size={14} /> Re-record
            </button>
            <button
              onClick={handleUpload}
              className="flex items-center gap-2 px-4 py-2 bg-primary-600 hover:bg-primary-500 text-white rounded-lg text-sm transition-colors"
            >
              <Upload size={14} /> Save Profile
            </button>
          </>
        )}

        {recordingState === 'uploading' && (
          <span className="text-sm text-gray-400 animate-pulse">Uploading…</span>
        )}

        {recordingState === 'done' && (
          <div className="flex items-center gap-2 text-green-400 text-sm">
            <UserCheck size={16} /> Voice profile saved!
          </div>
        )}
      </div>

      {errorMsg && (
        <p className="text-xs text-red-400 bg-red-900/20 border border-red-800/40 rounded px-3 py-2">
          {errorMsg}
        </p>
      )}
    </div>
  );
};

// ─── Modal wrapper ────────────────────────────────────────────────────────────

interface VoiceProfileModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export const VoiceProfileModal = ({ isOpen, onClose }: VoiceProfileModalProps) => (
  <AnimatePresence>
    {isOpen && (
      <motion.div
        className="fixed inset-0 z-[200] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4"
        initial={{ opacity: 0 }}
        animate={{ opacity: 1 }}
        exit={{ opacity: 0 }}
        onClick={(e) => e.target === e.currentTarget && onClose()}
      >
        <motion.div
          className="bg-gray-900 border border-gray-700 rounded-2xl shadow-2xl w-full max-w-md p-6"
          initial={{ y: 20, scale: 0.97 }}
          animate={{ y: 0, scale: 1 }}
          exit={{ y: 20, scale: 0.97 }}
          transition={{ type: 'spring', damping: 25, stiffness: 300 }}
        >
          <div className="flex items-center justify-between mb-4">
            <h2 className="text-base font-semibold text-gray-100">Voice Profile Training</h2>
            <button
              onClick={onClose}
              className="p-1.5 text-gray-400 hover:text-white rounded-full hover:bg-gray-800 transition-colors"
              aria-label="Close voice profile trainer"
            >
              <X size={16} />
            </button>
          </div>
          <p className="text-xs text-gray-500 mb-4">
            Record a reference clip and a matching transcript to fine-tune the TTS voice associated with
            this character. The audio is sent to the Fish&nbsp;Speech voice engine.
          </p>
          <VoiceProfileTrainer />
        </motion.div>
      </motion.div>
    )}
  </AnimatePresence>
);

export default VoiceProfileTrainer;
