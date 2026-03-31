import { useState, useRef, useEffect } from 'react';
import { motion, AnimatePresence } from 'framer-motion';
import { X, Send, Loader2 } from 'lucide-react';
import { Message } from '../../types/chat';

interface MiniChatProps {
  isOpen: boolean;
  onClose: () => void;
  messages: Message[];
  onSendMessage: (text: string) => Promise<void>;
  isProcessing: boolean;
}

const MiniChat = ({ isOpen, onClose, messages, onSendMessage, isProcessing }: MiniChatProps) => {
  const [inputText, setInputText] = useState('');
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages, isProcessing]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim() || isProcessing) return;

    const currentText = inputText;
    setInputText('');
    await onSendMessage(currentText);
  };

  return (
    <AnimatePresence>
      {isOpen && (
        <motion.div
          className="absolute bottom-4 sm:bottom-8 right-4 sm:right-8 w-[calc(100%-2rem)] sm:w-96 h-96 bg-gray-900 border border-gray-700 shadow-2xl shadow-black/50 rounded-xl overflow-hidden z-50 flex flex-col"
          initial={{ y: 50, opacity: 0, scale: 0.95 }}
          animate={{ y: 0, opacity: 1, scale: 1 }}
          exit={{ y: 50, opacity: 0, scale: 0.95 }}
          transition={{ type: 'spring', damping: 25, stiffness: 300 }}
        >
          <div className="flex items-center justify-between px-4 py-3 border-b border-gray-700 bg-gray-800">
            <h3 className="text-sm font-medium text-gray-200">Text Fallback</h3>
            <button
              onClick={onClose}
              className="p-1.5 text-gray-400 hover:text-white rounded-full hover:bg-gray-700 transition-colors"
            >
              <X size={16} />
            </button>
          </div>

          <div className="flex-grow overflow-y-auto p-4 space-y-4 bg-gray-900/50">
            {messages.length === 0 ? (
              <div className="text-center text-gray-500 text-sm mt-10">
                You can also type messages here.<br/>They will be spoken back to you.
              </div>
            ) : (
              messages.map(message => (
                <div
                  key={message.id}
                  className={`flex flex-col ${message.sender === 'user' ? 'items-end' : 'items-start'}`}
                >
                  <div
                    className={`text-sm px-4 py-2 rounded-2xl max-w-[85%] ${
                      message.sender === 'ai'
                        ? message.isError
                          ? 'bg-red-900/50 text-red-200 border border-red-800/50'
                          : 'bg-gray-800 text-gray-200 border border-gray-700 rounded-tl-sm'
                        : 'bg-primary-600 text-white rounded-tr-sm shadow-md'
                    }`}
                  >
                    {message.text}
                  </div>
                </div>
              ))
            )}

            {isProcessing && (
              <div className="flex items-start">
                <div className="bg-gray-800 text-gray-400 border border-gray-700 px-4 py-2 rounded-2xl rounded-tl-sm flex items-center gap-2">
                  <Loader2 size={14} className="animate-spin" />
                  <span className="text-xs">Processing...</span>
                </div>
              </div>
            )}
            <div ref={messagesEndRef} />
          </div>

          <form onSubmit={handleSubmit} className="p-3 border-t border-gray-700 bg-gray-800">
            <div className="flex items-center gap-2">
              <input
                type="text"
                placeholder="Type your message..."
                value={inputText}
                onChange={e => setInputText(e.target.value)}
                disabled={isProcessing}
                className="flex-grow px-4 py-2 bg-gray-900 border border-gray-600 rounded-full text-sm text-gray-200 focus:outline-none focus:ring-1 focus:ring-primary-500 focus:border-primary-500 placeholder-gray-500 disabled:opacity-50"
              />
              <button
                type="submit"
                disabled={isProcessing || !inputText.trim()}
                className="p-2 bg-primary-600 text-white rounded-full hover:bg-primary-500 focus:outline-none disabled:opacity-50 transition-colors"
              >
                <Send size={18} className="ml-0.5" />
              </button>
            </div>
          </form>
        </motion.div>
      )}
    </AnimatePresence>
  );
};

export default MiniChat;
