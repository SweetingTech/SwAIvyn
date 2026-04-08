import { useEffect, useRef, useCallback, useState } from 'react';

// Browser Speech Recognition type augmentation
interface SpeechRecognitionEvent extends Event {
  results: SpeechRecognitionResultList;
}

interface SpeechRecognitionResultList {
  length: number;
  item(index: number): SpeechRecognitionResult;
  [index: number]: SpeechRecognitionResult;
}

interface SpeechRecognitionResult {
  isFinal: boolean;
  length: number;
  item(index: number): SpeechRecognitionAlternative;
  [index: number]: SpeechRecognitionAlternative;
}

interface SpeechRecognitionAlternative {
  transcript: string;
  confidence: number;
}

interface SpeechRecognitionError extends Event {
  error: string;
  message?: string;
}

// Normalize the vendor-prefixed constructor
const SpeechRecognitionCtor: (new () => SpeechRecognition) | null =
  (typeof window !== 'undefined' &&
    (window.SpeechRecognition ||
      // @ts-expect-error vendor prefix
      window.webkitSpeechRecognition)) ||
  null;

interface SpeechRecognition extends EventTarget {
  continuous: boolean;
  interimResults: boolean;
  lang: string;
  start(): void;
  stop(): void;
  abort(): void;
  onresult: ((event: SpeechRecognitionEvent) => void) | null;
  onerror: ((event: SpeechRecognitionError) => void) | null;
  onend: (() => void) | null;
}

// ─── Public hook ──────────────────────────────────────────────────────────────

interface UseWakeWordOptions {
  wakeWord?: string;
  enabled?: boolean;
  onDetected?: () => void;
  lang?: string;
}

interface UseWakeWordReturn {
  isSupported: boolean;
  isActive: boolean;
  /** Manually enable/disable wake-word listening */
  setActive: (active: boolean) => void;
}

/**
 * `useWakeWord` – continuously listens in the background for a configurable
 * wake word using the Web SpeechRecognition API.  When the phrase is heard the
 * `onDetected` callback fires.
 *
 * Browsers that don't support SpeechRecognition (e.g. Firefox without a flag)
 * return `isSupported: false` and the hook is a no-op.
 */
export function useWakeWord({
  wakeWord = 'hey assistant',
  enabled = false,
  onDetected,
  lang = 'en-US',
}: UseWakeWordOptions = {}): UseWakeWordReturn {
  const [isActive, setIsActive] = useState(false);
  const recognitionRef = useRef<SpeechRecognition | null>(null);
  const isActiveRef = useRef(false);

  const isSupported = SpeechRecognitionCtor !== null;

  const normalize = (s: string) => s.toLowerCase().trim();
  const needle = normalize(wakeWord);

  const stop = useCallback(() => {
    recognitionRef.current?.abort();
    recognitionRef.current = null;
    isActiveRef.current = false;
    setIsActive(false);
  }, []);

  const start = useCallback(() => {
    if (!isSupported || !SpeechRecognitionCtor) return;
    if (recognitionRef.current) return; // already running

    const recognition = new SpeechRecognitionCtor();
    recognition.continuous = true;
    recognition.interimResults = true;
    recognition.lang = lang;

    recognition.onresult = (event: SpeechRecognitionEvent) => {
      for (let i = 0; i < event.results.length; i++) {
        const result = event.results[i];
        const transcript = normalize(result[0].transcript);
        if (transcript.includes(needle)) {
          onDetected?.();
          // Don't stop: keep listening for subsequent wake words
          break;
        }
      }
    };

    recognition.onerror = (event: SpeechRecognitionError) => {
      // 'no-speech' and 'aborted' are expected; restart only on real errors
      if (event.error !== 'no-speech' && event.error !== 'aborted') {
        console.warn('Wake-word recognition error:', event.error);
      }
    };

    recognition.onend = () => {
      // Auto-restart to keep continuously listening
      if (isActiveRef.current) {
        try {
          recognition.start();
        } catch {
          // Already started or permission denied; ignore
        }
      }
    };

    recognitionRef.current = recognition;
    isActiveRef.current = true;
    setIsActive(true);

    try {
      recognition.start();
    } catch (err) {
      console.warn('Could not start wake-word recognition:', err);
      recognitionRef.current = null;
      isActiveRef.current = false;
      setIsActive(false);
    }
  }, [isSupported, lang, needle, onDetected]);

  // Respond to `enabled` prop changes
  useEffect(() => {
    if (enabled) {
      start();
    } else {
      stop();
    }
    return stop;
  }, [enabled, start, stop]);

  const setActive = useCallback(
    (active: boolean) => {
      if (active) start();
      else stop();
    },
    [start, stop],
  );

  return { isSupported, isActive, setActive };
}

export default useWakeWord;
