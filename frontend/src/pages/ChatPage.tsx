import React, {
  useState,
  useEffect,
  useRef,
  useCallback,
  FormEvent
} from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip, Camera, Mic, Plus } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';

import ChatMessage from '../components/chat/ChatMessage';
import ChatSidebar from '../components/chat/ChatSidebar';
import CharacterSelector from '../components/chat/CharacterSelector';
import VoiceSelector from '../components/chat/VoiceSelector';
import BrainExplorer from '../components/BrainExplorer';

import chatService from '../services/chatService';
import conversationService from '../services/conversationService';
import apiService from '../services/apiService';
import { Message } from '../types/chat';
import { USER_NAME } from '../constants';
import {
  parseChatUrl,
  generateChatUrl,
  createDefaultChatUrl
} from '../utils/chatUrls';
import { useInitialization } from '../contexts/InitializationContext';

/* -------------------------------------------------------------------------- */
/*  Helper types                                                              */
/* -------------------------------------------------------------------------- */

interface Character {
  id: string;
  name: string;
  systemPrompt?: string;
  imagePath?: string;
}

interface LlmOption {
  value: string;
  label: string;
  engine: string | null;
  model: string | null;
}

interface ConversationMeta {
  id: string;
  title: string;
}

/* -------------------------------------------------------------------------- */

