import { Play, User, Loader2 } from 'lucide-react';
import { motion } from 'framer-motion';
import { Message } from '../../types/chat';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import { useState, useEffect } from 'react';
import ttsService from '../../services/ttsService';
import { useInitialization } from '../../contexts/InitializationContext';

interface ChatMessageProps {
  message: Message;
  characterImage?: string;
}

const ChatMessage = ({ message, characterImage }: ChatMessageProps) => {
  const isAI = message.sender === 'ai';
  const [isPlaying, setIsPlaying] = useState(false);
  const [imageError, setImageError] = useState(false);
  const { user } = useInitialization();

  const handleImageError = () => {
    console.warn('Failed to load character image:', characterImage);
    setImageError(true);
  };

  const handlePlayAudio = async () => {
    // Prefer authenticated user context, but fall back to anonymous settings
    const userId = user?.id;
    setIsPlaying(true);
    try {
      // Fetch TTS settings (works without userId as backend provides defaults)
      const ttsSettings = await ttsService.getSettings(userId);
      if (!ttsSettings || !ttsSettings.voiceId) {
        console.error("Could not retrieve TTS settings or voiceId.");
        setIsPlaying(false);
        return;
      }
      const voiceIdToUse = ttsSettings.voiceId;

      console.log(
        `Requesting TTS for text: "${message.text}"` +
          (userId ? ` with user ID: ${userId}` : '') +
          ` and voice ID: ${voiceIdToUse}`
      );

      const audioBlob = await ttsService.synthesize(message.text, userId, voiceIdToUse);
      
      if (audioBlob && audioBlob.size > 0) {
        const audio = new Audio(URL.createObjectURL(audioBlob));
        audio.play();
        audio.onended = () => {
          setIsPlaying(false);
          URL.revokeObjectURL(audio.src);
          console.log("Audio playback ended.");
        };
        audio.onerror = (e) => {
          console.error("Error during audio playback:", e);
          setIsPlaying(false);
          URL.revokeObjectURL(audio.src);
        };
      } else {
        console.error("Received empty or invalid audio blob.");
        setIsPlaying(false);
      }
    } catch (error) {
      console.error("Error synthesizing or playing audio:", error);
      setIsPlaying(false);
    }
    // Note: setIsPlaying(false) is primarily handled by audio.onended or onerror
    // to ensure it's set after playback finishes or fails.
  };

  // Auto-play on mount for AI messages when enabled
  // Read a simple toggle from localStorage so no backend setting is required
  // eslint-disable-next-line react-hooks/rules-of-hooks
  useEffect(() => {
    if (isAI) {
      try {
        const auto = localStorage.getItem('auto_tts') === 'true';
        if (auto) { void handlePlayAudio(); }
      } catch {}
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const formatTime = (ts: string) => {
    const d = new Date(ts);
    if (isNaN(d.getTime())) return 'Unknown';
    return d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  };
  
  return (
    <motion.div
      className={`flex ${isAI ? 'justify-start' : 'justify-end'}`}
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className={`flex max-w-[80%] ${isAI ? 'flex-row' : 'flex-row-reverse'}`}>
        <div className={`flex-shrink-0 ${isAI ? 'mr-3' : 'ml-3'}`}>
          {isAI ? (
            characterImage && !imageError ? (
              <img
                src={characterImage}
                alt="Character"
                className="w-8 h-8 rounded-full object-cover"
                onError={handleImageError}
              />
            ) : (
              <div className="w-8 h-8 bg-primary-100 rounded-full flex items-center justify-center text-primary-600">
                <div className="w-6 h-6">
                  <svg viewBox="0 0 32 32" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <path fillRule="evenodd" clipRule="evenodd" d="M16 5C9.92487 5 5 9.92487 5 16C5 22.0751 9.92487 27 16 27C22.0751 27 27 22.0751 27 16C27 9.92487 22.0751 5 16 5ZM9 16C9 12.134 12.134 9 16 9C19.866 9 23 12.134 23 16C23 19.866 19.866 23 16 23C12.134 23 9 19.866 9 16Z" fill="currentColor"/>
                    <path d="M16 12C13.7909 12 12 13.7909 12 16C12 18.2091 13.7909 20 16 20C18.2091 20 20 18.2091 20 16C20 13.7909 18.2091 12 16 12Z" fill="currentColor"/>
                  </svg>
                </div>
              </div>
            )
          ) : (
            <div className="w-8 h-8 bg-gray-200 rounded-full flex items-center justify-center text-gray-600">
              <User size={18} />
            </div>
          )}
        </div>
        
        <div>
          <div className={`rounded-lg px-4 py-2 shadow-sm ${
            isAI 
              ? 'bg-white text-gray-800 border border-gray-200' 
              : 'bg-primary-500 text-white'
          }`}>
            <ReactMarkdown remarkPlugins={[remarkGfm]}>{message.text}</ReactMarkdown>
          </div>
          
          <div className={`mt-1 flex items-center text-xs text-gray-500 ${
            isAI ? 'justify-start' : 'justify-end'
          }`}>
            <span>{formatTime(message.timestamp)}</span>
            
            {isAI && (
              <button 
                onClick={handlePlayAudio}
                disabled={isPlaying}
                className="ml-2 flex items-center text-xs text-primary-600 hover:text-primary-700 disabled:opacity-50 disabled:cursor-not-allowed"
              >
                {isPlaying ? (
                  <>
                    <Loader2 size={12} className="mr-1 animate-spin" />
                    Playing...
                  </>
                ) : (
                  <>
                    <Play size={12} className="mr-1" />
                    Play
                  </>
                )}
              </button>
            )}
          </div>
        </div>
      </div>
    </motion.div>
  );
};

export default ChatMessage;
