import { useState } from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip, Camera, Mic } from 'lucide-react';
import ChatMessage from '../components/chat/ChatMessage';
import ChatInput from '../components/chat/ChatInput';
import { Message } from '../types/chat';
import { useChatHub } from '../hooks/useChatHub';

const initialMessages: Message[] = [
  {
    id: '1',
    sender: 'ai',
    text: 'Hello! How can I help you today?',
    timestamp: new Date().toISOString()
  }
];

/**
 * ChatPage component manages the chat interface including message display,
 * input area, and integration with SignalR for real-time messaging.
 */
const ChatPage = () => {
  // Use the custom hook to manage SignalR chat connection and messages
  const { messages, sendMessage } = useChatHub();

  // Local state for the input text
  const [inputText, setInputText] = useState<string>('');

  /**
   * Handles form submission to send a chat message.
   * @param e Form event
   */
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim()) return;

    // Create a user message object
    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: inputText,
      timestamp: new Date().toISOString()
    };

    // Update local message list immediately for responsiveness
    // Note: messages state is managed by useChatHub, so no local setMessages here
    // The message will be added when SignalR broadcasts it back

    // Clear the input field
    setInputText('');

    // Send message via SignalR
    sendMessage('user', inputText);
  };

  return (
    <motion.div
      className="h-[calc(100vh-64px)] flex flex-col"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="flex flex-grow overflow-hidden">
        {/* Main Chat Area */}
        <div className="flex-grow flex flex-col overflow-hidden">
          <div className="px-4 py-2 bg-white border-b">
            <h1 className="text-xl font-semibold text-gray-800">Chat</h1>
          </div>

          <div className="flex-grow overflow-y-auto p-4 space-y-4">
            {messages.map(message => (
              <ChatMessage key={message.id} message={message} />
            ))}
          </div>

          <form onSubmit={handleSubmit} className="p-4 border-t bg-white">
            <div className="flex items-center space-x-2">
              <input
                type="text"
                placeholder="Type your message..."
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                className="flex-grow px-4 py-2 border border-gray-300 rounded-full focus:outline-none focus:ring-2 focus:ring-primary-500 focus:border-transparent"
              />
              <button
                type="button"
                className="p-2 text-gray-500 hover:text-primary-500 focus:outline-none"
              >
                <Paperclip size={20} />
              </button>
              <button
                type="button"
                className="p-2 text-gray-500 hover:text-primary-500 focus:outline-none"
              >
                <Camera size={20} />
              </button>
              <button
                type="button"
                className="p-2 text-gray-500 hover:text-primary-500 focus:outline-none"
              >
                <Mic size={20} />
              </button>
              <button
                type="submit"
                className="p-2 bg-primary-500 text-white rounded-full hover:bg-primary-600 focus:outline-none focus:ring-2 focus:ring-primary-500 focus:ring-offset-2"
              >
                <Send size={20} />
              </button>
            </div>
          </form>
        </div>

        {/* Sidebar */}
        <div className="hidden lg:block w-80 border-l bg-white">
          <div className="p-4 border-b">
            <h2 className="text-lg font-medium text-gray-800">Tools</h2>
          </div>
          <div className="p-4 space-y-4">
            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Files</h3>
              <p className="text-sm text-gray-500 mt-1">
                Drag and drop files here to upload
              </p>
              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
                <Paperclip size={16} className="mr-2" />
                Upload Files
              </button>
            </div>

            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Voice</h3>
              <p className="text-sm text-gray-500 mt-1">
                Record a voice message
              </p>
              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
                <Mic size={16} className="mr-2" />
                Start Recording
              </button>
            </div>

            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Camera</h3>
              <p className="text-sm text-gray-500 mt-1">
                Take a photo or video
              </p>
              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
                <Camera size={16} className="mr-2" />
                Open Camera
              </button>
            </div>
          </div>
        </div>
      </div>
    </motion.div>
  );
};


export default ChatPage;
