import { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { Mic, Volume2, MessageSquare, Loader2, UserCircle2, Radio } from 'lucide-react';
import ttsService from '../services/ttsService';
import chatService from '../services/chatService';
import conversationService from '../services/conversationService';
import { transcribeAudio } from '../services/sttService';
import MiniChat from '../components/voice-room/MiniChat';
import Avatar3DScene from '../components/voice-room/Avatar3DScene';
import AvatarStatsPanel, { useAvatarStats } from '../components/voice-room/AvatarStats';
import RoomCustomizer, { loadRoomItems } from '../components/voice-room/RoomCustomizer';
import { VoiceProfileModal } from '../components/voice-room/VoiceProfileTrainer';
import { useWakeWord } from '../hooks/useWakeWord';
import useEffectiveUser from '../hooks/useEffectiveUser';
import { Message } from '../types/chat';

interface VoiceConfig {
  apiKey: string;
  voice: string;
  ttsProvider: string;
}

const VoiceRoomPage = () => {
  const effectiveUserId = useEffectiveUser();
  const [isMiniChatOpen, setIsMiniChatOpen] = useState(false);
  const [isVoiceProfileOpen, setIsVoiceProfileOpen] = useState(false);
  const [isListening, setIsListening] = useState(false);
  const [isSpeaking, setIsSpeaking] = useState(false);
  const [isProcessing, setIsProcessing] = useState(false);
  const [voiceConfig, setVoiceConfig] = useState<VoiceConfig>({ apiKey: '', voice: 'glados', ttsProvider: 'fishspeech' });
  const [messages, setMessages] = useState<Message[]>([]);
  const [sessionId, setSessionId] = useState<string | null>(null);

  // 3D room state
  const [activeRoomItems, setActiveRoomItems] = useState<string[]>(loadRoomItems);
  const [wakeWordEnabled, setWakeWordEnabled] = useState(false);

  // Tamagotchi stats
  const { stats, setStats, recordInteraction } = useAvatarStats();

  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const audioChunksRef = useRef<Blob[]>([]);
  const audioRef = useRef<HTMLAudioElement | null>(null);

  // Wake word detection
  const { isSupported: wakeWordSupported, isActive: wakeWordActive, setActive: setWakeWordActive } =
    useWakeWord({
      wakeWord: 'hey assistant',
      enabled: wakeWordEnabled,
      onDetected: () => {
        if (!isListening && !isProcessing && !isSpeaking) {
          startRecording();
        }
      },
    });

  useEffect(() => {
    const loadSettings = async () => {
      try {
        const cfg = await ttsService.getSettings();
        setVoiceConfig({ 
          apiKey: cfg.apiKey || '', 
          voice: cfg.voiceId || 'glados',
          ttsProvider: cfg.ttsProvider || 'fishspeech'
        });
      } catch (err) {
        console.error('Failed to load TTS settings', err);
      }
    };
    loadSettings();

    // Cleanup on unmount
    return () => {
      if (audioRef.current) {
        const currentAudio = audioRef.current;
        if (currentAudio.src) {
          try {
            URL.revokeObjectURL(currentAudio.src);
          } catch (e) {
            console.warn('Failed to revoke audio object URL', e);
          }
          currentAudio.src = '';
        }
        currentAudio.pause();
        audioRef.current = null;
      }
      if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
        mediaRecorderRef.current.stop();
      }
    };
  }, []);

  const startRecording = async () => {
    try {
      if (isSpeaking && audioRef.current) {
        const currentAudio = audioRef.current;
        if (currentAudio.src) {
          try {
            URL.revokeObjectURL(currentAudio.src);
          } catch (e) {
            console.warn('Failed to revoke audio object URL', e);
          }
          currentAudio.src = '';
        }
        currentAudio.pause();
        audioRef.current = null;
        setIsSpeaking(false);
      }

      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mediaRecorder = new MediaRecorder(stream);
      mediaRecorderRef.current = mediaRecorder;
      audioChunksRef.current = [];

      mediaRecorder.ondataavailable = (event) => {
        if (event.data.size > 0) {
          audioChunksRef.current.push(event.data);
        }
      };

      mediaRecorder.onstop = async () => {
        setIsListening(false);
        setIsProcessing(true);
        const audioBlob = new Blob(audioChunksRef.current, { type: 'audio/webm' });

        try {
          // Send to STT endpoint
          const transcript = await transcribeAudio(audioBlob);
          if (transcript && transcript.trim()) {
            await handleUserMessage(transcript);
          } else {
             setIsProcessing(false);
          }
        } catch (err) {
          console.error("STT error:", err);
          setIsProcessing(false);
        }

        // Stop all tracks
        stream.getTracks().forEach(track => track.stop());
      };

      mediaRecorder.start();
      setIsListening(true);
    } catch (err) {
      console.error("Microphone access denied or failed", err);
      setIsListening(false);
    }
  };

  const stopRecording = () => {
    if (mediaRecorderRef.current && mediaRecorderRef.current.state === 'recording') {
      mediaRecorderRef.current.stop();
    }
  };

  const toggleListening = () => {
    if (isListening) {
      stopRecording();
    } else {
      startRecording();
    }
  };

  const handleUserMessage = async (text: string) => {
    const newUserMsg: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text,
      timestamp: new Date().toISOString(),
    };
    setMessages((prev) => [...prev, newUserMsg]);
    setIsProcessing(true);

    try {
      let currentSessionId = sessionId;
      if (!currentSessionId) {
        const newSession = await conversationService.createConversation(
          text.substring(0, 30),
          effectiveUserId || undefined,
          undefined
        );
        currentSessionId = newSession.id;
        setSessionId(currentSessionId);
      }

      const responseText = await chatService.sendMessage(currentSessionId!, text);

      const newAiMsg: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'ai',
        text: responseText,
        timestamp: new Date().toISOString(),
      };

      setMessages((prev) => [...prev, newAiMsg]);
      await playTts(responseText);
      recordInteraction();

    } catch (error) {
      console.error('Chat error:', error);
      const errorMsg: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'ai',
        text: 'Sorry, I encountered an error processing your request.',
        timestamp: new Date().toISOString(),
        isError: true,
      };
      setMessages((prev) => [...prev, errorMsg]);
    } finally {
      setIsProcessing(false);
    }
  };

  const playTts = async (text: string) => {
    try {
      setIsSpeaking(true);

      // Use the ttsService which handles authorization automatically
      const blob = await ttsService.synthesize(text, effectiveUserId || undefined, voiceConfig.voice);
      const url = URL.createObjectURL(blob);
      const audio = new Audio(url);
      audioRef.current = audio;

      audio.onended = () => {
        setIsSpeaking(false);
        URL.revokeObjectURL(url);
        audioRef.current = null;
      };

      await audio.play();
    } catch (err) {
      console.error('TTS failed', err);
      setIsSpeaking(false);
    }
  };

  const playSample = () => {
    playTts('Hello there! This is a sample from your configured voice.');
  };

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gradient-to-b from-gray-900 to-gray-800 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto h-full flex flex-col">
        <div className="text-center mb-4">
          <h1 className="text-3xl font-light text-white tracking-wider">Voice Room</h1>
          <p className="text-gray-400">Speak naturally with your AI assistant</p>
        </div>

        {/* Stats panel */}
        <div className="mb-3 max-w-xs mx-auto w-full">
          <AvatarStatsPanel stats={stats} onStatsChange={setStats} />
        </div>

        <div className="flex-grow bg-gray-900/50 backdrop-blur-sm rounded-2xl border border-gray-700/50 shadow-2xl overflow-hidden relative">

          {/* 3D Scene */}
          <div className="w-full h-full min-h-[520px] relative">
            <Avatar3DScene
              isListening={isListening}
              isSpeaking={isSpeaking}
              isProcessing={isProcessing}
              activeRoomItems={activeRoomItems}
            />

            {/* Controls bar */}
            <div className="absolute bottom-8 left-0 right-0 flex justify-center items-end space-x-4 z-10">
              {/* Mic */}
              <button
                className={`p-5 rounded-full shadow-lg flex items-center justify-center transition-all transform hover:scale-105 ${
                  isListening
                    ? 'bg-red-500 text-white shadow-red-500/50'
                    : isProcessing
                    ? 'bg-primary-600 text-white cursor-wait opacity-80'
                    : 'bg-white text-gray-800 hover:bg-gray-100'
                }`}
                onClick={toggleListening}
                disabled={isProcessing}
                aria-label={isListening ? 'Stop recording' : 'Start recording'}
              >
                {isProcessing ? <Loader2 size={28} className="animate-spin" /> : <Mic size={28} className={isListening ? 'animate-pulse' : ''} />}
              </button>

              {/* Test voice */}
              <button
                className="p-4 rounded-full shadow-md bg-gray-800/80 border border-gray-700 text-gray-300 hover:bg-gray-700 hover:text-white flex items-center justify-center transition-all backdrop-blur-md"
                onClick={playSample}
                title="Test Voice"
                aria-label="Play voice sample"
              >
                <Volume2 size={22} />
              </button>

              {/* Text chat */}
              <button
                className={`p-4 rounded-full shadow-md border flex items-center justify-center transition-all backdrop-blur-md ${
                  isMiniChatOpen
                    ? 'bg-primary-600 border-primary-500 text-white'
                    : 'bg-gray-800/80 border-gray-700 text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
                onClick={() => setIsMiniChatOpen(!isMiniChatOpen)}
                title="Open Text Chat"
                aria-label="Toggle text chat"
              >
                <MessageSquare size={22} />
              </button>

              {/* Voice profile trainer */}
              <button
                className={`p-4 rounded-full shadow-md border flex items-center justify-center transition-all backdrop-blur-md ${
                  isVoiceProfileOpen
                    ? 'bg-primary-600 border-primary-500 text-white'
                    : 'bg-gray-800/80 border-gray-700 text-gray-300 hover:bg-gray-700 hover:text-white'
                }`}
                onClick={() => setIsVoiceProfileOpen(true)}
                title="Voice Profile Training"
                aria-label="Open voice profile trainer"
              >
                <UserCircle2 size={22} />
              </button>

              {/* Wake word toggle */}
              {wakeWordSupported && (
                <button
                  className={`p-4 rounded-full shadow-md border flex items-center justify-center transition-all backdrop-blur-md ${
                    wakeWordActive
                      ? 'bg-green-700 border-green-600 text-green-200'
                      : 'bg-gray-800/80 border-gray-700 text-gray-300 hover:bg-gray-700 hover:text-white'
                  }`}
                  onClick={() => {
                    const next = !wakeWordEnabled;
                    setWakeWordEnabled(next);
                    setWakeWordActive(next);
                  }}
                  title={wakeWordActive ? 'Disable wake word' : 'Enable wake word ("Hey Assistant")'}
                  aria-label="Toggle wake word detection"
                >
                  <Radio size={22} />
                </button>
              )}

              {/* Room customiser */}
              <RoomCustomizer
                activeItems={activeRoomItems}
                onActiveItemsChange={setActiveRoomItems}
              />
            </div>
          </div>

          <MiniChat
            isOpen={isMiniChatOpen}
            onClose={() => setIsMiniChatOpen(false)}
            messages={messages}
            onSendMessage={handleUserMessage}
            isProcessing={isProcessing}
          />
        </div>

      </div>

      {/* Voice Profile Training modal */}
      <VoiceProfileModal
        isOpen={isVoiceProfileOpen}
        onClose={() => setIsVoiceProfileOpen(false)}
      />
    </motion.div>
  );
};

export default VoiceRoomPage;
