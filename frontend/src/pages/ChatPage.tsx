import React, { useState, useEffect, useRef } from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip, Camera, Mic, Plus } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';

import ChatMessage from '../components/chat/ChatMessage';
import ChatSidebar from '../components/chat/ChatSidebar';
import CharacterSelector from '../components/chat/CharacterSelector';
import BrainExplorer from '../components/BrainExplorer';

import chatService from '../services/chatService';
import conversationService from '../services/conversationService';
import apiService from '../services/apiService';
import { Message } from '../types/chat';
import { USER_ID, USER_NAME } from '../constants';
import { parseChatUrl, generateChatUrl, createDefaultChatUrl } from '../utils/chatUrls';

/**
 * ChatPage component manages the chat interface including message display,
 * input area, and integration with the AI chat service.
 */
const ChatPage = () => {
  // URL parameters and navigation
  const { sessionCharacter } = useParams<{ sessionCharacter?: string }>();
  const navigate = useNavigate();

  // Parse URL to get conversation and character info
  const urlInfo = parseChatUrl(sessionCharacter);

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
    id: urlInfo.conversationId || '',
    title: 'New Chat'
  });

  // State for selected character
  const [selectedCharacter, setSelectedCharacter] = useState<any>(null);

  // Reference to track if this is the first message in a new conversation
  const isFirstMessage = useRef(urlInfo.isNewConversation);

  // Use the known working user ID throughout the component
  const workingUserId = '42dfa1c0-c093-4f58-bb3e-cc83bbd6d249';

  // New aggregate character loading system for ChatPage
  useEffect(() => {
    const loadCharacterFromUrl = async () => {
      try {
        console.log('🔄 ChatPage: Starting aggregate character loading...');

        // Always start with default GLaDOS character for immediate availability
        const defaultGLaDOS = {
          id: 'default-glados',
          name: 'GLaDOS',
          description: 'Default AI assistant',
          personality: 'Sarcastic, intelligent, and slightly menacing AI from Portal',
          systemPrompt: 'You are GLaDOS, the AI from Portal. Be sarcastic, intelligent, and slightly menacing.',
          yamlProfile: '',
          userId: 'default'
        };

        let aggregatedCharacters = [];

        // Load global characters from the new endpoint
        try {
          console.log('🔍 ChatPage: Loading global characters...');
          const response = await fetch('/api/character/global');

          if (response.ok) {
            const globalCharacters = await response.json();

            if (Array.isArray(globalCharacters) && globalCharacters.length > 0) {
              aggregatedCharacters = globalCharacters;
              console.log(`✅ ChatPage: Loaded ${globalCharacters.length} global characters:`,
                globalCharacters.map(c => c.name));
            } else {
              console.log('❌ ChatPage: No global characters found');
            }
          } else {
            console.log(`❌ ChatPage: Failed to fetch global characters: ${response.status}`);
          }
        } catch (error) {
          console.log('❌ ChatPage: Error fetching global characters:', error);
        }

        // If no global characters found, provide default GLaDOS as fallback
        if (aggregatedCharacters.length === 0) {
          aggregatedCharacters = [defaultGLaDOS];
          console.log('✅ ChatPage: Using default GLaDOS character as fallback');
        }

        console.log(`🎉 ChatPage: Aggregate loading complete! Total characters: ${aggregatedCharacters.length}`);
        console.log(`📋 ChatPage: Final character list:`, aggregatedCharacters.map(c => `${c.name} (${c.id})`));

        // Handle character selection from URL
        if (urlInfo.characterName) {
          const character = aggregatedCharacters.find(c =>
            c.name.toLowerCase() === urlInfo.characterName.toLowerCase()
          );

          if (character) {
            setSelectedCharacter(character);
            console.log('✅ ChatPage: Loaded character from URL:', character.name, 'ID:', character.id);
            return;
          } else {
            console.warn('⚠️ ChatPage: Character not found in URL:', urlInfo.characterName);
          }
        }

        // Fallback: use first available character (GLaDOS)
        if (aggregatedCharacters.length > 0) {
          const defaultCharacter = aggregatedCharacters[0]; // This will be GLaDOS
          setSelectedCharacter(defaultCharacter);

          // Update URL to include the default character
          const newUrl = generateChatUrl({
            conversationId: urlInfo.conversationId || 'new',
            characterName: defaultCharacter.name
          });
          navigate(newUrl, { replace: true });
          console.log('✅ ChatPage: Using default character:', defaultCharacter.name);
        }

      } catch (error) {
        console.error('💥 ChatPage: Critical error in aggregate loading:', error);

        // Ultimate fallback - just GLaDOS
        const fallbackGLaDOS = {
          id: 'fallback-glados',
          name: 'GLaDOS',
          description: 'Fallback AI assistant',
          personality: 'Sarcastic, intelligent, and slightly menacing AI from Portal',
          systemPrompt: 'You are GLaDOS, the AI from Portal. Be sarcastic, intelligent, and slightly menacing.',
          yamlProfile: '',
          userId: 'fallback'
        };

        setSelectedCharacter(fallbackGLaDOS);
        navigate(createDefaultChatUrl(), { replace: true });
        console.log('🆘 ChatPage: Using ultimate fallback GLaDOS character');
      }
    };

    loadCharacterFromUrl();
  }, [urlInfo.characterName, urlInfo.conversationId]); // Only re-run when URL parameters change

  // Load the most recent conversation or start a new one
  useEffect(() => {
    const loadConversation = async () => {
      try {
        console.log('🔄 ChatPage: Loading conversation for user:', workingUserId);

        // Load recent conversation for this user
        const recent = await conversationService.getRecentConversation(workingUserId);

        if (recent && recent.id) {
          // Load existing conversation
          setCurrentConversation({
            id: recent.id as string,
            title: recent.title
          });

          // Load messages for this conversation
          const conversationMessages = await conversationService.getMessages(recent.id);

          // Convert to the Message format used by the UI - ensure it's an array
          const messagesArray = Array.isArray(conversationMessages) ? conversationMessages : [];
          const formattedMessages = messagesArray.map(msg => ({
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
  const handleCharacterSelect = async (character: any) => {
    setSelectedCharacter(character);

    // Update URL to reflect character selection
    if (character) {
      const newUrl = generateChatUrl({
        conversationId: currentConversation.id || 'new',
        characterName: character.name
      });
      navigate(newUrl, { replace: true });
      console.log('Updated URL for character selection:', character.name);
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

      // Update URL for new conversation while keeping character
      const newUrl = generateChatUrl({
        conversationId: 'new',
        characterName: selectedCharacter?.name
      });
      navigate(newUrl, { replace: true });

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

      // Convert to the Message format used by the UI - ensure it's an array
      const messagesArray = Array.isArray(conversationMessages) ? conversationMessages : [];
      const formattedMessages = messagesArray.map(msg => ({
        id: msg.id,
        sender: msg.role === 'user' ? 'user' : (msg.role === 'assistant' ? 'ai' : 'system') as 'user' | 'ai' | 'system',
        text: msg.content,
        timestamp: msg.timestamp
      }));

      setMessages(formattedMessages);
      isFirstMessage.current = false;

      // Update URL to reflect conversation selection
      const newUrl = generateChatUrl({
        conversationId: conversationId,
        characterName: selectedCharacter?.name
      });
      navigate(newUrl, { replace: true });

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
        const newConversation = await conversationService.createConversation(workingUserId, title);

        conversationId = newConversation.id; // Use this right away
        setCurrentConversation({
          id: newConversation.id,
          title: newConversation.title
        });

        // Update URL to reflect new conversation
        const newUrl = generateChatUrl({
          conversationId: newConversation.id,
          characterName: selectedCharacter?.name
        });
        navigate(newUrl, { replace: true });

        // Set character context if we have a selected character
        if (selectedCharacter && selectedCharacter.systemPrompt) {
          try {
            await conversationService.setCharacterContext(
              conversationId,
              workingUserId,
              selectedCharacter.id || null,
              selectedCharacter.systemPrompt
            );
            console.log(`Set character context for conversation: ${selectedCharacter.name}`);
          } catch (error) {
            console.error('Error setting character context:', error);
          }
        }

        isFirstMessage.current = false;
      }      if (!conversationId) {
        throw new Error('No conversation ID available');
      }

      // Send message to API and get AI response (include character ID if selected)
      // Note: The backend handles storing both user and AI messages in the database
      console.log('ChatPage: Sending message with character ID:', selectedCharacter?.id, 'Character name:', selectedCharacter?.name);
      const aiResponse = await chatService.sendMessage(
        conversationId,
        workingUserId,
        userMessage.text,
        selectedCharacter?.id || null
      );

      // Create AI message object
      const aiMessage: Message = {
        id: Date.now().toString(),
        sender: 'ai',
        text: aiResponse,
        timestamp: new Date().toISOString()
      };      // Add AI message to messages
      setMessages(prev => [...prev, aiMessage]);
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
            userId={workingUserId}
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
