import { Play, User } from 'lucide-react';
import { motion } from 'framer-motion';
import { Message } from '../../types/chat';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';

interface ChatMessageProps {
  message: Message;
  characterImage?: string;
}

const ChatMessage = ({ message, characterImage }: ChatMessageProps) => {
  const isAI = message.sender === 'ai';
  
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
            characterImage ? (
              <img
                src={characterImage}
                alt="Character"
                className="w-8 h-8 rounded-full object-cover"
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
            <span>
              {new Date(message.timestamp).toLocaleTimeString([], { 
                hour: '2-digit', 
                minute: '2-digit' 
              })}
            </span>
            
            {isAI && (
              <button className="ml-2 flex items-center text-xs text-primary-600 hover:text-primary-700">
                <Play size={12} className="mr-1" />
                Play
              </button>
            )}
          </div>
        </div>
      </div>
    </motion.div>
  );
};

export default ChatMessage;
