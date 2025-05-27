import React, { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip, Camera, Mic, Plus } from 'lucide-react';

import ChatMessage from '../components/chat/ChatMessage';
import ChatSidebar from '../components/chat/ChatSidebar';
import CharacterSelector from '../components/chat/CharacterSelector';
import BrainExplorer from '../components/BrainExplorer';

import chatService from '../services/chatService';
import conversationService from '../services/conversationService';
import { Message } from '../types/chat';
import { USER_ID, USER_NAME } from '../constants';

/**
 * ChatPage component manages the chat interface including message display,
 * input area, and integration with the AI chat service.
 */
const ChatPage = () => {
  // Local state for the input text
  const [inputText, setInputText] = useState<string>('');

  // State for loading status
  const [isLoading, setIsLoading] = useState(false);

  // State for messages in the current conversation
  const [messages, setMessages] = useState<Message[]>([]);

  // State for current conversation
  const [currentConversation, setCurrentConversation] = useState<{
    id: string;
    title: string;
  }>({
    id: '',
    title: 'New Chat'
  });

  // State for selected character
  const [selectedCharacter, setSelectedCharacter] = useState<any>(null);

  // Reference to track if this is the first message in a new conversation
  const isFirstMessage = useRef(true);

  // Load the most recent conversation or start a new one
  useEffect(() => {
    const loadConversation = async () => {
      try {
        console.log('Using hard-coded user:', USER_ID);
        
        // Load recent conversation for this user
        const recent = await conversationService.getRecentConversation(USER_ID);

        if (recent && recent.id) {
          // Load existing conversation
          setCurrentConversation({
            id: recent.id as string,
            title: recent.title
          });

          // Load messages for this conversation
          const conversationMessages = await conversationService.getMessages(recent.id);

          // Convert to the Message format used by the UI
          const formattedMessages = conversationMessages.map(msg => ({
            id: msg.id,
            sender: msg.role === 'user' ? 'user' : (msg.role === 'assistant' ? 'ai' : 'system') as 'user' | 'ai' | 'system',
            text: msg.content,
            timestamp: msg.timestamp
          }));

          setMessages(formattedMessages);
          isFirstMessage.current = false;
        } else {
          // Start with a new conversation
          setMessages([{
            id: '1',
            sender: 'ai',
            text: `Hello ${USER_NAME}! How can I help you today?`,
            timestamp: new Date().toISOString()
          }]);
          isFirstMessage.current = true;
        }
      } catch (error) {
        console.error('Error loading conversation, starting fresh:', error);
        // Start with a new conversation on error
        setMessages([{
          id: '1',
          sender: 'ai',
          text: `Hello ${USER_NAME}! How can I help you today?`,
          timestamp: new Date().toISOString()
        }]);
        isFirstMessage.current = true;
      }
    };

    loadConversation();
  }, []);

  // Handle character selection
  const handleCharacterSelect = (character: any) => {
    setSelectedCharacter(character);

    // Persist selection to localStorage
    if (character) {
      localStorage.setItem('selectedCharacterId', character.id);
    } else {
      localStorage.removeItem('selectedCharacterId');
    }
  };
  /**
   * Creates a new conversation
   */
  const handleNewConversation = async () => {
    try {
      // Reset the current state
      setMessages([{
        id: '1',
        sender: 'ai',
        text: `Hello ${USER_NAME}! How can I help you today?`,
        timestamp: new Date().toISOString()
      }]);

      setCurrentConversation({
        id: '',
        title: 'New Chat'
      });

      isFirstMessage.current = true;
      setInputText('');
    } catch (error) {
      console.error('Error creating new conversation:', error);
    }
  };

  /**
   * Selects an existing conversation
   */
  const handleSelectConversation = async (conversationId: string) => {
    try {
      // Get conversation details
      const conversation = await conversationService.getConversation(conversationId);

      setCurrentConversation({
        id: conversation.id,
        title: conversation.title
      });

      // Load messages for this conversation
      const conversationMessages = await conversationService.getMessages(conversationId);

      // Convert to the Message format used by the UI
      const formattedMessages = conversationMessages.map(msg => ({
        id: msg.id,
        sender: msg.role === 'user' ? 'user' : (msg.role === 'assistant' ? 'ai' : 'system') as 'user' | 'ai' | 'system',
        text: msg.content,
        timestamp: msg.timestamp
      }));

      setMessages(formattedMessages);
      isFirstMessage.current = false;

      // Update last open time
      await conversationService.updateLastOpenTime(conversationId);
    } catch (error) {
      console.error(`Error selecting conversation ${conversationId}:`, error);
    }
  };

  /**
   * Handles form submission to send a chat message.
   * @param e Form event
   */
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!inputText.trim() || isLoading) return;

    // Create a user message object
    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: inputText,
      timestamp: new Date().toISOString()
    };

    // Add user message to messages
    setMessages(prev => [...prev, userMessage]);

    // Clear the input field
    setInputText('');

    // Set loading state
    setIsLoading(true);

    try {
      // Always use a local variable for the conversation ID
      let conversationId = currentConversation.id;

      // If this is the first message in a new conversation, create the conversation first
      if (isFirstMessage.current && !conversationId) {
        // Generate a title from the first message (truncate if too long)
        const title = userMessage.text.length > 30
          ? `${userMessage.text.substring(0, 30)}...`
          : userMessage.text;

        // Create a new conversation
        const newConversation = await conversationService.createConversation(USER_ID, title);

        conversationId = newConversation.id; // Use this right away
        setCurrentConversation({
          id: newConversation.id,
          title: newConversation.title
        });

        // Set character context if we have a selected character
        if (selectedCharacter && selectedCharacter.systemPrompt) {
          try {
            await conversationService.setCharacterContext(
              conversationId,
              USER_ID,
              selectedCharacter.id || null,
              selectedCharacter.systemPrompt
            );
            console.log(`Set character context for conversation: ${selectedCharacter.name}`);
          } catch (error) {
            console.error('Error setting character context:', error);
          }
        }

        isFirstMessage.current = false;
      }

      if (!conversationId) {
        throw new Error('No conversation ID available');
      }

      // Store the user message in the database
      await conversationService.appendMessage(
        conversationId,
        USER_ID,
        'user',
        userMessage.text
      );

      // Send message to API and get AI response (include character ID if selected)
      const aiResponse = await chatService.sendMessage(
        conversationId,
        USER_ID,
        inputText,
        selectedCharacter?.id || null
      );

      // Create AI message object
      const aiMessage: Message = {
        id: Date.now().toString(),
        sender: 'ai',
        text: aiResponse,
        timestamp: new Date().toISOString()
      };

      // Add AI message to messages
      setMessages(prev => [...prev, aiMessage]);

      // Store the AI response in the database
      await conversationService.appendMessage(
        conversationId,
        USER_ID,
        'assistant',
        aiResponse
      );
    } catch (error) {
      console.error('Error sending message:', error);

      // Add error message
      const errorMessage: Message = {
        id: Date.now().toString(),
        sender: 'system',
        text: 'Sorry, there was an error processing your message. Please try again.',
        timestamp: new Date().toISOString()
      };

      setMessages(prev => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <motion.div
      className="h-[calc(100vh-64px)] flex flex-col"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >      <div className="flex flex-grow overflow-hidden">
        {/* Chat Sidebar */}
        <div className="w-64 border-r">
          <ChatSidebar
            userId={USER_ID}
            currentSessionId={currentConversation.id || null}
            onSelectSession={handleSelectConversation}
            onNewSession={handleNewConversation}
          />
        </div>

        {/* Main Chat Area */}
        <div className="flex-grow flex flex-col overflow-hidden">
          <div className="px-4 py-2 bg-white border-b">
            <div className="flex justify-between items-center mb-2">
              <h1 className="text-xl font-semibold text-gray-800">{currentConversation.title}</h1>
              <button
                onClick={handleNewConversation}
                className="p-2 text-gray-500 hover:text-primary-500 focus:outline-none"
                title="New Chat"
              >
                <Plus size={20} />
              </button>
            </div>

            {/* Character Selector */}
            <div className="flex items-center justify-between">
              <CharacterSelector
                selectedCharacterId={selectedCharacter?.id || null}
                onCharacterSelect={handleCharacterSelect}
                disabled={false}
              />
              {selectedCharacter && (
                <div className="text-sm text-blue-600 flex items-center">
                  <span className="w-2 h-2 bg-blue-500 rounded-full mr-2"></span>
                  Active: {selectedCharacter.name}
                </div>
              )}
            </div>
          </div>

          <div className="flex-grow overflow-y-auto p-4 space-y-4">
            {messages.map((message: Message) => (
              <ChatMessage key={message.id} message={message} />
            ))}
            {isLoading && (
              <div className="flex items-center justify-center p-2">
                <div className="animate-pulse text-gray-500">AI is thinking...</div>
              </div>
            )}
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
                disabled={!inputText.trim() || isLoading}
              >
                <Send size={20} />
              </button>
            </div>
          </form>
        </div>

        {/* Tools Sidebar */}
        <div className="hidden lg:block w-80 border-l bg-white">
          <div className="p-4 border-b">
            <h2 className="text-lg font-medium text-gray-800">Tools</h2>
          </div>
          {/* Brain explorer UI */}
          <BrainExplorer />
          <div className="p-4 space-y-4">
            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Files</h3>
              <p className="text-sm text-gray-500 mt-1">
                Drag and drop files here to upload
              </p>              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
                <Paperclip size={16} className="mr-2" />
                Upload Files
              </button>
            </div>

            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Voice</h3>
              <p className="text-sm text-gray-500 mt-1">
                Record a voice message
              </p>              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
                <Mic size={16} className="mr-2" />
                Start Recording
              </button>
            </div>

            <div className="card">
              <h3 className="text-sm font-medium text-gray-700">Camera</h3>
              <p className="text-sm text-gray-500 mt-1">
                Take a photo or video
              </p>              <button className="btn btn-ghost text-sm w-full mt-2 border border-dashed border-gray-300">
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
