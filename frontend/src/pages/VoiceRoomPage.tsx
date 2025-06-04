import { useState } from 'react';
import { motion } from 'framer-motion';
import { Mic, Volume2, MessageSquare, Camera, Paperclip } from 'lucide-react';
import VoiceRoomAvatar from '../components/voice-room/VoiceRoomAvatar';
import MiniChat from '../components/voice-room/MiniChat';
import { playTts } from '../utils/playTts';

const VoiceRoomPage = () => {
  const [isMiniChatOpen, setIsMiniChatOpen] = useState(false);
  const [isListening, setIsListening] = useState(false);

  const playSampleTts = async () => {
    try {
      const response = await fetch('/api/tts?text=Hello');
      if (!response.ok) {
        throw new Error('Failed to fetch TTS');
      }
      const blob = await response.blob();
      await playTts(blob);
    } catch (err) {
      console.error('Error fetching TTS:', err);
      alert('Unable to play TTS.');
    }
  };
  
  const toggleListening = () => {
    setIsListening(!isListening);
  };
  
  const toggleMiniChat = () => {
    setIsMiniChatOpen(!isMiniChatOpen);
  };

  return (
    <motion.div
      className="min-h-[calc(100vh-64px)] bg-gradient-to-b from-primary-50 to-primary-100 p-4"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="max-w-5xl mx-auto h-full flex flex-col">
        <div className="text-center mb-4">
          <h1 className="text-2xl font-medium text-gray-800">Voice Room</h1>
          <p className="text-gray-600">Speak with your AI assistant</p>
        </div>
        
        <div className="flex-grow bg-white rounded-lg shadow-medium overflow-hidden relative">
          {/* Room Canvas */}
          <div className="w-full h-full min-h-[500px] p-6 relative">
            <VoiceRoomAvatar isListening={isListening} />
            
            {/* Control Buttons */}
            <div className="absolute bottom-8 left-0 right-0 flex justify-center space-x-6">
              <button
                className={`p-4 rounded-full shadow-md flex items-center justify-center transition-all ${
                  isListening 
                    ? 'bg-error-500 text-white animate-pulse' 
                    : 'bg-white text-gray-700 hover:bg-gray-50'
                }`}
                onClick={toggleListening}
              >
                <Mic size={24} />
              </button>
              
              <button
                className="p-4 rounded-full shadow-md bg-white text-gray-700 hover:bg-gray-50 flex items-center justify-center transition-all"
                onClick={playSampleTts}
              >
                <Volume2 size={24} />
              </button>
              
              <button
                className="p-4 rounded-full shadow-md bg-white text-gray-700 hover:bg-gray-50 flex items-center justify-center transition-all"
                onClick={toggleMiniChat}
              >
                <MessageSquare size={24} />
              </button>
            </div>
          </div>
          
          {/* Mini Chat */}
          <MiniChat isOpen={isMiniChatOpen} onClose={() => setIsMiniChatOpen(false)} />
        </div>
        
        {/* Quick Action Buttons */}
        <div className="mt-4 flex justify-center space-x-4">
          <button className="btn btn-ghost text-sm">
            <Camera size={16} className="mr-2" />
            Camera
          </button>
          <button className="btn btn-ghost text-sm">
            <Paperclip size={16} className="mr-2" />
            Upload
          </button>
        </div>
      </div>
    </motion.div>
  );
};

export default VoiceRoomPage;