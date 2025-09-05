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
  const [effectiveUserId, setEffectiveUserId] = useState<string | null>(null);
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
  // Persisted settings snapshot for building options and discovery
  const [enabledEngines, setEnabledEngines] = useState<Record<string, boolean>>({});
  const [engineModels, setEngineModels] = useState<Record<string, string>>({});
  const [connections, setConnections] = useState<{ OllamaApiUrl?: string; LmStudioApiUrl?: string; VllmApiUrl?: string }>({});
  const [notice, setNotice] = useState<string>('');
  const fileInputRef = useRef<HTMLInputElement|null>(null);

  /* ------------------------------ refs ----------------------------------- */
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const isFirstMessage = useRef(urlInfo.isNewConversation);
  const didLoadSettingsRef = useRef<string | null>(null);


  /* -------------------------- persist prefs ------------------------------ */
  useEffect(() => {
    localStorage.setItem(
      'showCharacterImages',
      showCharacterImages ? 'true' : 'false'
    );
  }, [showCharacterImages]);

  // Set effectiveUserId from user context with fallback
  useEffect(() => {
    const userId = user?.id || 'admin'; // Use same fallback as Settings page
    console.log('🔄 Chat: Setting effective user ID to:', userId, '(user context:', user, ')');
    setEffectiveUserId(userId);
  }, [user?.id, user]);

  /* ---------------------- SYNC FROM DASHBOARD LLM ------------------------ */
  useEffect(() => {
    const syncAllChatSettings = async () => {
      if (!effectiveUserId) {
        console.log('🔄 Chat: No effective user ID, skipping settings sync');
        return;
      }

      // Guard against duplicate runs (e.g., React StrictMode) per user
      if (didLoadSettingsRef.current === effectiveUserId) {
        console.log('🔄 Chat: Settings already loaded for user', effectiveUserId, 'skipping');
        return;
      }
      didLoadSettingsRef.current = effectiveUserId;

      console.log('🔄 Chat: === LOADING CHAT SETTINGS ===');
      console.log('🔄 Chat: Effective User ID:', effectiveUserId);
      console.log('🔄 Chat: User object:', user);

      try {
        const settings = await chatService.getChatSettings(effectiveUserId);
        console.log('🔄 Chat: Raw settings from backend:', settings);
        console.log('🔄 Chat: Settings.enabledEngines:', settings.enabledEngines);
        console.log('🔄 Chat: Settings.engineModels:', settings.engineModels);

        setChatEngineOverride(settings.llmEngine || null);
        setChatModelOverride(settings.llmModel || null);
        setTtsProvider(settings.ttsProvider || null);
        setTtsVoiceId(settings.ttsVoiceId || null);
        setEnabledEngines(settings.enabledEngines || {});
        setEngineModels(settings.engineModels || {});
        
        console.log('🔄 Chat: State updated with:', {
          engine: settings.llmEngine,
          model: settings.llmModel, 
          enabledEngines: settings.enabledEngines,
          engineModels: settings.engineModels
        });

        // Load connection settings (base URLs) for discovery without hardcoding
        try {
          const resp = await fetch(`/api/settings/connections?userId=${encodeURIComponent(effectiveUserId)}`);
          if (resp.ok) {
            const conn = await resp.json();
            setConnections({
              OllamaApiUrl: conn.OllamaApiUrl,
              LmStudioApiUrl: conn.LmStudioApiUrl,
              VllmApiUrl: conn.VllmApiUrl,
            });
          }
        } catch {}

        console.log('🔄 Chat: Chat settings synced → LLM:', settings.llmEngine, settings.llmModel, 'TTS:', settings.ttsProvider, settings.ttsVoiceId);
      } catch (err) {
        console.error('Chat settings sync failed:', err);
        // Fallback or default settings if preferred
        setChatEngineOverride(null); // Default LLM
        setChatModelOverride(null);
        setTtsProvider('fishspeech'); // Default TTS
        setTtsVoiceId('glados');
        setEnabledEngines({ ollama: true, lmstudio: true });
        setEngineModels({});
      }
    };

    syncAllChatSettings();
  }, [effectiveUserId]); // Only run once when user resolves

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
          `/api/character/user/${effectiveUserId}`
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
              `/api/settings/DefaultCharacterId?userId=${effectiveUserId}`
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
    effectiveUserId,
    navigate,
    urlInfo.characterName,
    urlInfo.conversationId,
    sessionCharacter
  ]);

  /* ---------------------------- Build LLM options from saved settings --------------------------- */
  useEffect(() => {
    const llms: LlmOption[] = [];
    const seen = new Set<string>();
    const pushOpt = (engine: string, model: string, label?: string) => {
      if (!engine || !model) return;
      const key = `${engine}:${model}`;
      if (seen.has(key)) return;
      const displayLabel = label || `${engine.toUpperCase()}: ${model}`;
      llms.push({ value: key, label: displayLabel, engine, model });
      seen.add(key);
    };
    
    // Only include engines that are both enabled AND have a configured model
    for (const eng of ['ollama', 'lmstudio', 'openai', 'claude', 'vllm']) {
      if (!enabledEngines[eng]) continue; // Skip disabled engines
      const m = engineModels[eng];
      if (m && m.trim()) { // Only include if model is actually configured
        pushOpt(eng, m);
      }
    }
    
    setAvailableLlms(llms);
    
    // Auto-select first available if no current selection or current selection is invalid
    if (llms.length > 0) {
      const currentKey = chatEngineOverride && chatModelOverride ? `${chatEngineOverride}:${chatModelOverride}` : null;
      const isCurrentValid = currentKey && llms.some(l => l.value === currentKey);
      
      if (!isCurrentValid) {
        // Auto-select the first available option
        const firstOption = llms[0];
        setChatEngineOverride(firstOption.engine);
        setChatModelOverride(firstOption.model);
        console.log('🔄 Chat: Auto-selected first available LLM:', firstOption.engine, firstOption.model);
      }
    } else {
      // No valid options available, clear selection
      setChatEngineOverride(null);
      setChatModelOverride(null);
    }
    
    // Debug log to track what's available
    console.log('🔄 Chat: Building dropdown options. Enabled engines:', enabledEngines);
    console.log('🔄 Chat: Engine models:', engineModels);
    console.log('🔄 Chat: Available LLM options:', llms.map(l => `${l.value} (${l.label})`));
  }, [enabledEngines, engineModels, chatEngineOverride, chatModelOverride]);

  /* ---------------------------- Live discovery to fill missing models --------------------------- */
  useEffect(() => {
    if (!effectiveUserId) return;
    // If we only have 'default', try to discover per-engine
    const needDiscovery = Object.keys(enabledEngines).some(k => enabledEngines[k]) &&
      Object.keys(engineModels).filter(k => enabledEngines[k] && !!engineModels[k]).length === 0;
    if (!needDiscovery) return;

    let cancelled = false;
    (async () => {
      const nextModels: Record<string, string> = { ...engineModels };
      const trySet = (engine: string, model?: string) => {
        if (!model) return false;
        if (!nextModels[engine]) { nextModels[engine] = model; return true; }
        return false;
      };
      try {
        // LM Studio: prefer loaded model endpoint
        if (enabledEngines['lmstudio']) {
          try {
            const qs: string[] = [`userId=${encodeURIComponent(effectiveUserId)}`];
            if (connections.LmStudioApiUrl) qs.push(`baseUrl=${encodeURIComponent(connections.LmStudioApiUrl)}`);
            const res = await fetch(`/api/llm/lmstudio/model?${qs.join('&')}`);
            if (res.ok) {
              const { model } = await res.json();
              trySet('lmstudio', model);
            }
          } catch {}
        }
        // Ollama / LM Studio / vLLM: use unified models endpoint
        for (const eng of ['ollama', 'lmstudio', 'vllm']) {
          if (!enabledEngines[eng]) continue;
          if (nextModels[eng]) continue; // already set
          try {
            const qs: string[] = [`engine=${eng}`, `userId=${encodeURIComponent(effectiveUserId)}`];
            const base = eng === 'ollama' ? connections.OllamaApiUrl : eng === 'lmstudio' ? connections.LmStudioApiUrl : connections.VllmApiUrl;
            if (base) qs.push(`baseUrl=${encodeURIComponent(base)}`);
            const r = await fetch(`/api/llm/models?${qs.join('&')}`);
            if (r.ok) {
              const payload = await r.json();
              const models: string[] = Array.isArray(payload) ? payload : (payload?.models || []);
              if (models.length) trySet(eng, models[0]);
            }
          } catch {}
        }
      } finally {
        if (!cancelled) {
          // Persist back so future loads have the models
          setEngineModels(nextModels);
          try {
            if (effectiveUserId) {
              const payload: any = {
                enabledEngines,
                engineModels: nextModels,
              };
              if (chatEngineOverride) payload.llmEngine = chatEngineOverride;
              if (chatModelOverride) payload.llmModel = chatModelOverride;
              await chatService.updateChatSettings(effectiveUserId, payload);
            }
          } catch {}
        }
      }
    })();
    return () => { cancelled = true; };
  }, [effectiveUserId, enabledEngines, engineModels, chatEngineOverride, chatModelOverride]);


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
      if (!effectiveUserId) return;

      try {
        const recent = await conversationService.getRecentConversation(effectiveUserId);

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
  }, [effectiveUserId]);

  /* ---------------------------------------------------------------------- */
  /*  Character select handler                                               */
  /* ---------------------------------------------------------------------- */

  const handleCharacterSelect = useCallback(
    async (character: Character | null) => {
      console.log('🔄 Chat: Character selected:', character);
      setSelectedCharacter(character);

      if (character) {
        localStorage.setItem('selectedCharacterId', character.id);
        if (effectiveUserId) {
          fetch('/api/settings/DefaultCharacterId', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: effectiveUserId, value: character.id })
          }).catch(() => {});
        }
      } else {
        localStorage.removeItem('selectedCharacterId');
        if (effectiveUserId) {
          fetch('/api/settings/DefaultCharacterId', {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ userId: effectiveUserId, value: '' })
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
    [effectiveUserId, currentConversation.id, navigate]
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
        const uid = effectiveUserId || 'demo-user-id';
        const newConvo = await conversationService.createConversation(title, uid);

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

        if (selectedCharacter?.systemPrompt && effectiveUserId) {
          await conversationService.setCharacterContext(
            conversationId,
            effectiveUserId,
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

      if (effectiveUserId) {
        await conversationService.appendMessage(
          conversationId,
          effectiveUserId,
          'user',
          textToSend
        );
      }

      /* ---------------- send to model ---------------- */
      const aiResponse = await chatService.sendMessage(
        conversationId,
        textToSend,
        effectiveUserId,
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

      if (effectiveUserId) {
        await conversationService.appendMessage(
          conversationId,
          effectiveUserId,
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
            userId={effectiveUserId || ''}
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
                  if (!effectiveUserId) return;
                  console.log('🔄 Chat: VoiceSelector changed TTS to Provider:', newProvider, 'VoiceID:', newVoiceId);
                  setTtsProvider(newProvider);
                  setTtsVoiceId(newVoiceId);
                  try {
                    await chatService.updateChatSettings(effectiveUserId, {
                      llmEngine: chatEngineOverride || undefined,
                      llmModel: chatModelOverride || undefined,
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
                  chatEngineOverride && chatModelOverride
                    ? `${chatEngineOverride}:${chatModelOverride}`
                    : ''
                }
                onChange={async (e) => {
                  const sel = availableLlms.find(
                    (l) => l.value === e.target.value
                  );
                  if (!sel || !effectiveUserId) {
                    console.warn('🔄 Chat: Selected LLM option not found or user ID missing:', e.target.value, effectiveUserId);
                    return;
                  }

                  console.log('🔄 Chat: User changed LLM to:', sel);
                  const newEngine = sel.engine;
                  const newModel = sel.model;

                  setChatEngineOverride(newEngine);
                  setChatModelOverride(newModel);

                  try {
                    console.log('🔄 Chat: Updating all chat settings. New LLM:', newEngine, newModel, 'Current TTS:', ttsProvider, ttsVoiceId);
                    const updatedEnabled = { ...enabledEngines };
                    if (newEngine) updatedEnabled[newEngine] = true;
                    const updatedModels = { ...engineModels };
                    if (newEngine && newModel) updatedModels[newEngine] = newModel;
                    setEnabledEngines(updatedEnabled);
                    setEngineModels(updatedModels);
                    if (effectiveUserId) {
                      await chatService.updateChatSettings(effectiveUserId, {
                        llmEngine: newEngine,
                        llmModel: newModel,
                        ttsProvider: ttsProvider || 'fishspeech',
                        ttsVoiceId: ttsVoiceId || 'glados',
                        enabledEngines: updatedEnabled,
                        engineModels: updatedModels,
                      });
                    }

                    // Notify dashboard about LLM part of the change
                    window.dispatchEvent(
                      new CustomEvent('llmSettingsChanged', {
                        detail: { engine: newEngine, model: newModel }
                      })
                    );
                    console.log('🔄 Chat: Chat settings (including LLM) updated and Dashboard notified.');
                    setNotice('LLM selection saved');
                    setTimeout(() => setNotice(''), 2000);
                  } catch (err) {
                    console.error('Failed to update chat settings:', err);
                  }
                }}
                className="px-3 py-2 border rounded-lg text-sm disabled:opacity-50"
                disabled={isLoading || availableLlms.length === 0}
              >
                {availableLlms.length === 0 ? (
                  <option value="">No activated models - Configure in Settings</option>
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
              <button className="btn btn-ghost w-full mt-2 border-dashed" onClick={() => fileInputRef.current?.click()}>
                <Paperclip size={16} className="mr-2" />
                Upload Files
              </button>
              <button
                type="button"
                className="btn btn-ghost w-full mt-2"
                onClick={() => fileInputRef.current?.click()}
              >
                <Paperclip size={16} className="mr-2" /> Choose Files…
              </button>
              <input
                ref={fileInputRef}
                type="file"
                multiple
                className="hidden"
                onChange={async (e) => {
                  if (!e.target.files || !currentConversation.id) return;
                  const form = new FormData();
                  Array.from(e.target.files).forEach((f) => form.append('files', f));
                  form.append('conversationId', currentConversation.id);
                  try {
                    const resp = await fetch('/api/upload/files', { method: 'POST', body: form });
                    if (!resp.ok) throw new Error(`Upload failed: ${resp.status}`);
                    setNotice('Files uploaded and processing started');
                    setTimeout(() => setNotice(''), 2500);
                  } catch (upErr) {
                    console.error('Upload error:', upErr);
                    setNotice('Upload failed');
                    setTimeout(() => setNotice(''), 2500);
                  } finally {
                    e.target.value = '';
                  }
                }}
              />
              <script/>
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
      {/* toast */}
      {notice && (
        <div className="fixed top-4 right-4 bg-green-600 text-white px-4 py-2 rounded shadow-lg z-50">
          {notice}
        </div>
      )}
    </motion.div>
  );
};

export default ChatPage;
