import { useState } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Send, Paperclip, Camera } from 'lucide-react';
import { Message } from '../../types/chat';
import ttsService from '../../services/ttsService';

interface MiniChatProps {
  isOpen: boolean;
  onClose: () => void;
}

const initialMessages: Message[] = [
  {
    id: '1',
    sender: 'ai',
    text: 'You can also type messages here if you prefer.',
    timestamp: new Date().toISOString()
  }
];

const MiniChat = ({ isOpen, onClose }: MiniChatProps) => {
  const [messages, setMessages] = useState<Message[]>(initialMessages);
  const [inputText, setInputText] = useState('');

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim()) return;

    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: inputText,
      timestamp: new Date().toISOString()
    };

    setMessages(prev => [...prev, userMessage]);
    setInputText('');

    // Simulate AI response
    setTimeout(async () => {
      const aiMessage: Message = {
        id: (Date.now() + 1).toString(),
        sender: 'ai',
        text: 'I received your message. This is a placeholder response.',
        timestamp: new Date().toISOString()
      };
      setMessages(prev => [...prev, aiMessage]);
      try {
        const blob = await ttsService.synthesize(aiMessage.text);
        const url = URL.createObjectURL(blob);
        new Audio(url).play();
      } catch (err) {
        console.error('TTS failed', err);
      }
    }, 1000);
  };
  
  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div 
          className="absolute bottom-0 right-0 w-full sm:w-96 h-96 bg-white shadow-hard rounded-t-lg sm:rounded-lg sm:bottom-8 sm:right-8 overflow-hidden"
          initial={{ y: "100%", opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          exit={{ y: "100%", opacity: 0 }}
          transition={{ type: "spring", damping: 25, stiffness: 300 }}
        >
          <div className="flex items-center justify-between px-4 py-2 border-b bg-gray-50">
            <h3 className="text-sm font-medium text-gray-700">Chat</h3>
            <button 
              onClick={onClose}
              className="p-1 text-gray-500 hover:text-gray-700 rounded-full hover:bg-gray-100"
            >
              <X size={18} />
            </button>
          </div>
          
          <div className="h-[calc(100%-88px)] overflow-y-auto p-3 space-y-2">
            {messages.map(message => (
              <div 
                key={message.id} 
                className={`text-sm p-2 rounded-lg max-w-[85%] ${
                  message.sender === 'ai'
                    ? 'bg-gray-100 text-gray-800 mr-auto'
                    : 'bg-primary-100 text-primary-800 ml-auto'
                }`}
              >
                {message.text}
              </div>
            ))}
          </div>
          
          <form onSubmit={handleSubmit} className="p-2 border-t bg-white">
            <div className="flex items-center space-x-1">
              <button 
                type="button"
                className="p-1.5 text-gray-500 hover:text-primary-500 focus:outline-none"
              >
                <Paperclip size={16} />
              </button>
              <button 
                type="button"
                className="p-1.5 text-gray-500 hover:text-primary-500 focus:outline-none"
              >
                <Camera size={16} />
              </button>
              <input
                type="text"
                placeholder="Type a message..."
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                className="flex-grow px-3 py-1.5 border border-gray-300 rounded-full text-sm focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-transparent"
              />
              <button
                type="submit"
                className="p-1.5 text-primary-500 hover:text-primary-600 focus:outline-none"
              >
                <Send size={16} />
              </button>
            </div>
          </form>
        </motion.div>
      )}
    </AnimatePresence>
  );
};

export default MiniChat;