const ChatPage: React.FC = () => {
  /* --------------------------- context / routing ------------------------- */
  const { user } = useInitialization();
  const { sessionCharacter } = useParams<{ sessionCharacter?: string }>();
  const navigate = useNavigate();
  const urlInfo = parseChatUrl(sessionCharacter);

  /* ------------------------------- state --------------------------------- */
  const [inputText, setInputText] = useState('');
  const [isLoading, setIsLoading] = useState(false);
  const [messages, setMessages] = useState<Message[]>([]);
  const [currentConversation, setCurrentConversation] =
    useState<ConversationMeta>({
      id: urlInfo.conversationId || '',
      title: 'New Chat'
    });

  const [selectedCharacter, setSelectedCharacter] = useState<Character | null>(
    null
  );
  const [showCharacterImages, setShowCharacterImages] = useState<boolean>(() => {
    const stored = localStorage.getItem('showCharacterImages');
    return stored ? stored === 'true' : true;
  });

  const [chatEngineOverride, setChatEngineOverride] =
    useState<string | null>(null);
  const [chatModelOverride, setChatModelOverride] =
    useState<string | null>(null);
  const [ttsProvider, setTtsProvider] = useState<string | null>(null);
  const [ttsVoiceId, setTtsVoiceId] = useState<string | null>(null);
  const [availableLlms, setAvailableLlms] = useState<LlmOption[]>([]);

  /* ------------------------------ refs ----------------------------------- */
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const isFirstMessage = useRef(urlInfo.isNewConversation);

  /* -------------------------- persist prefs ------------------------------ */
  useEffect(() => {
    localStorage.setItem(
      'showCharacterImages',
      showCharacterImages ? 'true' : 'false'
    );
  }, [showCharacterImages]);

  /* ---------------------- SYNC FROM DASHBOARD LLM ------------------------ */
  useEffect(() => {
    const syncAllChatSettings = async () => {
      if (!user?.id) return;

      try {
        const settings = await chatService.getChatSettings(user.id);
        console.log('🔄 Chat: Syncing all chat settings from backend:', settings);

        setChatEngineOverride(settings.llmEngine || null);
        setChatModelOverride(settings.llmModel || null);
        setTtsProvider(settings.ttsProvider || null);
        setTtsVoiceId(settings.ttsVoiceId || null);

        console.log('🔄 Chat: Chat settings synced → LLM:', settings.llmEngine, settings.llmModel, 'TTS:', settings.ttsProvider, settings.ttsVoiceId);
      } catch (err) {
        console.error('Chat settings sync failed:', err);
        // Fallback or default settings if preferred
        setChatEngineOverride(null); // Default LLM
        setChatModelOverride(null);
        setTtsProvider('fishspeech'); // Default TTS
        setTtsVoiceId('glados');
      }
    };

    syncAllChatSettings();
  }, [user?.id]); // Only run once when user loads

  /* ----------------------- auto-scroll on change ------------------------- */
  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, isLoading]);

  /* ------------------ character bootstrap from URL ---------------------- */
  useEffect(() => {
    const bootstrapCharacter = async () => {
      if (!user?.id) return;

      try {
        const characters: Character[] = await apiService.get(
          `/api/character/user/${user.id}`
        );
        console.log('🔄 Chat: Loaded characters:', characters);

        let character: Character | null = null;

        /* 1️⃣ URL param */
        if (urlInfo.characterName) {
          character =
            characters.find(
              (c) =>
                c.name.toLowerCase() === urlInfo.characterName!.toLowerCase()
            ) || null;
        }

        /* 2️⃣ LocalStorage */
        if (!character) {
          const storedId = localStorage.getItem('selectedCharacterId');
          if (storedId) {
            character = characters.find((c) => c.id === storedId) || null;
          }
        }

        /* 3️⃣ Persisted default */
        if (!character) {
          try {
            const res = await fetch(
              `/api/settings/DefaultCharacterId?userId=${user.id}`
            );
            if (res.ok) {
              const { value } = (await res.json()) as { value: string };
              character = characters.find((c) => c.id === value) || null;
            }
          } catch {
            /* ignore */
          }
        }

        /* 4️⃣ Fallback */
        if (!character && characters.length) character = characters[0];

        if (character) {
          setSelectedCharacter(character);
          const newUrl = generateChatUrl({
            conversationId: urlInfo.conversationId || 'new',
            characterName: character.name
          });
          navigate(newUrl, { replace: true });
        } else {
          navigate(createDefaultChatUrl(), { replace: true });
        }
      } catch (err) {
        console.error('Character bootstrap error:', err);
        navigate(createDefaultChatUrl(), { replace: true });
      }
    };

    bootstrapCharacter();
  }, [
    user?.id,
    navigate,
    urlInfo.characterName,
    urlInfo.conversationId,
    sessionCharacter
  ]);

  /* ---------------------------- Load saved LLM options from database --------------------------- */
  useEffect(() => {
    const loadSavedLlmOptions = async () => {
      if (!user?.id) return;

      try {
        // Get saved settings from database
        const settings = await chatService.getChatSettings(user.id);
        console.log('🔄 Chat: Loaded saved settings from database:', settings);

        // Create LLM options based on saved settings only
        const llms: LlmOption[] = [
          { value: 'default', label: 'Default LLM', engine: null, model: null }
        ];

        // Add the currently saved configuration as the primary option
        if (settings.llmEngine && settings.llmModel) {
          llms.push({
            value: `${settings.llmEngine}:${settings.llmModel}`,
            label: `${settings.llmEngine.charAt(0).toUpperCase() + settings.llmEngine.slice(1)}: ${settings.llmModel}`,
            engine: settings.llmEngine,
            model: settings.llmModel
          });
        } else if (settings.llmEngine) {
          llms.push({
            value: `${settings.llmEngine}:default`,
            label: `${settings.llmEngine.charAt(0).toUpperCase() + settings.llmEngine.slice(1)} (Default)`,
            engine: settings.llmEngine,
            model: null
          });
        }

        // Add other basic options for fallback
        ['ollama', 'lmstudio', 'openai', 'claude'].forEach(engine => {
          if (engine !== settings.llmEngine) {
            llms.push({
              value: `${engine}:default`,
              label: `${engine.charAt(0).toUpperCase() + engine.slice(1)} (Default)`,
              engine: engine,
              model: null
            });
          }
        });

        console.log('🔄 Chat: LLM options from saved settings:', llms);
        setAvailableLlms(llms);

      } catch (error) {
        console.error('🔄 Chat: Failed to load saved settings, using fallback options:', error);
        
        // Fallback options if database read fails
        const fallbackLlms: LlmOption[] = [
          { value: 'default', label: 'Default LLM', engine: null, model: null },
          { value: 'ollama:default', label: 'Ollama (Default)', engine: 'ollama', model: null },
          { value: 'lmstudio:default', label: 'LM Studio (Default)', engine: 'lmstudio', model: null },
          { value: 'openai:default', label: 'OpenAI (Default)', engine: 'openai', model: null },
          { value: 'claude:default', label: 'Claude (Default)', engine: 'claude', model: null }
        ];
        setAvailableLlms(fallbackLlms);
      }
    };

    loadSavedLlmOptions();
  }, [user?.id]); // Only run when user changes

  /* 🕵️‍♂️ Debug: track dropdown value + available list */
  useEffect(() => {
    const current = chatEngineOverride
      ? `${chatEngineOverride}:${chatModelOverride || 'default'}`
      : 'default';
    console.log(
      '🔄 Chat: Dropdown should show →',
      current,
      '| engine:',
      chatEngineOverride,
      '| model:',
      chatModelOverride
    );
    console.log(
      '🔄 Chat: Available options →',
      availableLlms.map((l) => l.value)
    );
  }, [chatEngineOverride, chatModelOverride, availableLlms]);

  /* ---------------------- load / resume conversation -------------------- */
  useEffect(() => {
    const loadConversation = async () => {
      if (!user?.id) return;

      try {
        const recent = await conversationService.getRecentConversation(
          user.id
        );

        if (recent?.id) {
          console.log('🔄 Chat: Resuming conversation', recent.id);
          setCurrentConversation({ id: recent.id, title: recent.title });
          const raw = await conversationService.getMessages(recent.id);

          const formatted = (Array.isArray(raw) ? raw : []).map(
            (m): Message => ({
              id: m.id,
              sender:
                m.role === 'user'
                  ? 'user'
                  : m.role === 'assistant'
                  ? 'ai'
                  : 'system',
              text: m.content,
              timestamp: m.timestamp
            })
          );

          setMessages(
            formatted.filter(
              (m, i, arr) => arr.findIndex((x) => x.id === m.id) === i
            )
          );
          isFirstMessage.current = false;
        } else {
          console.log('🔄 Chat: No recent conversation, starting fresh');
          setMessages([
            {
              id: 'welcome',
              sender: 'ai',
              text: `Hello ${USER_NAME}! How can I help you today?`,
              timestamp: new Date().toISOString()
            }
          ]);
          isFirstMessage.current = true;
        }
      } catch (err) {
        console.error('Conversation load error:', err);
      }
    };

    loadConversation();
  }, [user?.id]);

  /* ---------------------------------------------------------------------- */
  /*  Character select handler                                               */
  /* ---------------------------------------------------------------------- */

  const handleCharacterSelect = useCallback(
    async (character: Character | null) => {
      console.log('🔄 Chat: Character selected:', character);
      setSelectedCharacter(character);

      if (character) {
        localStorage.setItem('selectedCharacterId', character.id);
        if (user?.id) {
          fetch('/api/settings/DefaultCharacterId', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: user.id, value: character.id })
          }).catch(() => {});
        }
      } else {
        localStorage.removeItem('selectedCharacterId');
        if (user?.id) {
          fetch('/api/settings/DefaultCharacterId', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: user.id, value: '' })
          }).catch(() => {});
        }
      }

      navigate(
        generateChatUrl({
          conversationId: currentConversation.id || 'new',
          characterName: character?.name
        }),
        { replace: true }
      );
    },
    [user?.id, currentConversation.id, navigate]
  );

  /* ---------------------------------------------------------------------- */
  /*  New conversation                                                      */
  /* ---------------------------------------------------------------------- */

  const handleNewConversation = async () => {
    console.log('🔄 Chat: Starting new conversation');
    setMessages([
      {
        id: 'welcome',
        sender: 'ai',
        text: `Hello ${USER_NAME}! How can I help you today?`,
        timestamp: new Date().toISOString()
      }
    ]);

    setCurrentConversation({ id: '', title: 'New Chat' });

    navigate(
      generateChatUrl({
        conversationId: 'new',
        characterName: selectedCharacter?.name
      }),
      { replace: true }
    );

    isFirstMessage.current = true;
    setInputText('');
  };

  /* ---------------------------------------------------------------------- */
  /*  Select existing conversation                                          */
  /* ---------------------------------------------------------------------- */

  const handleSelectConversation = async (conversationId: string) => {
    try {
      console.log('🔄 Chat: Selecting conversation', conversationId);
      const convo = await conversationService.getConversation(conversationId);

      setCurrentConversation({ id: convo.id, title: convo.title });

      const raw = await conversationService.getMessages(conversationId);
      const formatted: Message[] = (Array.isArray(raw) ? raw : []).map((m) => ({
        id: m.id,
        sender:
          m.role === 'user'
            ? 'user'
            : m.role === 'assistant'
            ? 'ai'
            : 'system',
        text: m.content,
        timestamp: m.timestamp
      }));

      setMessages(
        formatted.filter(
          (m, i, arr) => arr.findIndex((x) => x.id === m.id) === i
        )
      );
      isFirstMessage.current = false;

      navigate(
        generateChatUrl({
          conversationId,
          characterName: selectedCharacter?.name
        }),
        { replace: true }
      );

      await conversationService.updateLastOpenTime(conversationId);
    } catch (err) {
      console.error('Select conversation error:', err);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  Submit message                                                        */
  /* ---------------------------------------------------------------------- */

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!inputText.trim() || isLoading) return;

    const textToSend = inputText.trim();

    const userMessage: Message = {
      id: Date.now().toString(),
      sender: 'user',
      text: textToSend,
      timestamp: new Date().toISOString()
    };

    setMessages((prev) => [...prev, userMessage]);
    setInputText('');
    setIsLoading(true);

    try {
      let conversationId = currentConversation.id;

      if (isFirstMessage.current && !conversationId) {
        const title =
          textToSend.length > 30 ? `${textToSend.slice(0, 30)}…` : textToSend;
        const newConvo = await conversationService.createConversation(title);

        console.log('🔄 Chat: Created conversation', newConvo.id);

        conversationId = newConvo.id;
        setCurrentConversation({ id: newConvo.id, title: newConvo.title });

        navigate(
          generateChatUrl({
            conversationId,
            characterName: selectedCharacter?.name
          }),
          { replace: true }
        );

        if (selectedCharacter?.systemPrompt && user?.id) {
          await conversationService.setCharacterContext(
            conversationId,
            user.id,
            selectedCharacter.id,
            selectedCharacter.systemPrompt
          );
          console.log(
            `🔄 Chat: Set character context → ${selectedCharacter.name}`
          );
        }

        isFirstMessage.current = false;
      }      if (!conversationId) throw new Error('No conversation ID generated');

      console.log('🔍 Chat: About to append message', {
        conversationId,
        userId: user?.id,
        messageType: 'user',
        messageContent: textToSend
      });

      if (user?.id) {
        await conversationService.appendMessage(
          conversationId,
          user.id,
          'user',
          textToSend
        );
      }

      /* ---------------- send to model ---------------- */
      const aiResponse = await chatService.sendMessage(
        conversationId,
        textToSend,
        selectedCharacter?.id || null,
        chatEngineOverride,
        chatModelOverride
      );

      console.log('🔄 Chat: AI response received:', aiResponse);

      const aiMessage: Message = {
        id: Date.now().toString(),
        sender: 'ai',
        text: aiResponse,
        timestamp: new Date().toISOString()
      };      setMessages((prev) => [...prev, aiMessage]);

      console.log('🔍 Chat: About to append AI message', {
        conversationId,
        userId: user?.id,
        messageType: 'assistant',
        messageContent: aiResponse
      });

      if (user?.id) {
        await conversationService.appendMessage(
          conversationId,
          user.id,
          'assistant',
          aiResponse
        );
      }
    } catch (err) {
      console.error('Chat send error:', err);
      setMessages((prev) => [
        ...prev,
        {
          id: Date.now().toString(),
          sender: 'system',
          text: 'Sorry, something went wrong. Please try again.',
          timestamp: new Date().toISOString()
        }
      ]);
    } finally {
      setIsLoading(false);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  JSX                                                                   */
  /* ---------------------------------------------------------------------- */

  return (
    <motion.div
      className="h-[calc(100vh-64px)] flex flex-col"
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      transition={{ duration: 0.3 }}
    >
      <div className="flex flex-grow overflow-hidden">
        {/* --------------- LEFT SIDEBAR (sessions) ----------------------- */}
        <div className="w-64 border-r">
          <ChatSidebar
            userId={user?.id || ''}
            currentSessionId={currentConversation.id || null}
            onSelectSession={handleSelectConversation}
            onNewSession={handleNewConversation}
          />
        </div>

        {/* --------------- MAIN CHAT AREA -------------------------------- */}
        <div className="flex-grow flex flex-col overflow-hidden">
          {/* header */}
          <div className="px-4 py-2 bg-white border-b">
            <div className="flex justify-between items-center mb-2">
              <h1 className="text-xl font-semibold text-gray-800">
                {currentConversation.title}
              </h1>
              <button
                onClick={handleNewConversation}
                className="p-2 text-gray-500 hover:text-primary-500"
                title="New Chat"
              >
                <Plus size={20} />
              </button>
            </div>

            <div className="flex flex-wrap items-center gap-4">
              {/* character */}
              <CharacterSelector
                selectedCharacterId={selectedCharacter?.id || null}
                onCharacterSelect={handleCharacterSelect}
              />
              {selectedCharacter && (
                <span className="flex items-center text-sm text-blue-600">
                  <span className="w-2 h-2 bg-blue-500 rounded-full mr-2" />
                  Active: {selectedCharacter.name}
                </span>
              )}

              {/* toggle img */}
              <label className="text-sm flex items-center gap-1">
                <input
                  type="checkbox"
                  checked={showCharacterImages}
                  onChange={(e) => setShowCharacterImages(e.target.checked)}
                />
                <span>Use Image</span>
              </label>

              {/* voice selector */}
              <VoiceSelector
                disabled={isLoading}
                provider={ttsProvider}
                voiceId={ttsVoiceId}
                onSettingsChange={async (newProvider, newVoiceId) => {
                  if (!user?.id) return;
                  console.log('🔄 Chat: VoiceSelector changed TTS to Provider:', newProvider, 'VoiceID:', newVoiceId);
                  setTtsProvider(newProvider);
                  setTtsVoiceId(newVoiceId);
                  try {
                    await chatService.updateChatSettings(user.id, {
                      llmEngine: chatEngineOverride || '', // Pass current LLM engine or default
                      llmModel: chatModelOverride || '',   // Pass current LLM model or default
                      ttsProvider: newProvider,
                      ttsVoiceId: newVoiceId,
                    });
                    console.log('🔄 Chat: TTS settings updated successfully via chatService.');
                  } catch (error) {
                    console.error('Failed to update TTS settings via chatService:', error);
                  }
                }}
              />

              {/* LLM dropdown */}
              <select
                id="llm-override-select"
                value={
                  chatEngineOverride
                    ? `${chatEngineOverride}:${chatModelOverride || 'default'}`
                    : 'default'
                }
                onChange={async (e) => {
                  const sel = availableLlms.find(
                    (l) => l.value === e.target.value
                  );
                  if (!sel || !user?.id) {
                    console.warn('🔄 Chat: Selected LLM option not found or user ID missing:', e.target.value, user?.id);
                    return;
                  }

                  console.log('🔄 Chat: User changed LLM to:', sel);
                  const newEngine = sel.engine;
                  const newModel = sel.model;

                  setChatEngineOverride(newEngine);
                  setChatModelOverride(newModel);

                  try {
                    console.log('🔄 Chat: Updating all chat settings. New LLM:', newEngine, newModel, 'Current TTS:', ttsProvider, ttsVoiceId);
                    await chatService.updateChatSettings(user.id, {
                      llmEngine: newEngine || '', // Ensure empty string if null
                      llmModel: newModel || '',   // Ensure empty string if null
                      ttsProvider: ttsProvider || 'fishspeech', // Default if null
                      ttsVoiceId: ttsVoiceId || 'glados',     // Default if null
                    });

                    // Notify dashboard about LLM part of the change
                    window.dispatchEvent(
                      new CustomEvent('llmSettingsChanged', {
                        detail: { engine: newEngine, model: newModel }
                      })
                    );
                    console.log('🔄 Chat: Chat settings (including LLM) updated and Dashboard notified.');
                  } catch (err) {
                    console.error('Failed to update chat settings:', err);
                  }
                }}
                className="px-3 py-2 border rounded-lg text-sm disabled:opacity-50"
                disabled={isLoading || availableLlms.length === 0}
              >
                {availableLlms.length === 0 ? (
                  <option value="default">Loading models...</option>
                ) : (
                  availableLlms.map((l) => (
                    <option key={l.value} value={l.value}>
                      {l.label}
                    </option>
                  ))
                )}
              </select>
            </div>
          </div>

          {/* messages */}
          <div className="flex-grow overflow-y-auto p-4 space-y-4">
            {messages.map((m) => (
              <ChatMessage
                key={m.id}
                message={m}
                characterImage={
                  showCharacterImages && m.sender === 'ai' && selectedCharacter?.imagePath
                    ? selectedCharacter.imagePath.startsWith('http') || selectedCharacter.imagePath.startsWith('/')
                      ? selectedCharacter.imagePath
                      : `/${selectedCharacter.imagePath}`
                    : undefined
                }
              />
            ))}
            {isLoading && (
              <div className="flex justify-center text-gray-500 animate-pulse">
                AI is thinking…
              </div>
            )}
            <div ref={bottomRef} />
          </div>

          {/* composer */}
          <form onSubmit={handleSubmit} className="p-4 border-t bg-white">
            <div className="flex items-center gap-2">
              <input
                type="text"
                placeholder="Type your message…"
                value={inputText}
                onChange={(e) => setInputText(e.target.value)}
                className="flex-grow px-4 py-2 border rounded-full focus:ring"
              />
              <button type="button" className="p-2 text-gray-500">
                <Paperclip size={20} />
              </button>
              <button type="button" className="p-2 text-gray-500">
                <Camera size={20} />
              </button>
              <button type="button" className="p-2 text-gray-500">
                <Mic size={20} />
              </button>
              <button
                type="submit"
                className="p-2 bg-primary-500 text-white rounded-full disabled:opacity-50"
                disabled={!inputText.trim() || isLoading}
              >
                <Send size={20} />
              </button>
            </div>
          </form>
        </div>

        {/* --------------- RIGHT SIDEBAR (tools) ------------------------- */}
        <div className="hidden lg:block w-80 border-l bg-white">
          <div className="p-4 border-b">
            <h2 className="text-lg font-medium">Tools</h2>
          </div>

          <BrainExplorer />

          <div className="p-4 space-y-4">
            <div>
              <h3 className="text-sm font-medium">Files</h3>
              <p className="text-sm text-gray-500">
                Drag and drop files here to upload
              </p>
              <button className="btn btn-ghost w-full mt-2 border-dashed">
                <Paperclip size={16} className="mr-2" />
                Upload Files
              </button>
            </div>

            <div>
              <h3 className="text-sm font-medium">Voice</h3>
              <p className="text-sm text-gray-500">Record a voice message</p>
              <button className="btn btn-ghost w-full mt-2 border-dashed">
                <Mic size={16} className="mr-2" />
                Start Recording
              </button>
            </div>

            <div>
              <h3 className="text-sm font-medium">Camera</h3>
              <p className="text-sm text-gray-500">Take a photo or video</p>
              <button className="btn btn-ghost w-full mt-2 border-dashed">
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
