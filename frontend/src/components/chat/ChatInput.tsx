import React, { useState, ChangeEvent, FormEvent, useEffect, useRef } from 'react';
import { transcribeAudio } from '../../services/sttService';

interface ChatInputProps {
  onSendMessage: (message: string) => void;
  onFileUpload: (file: File) => void;
}

/**
 * ChatInput component provides an input area with send button and file upload zone.
 * Fully commented and designed for integration with SignalR chat hub.
 */
const ChatInput: React.FC<ChatInputProps> = ({ onSendMessage, onFileUpload }) => {
  // State to hold the current input text
  const [inputText, setInputText] = useState<string>('');
  const [recording, setRecording] = useState(false);
  const [autoTts, setAutoTts] = useState<boolean>(() => {
    try { return localStorage.getItem('auto_tts') === 'true'; } catch { return false; }
  });
  const [autoStt, setAutoStt] = useState<boolean>(() => {
    try { return localStorage.getItem('auto_stt') === 'true'; } catch { return false; }
  });
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<BlobPart[]>([]);

  /**
   * Handles changes in the input text field.
   * @param e Change event
   */
  const handleInputChange = (e: ChangeEvent<HTMLInputElement>) => {
    setInputText(e.target.value);
  };

  /**
   * Handles form submission to send the message.
   * @param e Form event
   */
  const handleSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (inputText.trim() === '') return;
    onSendMessage(inputText.trim());
    setInputText('');
  };

  /**
   * Handles file selection for upload.
   * @param e Change event
   */
  const handleFileChange = (e: ChangeEvent<HTMLInputElement>) => {
    if (e.target.files && e.target.files.length > 0) {
      onFileUpload(e.target.files[0]);
      e.target.value = ''; // Reset file input
    }
  };

  // Persist toggles
  useEffect(() => { try { localStorage.setItem('auto_tts', String(autoTts)); } catch {} }, [autoTts]);
  useEffect(() => { try { localStorage.setItem('auto_stt', String(autoStt)); } catch {} }, [autoStt]);

  const toggleRecording = async () => {
    if (recording) {
      try { mediaRecorderRef.current?.stop(); } catch {}
      setRecording(false);
      return;
    }
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mr = new MediaRecorder(stream);
      mediaRecorderRef.current = mr;
      chunksRef.current = [];
      mr.ondataavailable = (e) => { if (e.data && e.data.size > 0) chunksRef.current.push(e.data); };
      mr.onstop = async () => {
        const blob = new Blob(chunksRef.current, { type: 'audio/webm' });
        try {
          const text = await transcribeAudio(blob);
          if (text) {
            if (autoStt) {
              // Auto-send when auto STT is enabled
              onSendMessage(text);
            } else {
              setInputText(prev => (prev ? prev + ' ' + text : text));
            }
          }
        } catch (err) {
          console.error('STT failed', err);
        } finally {
          // stop all tracks
          stream.getTracks().forEach(t => t.stop());
        }
      };
      mr.start();
      setRecording(true);
    } catch (err) {
      console.error('Could not start recording', err);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="p-4 border-t bg-white">
      <div className="flex items-center space-x-2">
        <input
          type="text"
          placeholder="Type your message..."
          value={inputText}
          onChange={handleInputChange}
          className="flex-grow px-4 py-2 border border-gray-300 rounded-full focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
        />
        <button type="button" onClick={toggleRecording} className={`px-3 py-2 rounded-full ${recording ? 'bg-red-500 text-white' : 'bg-gray-100 text-gray-700 hover:bg-gray-200'}`} title={recording ? 'Stop recording' : 'Start recording (STT)'}>
          {recording ? 'Stop' : '🎙️'}
        </button>
        <label className="inline-flex items-center text-xs text-gray-600 mx-1" title="Auto play AI responses">
          <input type="checkbox" className="mr-1" checked={autoTts} onChange={e => setAutoTts(e.target.checked)} /> Auto Play
        </label>
        <label className="inline-flex items-center text-xs text-gray-600 mx-1" title="Auto-send STT">
          <input type="checkbox" className="mr-1" checked={autoStt} onChange={e => setAutoStt(e.target.checked)} /> Auto STT
        </label>
        <label htmlFor="file-upload" className="p-2 text-gray-500 hover:text-primary-500 cursor-pointer">
          <svg xmlns="http://www.w3.org/2000/svg" className="h-6 w-6" fill="none" viewBox="0 0 24 24" stroke="currentColor">
            <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.172 7l-6.586 6.586a2 2 0 102.828 2.828L18 9.828m-4-2.828l6 6" />
          </svg>
        </label>
        <input id="file-upload" type="file" className="hidden" onChange={handleFileChange} />
        <button
          type="submit"
          className="p-2 bg-primary-500 text-white rounded-full hover:bg-primary-600 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2"
        >
          Send
        </button>
      </div>
    </form>
  );
};

export default ChatInput;
