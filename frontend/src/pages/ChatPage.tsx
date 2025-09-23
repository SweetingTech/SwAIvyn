import React, {
  useState,
  useEffect,
  useRef,
  useCallback,
  FormEvent
} from 'react';
import { motion } from 'framer-motion';
import { Send, Paperclip, Camera, Mic, Plus, Volume2, VolumeX, MicOff, X, MessageSquare, Settings, PlayCircle, StopCircle } from 'lucide-react';
import { useParams, useNavigate } from 'react-router-dom';

import ChatMessage from '../components/chat/ChatMessage';
import ChatSidebar from '../components/chat/ChatSidebar';
import CharacterSelector from '../components/chat/CharacterSelector';
import VoiceSelector from '../components/chat/VoiceSelector';
import BrainExplorer from '../components/BrainExplorer';
import FileManager from '../components/chat/FileManager';

import chatService from '../services/chatService';
import conversationService from '../services/conversationService';
import apiService from '../services/apiService';
import ttsService from '../services/ttsService';
import { Message } from '../types/chat';
import {
  parseChatUrl,
  generateChatUrl,
  createDefaultChatUrl
} from '../utils/chatUrls';
import { useInitialization } from '../contexts/InitializationContext';
import useEffectiveUser from '../hooks/useEffectiveUser';

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
  const eff = useEffectiveUser();
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
  const [isUniversalTtsPlaying, setIsUniversalTtsPlaying] = useState(false);
  const universalAudioRef = useRef<HTMLAudioElement | null>(null);
  const fileInputRef = useRef<HTMLInputElement|null>(null);

  // Voice interaction toggles
  const [ttsEnabled, setTtsEnabled] = useState<boolean>(() => {
    const stored = localStorage.getItem('auto_tts');
    return stored === 'true';
  });
  const [sttEnabled, setSttEnabled] = useState<boolean>(() => {
    const stored = localStorage.getItem('stt_enabled');
    return stored === 'true';
  });

  // Mobile responsive state
  const [isLeftSidebarOpen, setIsLeftSidebarOpen] = useState(false);
  const [isRightSidebarOpen, setIsRightSidebarOpen] = useState(false);

  /* ------------------------------ refs ----------------------------------- */
  const bottomRef = useRef<HTMLDivElement | null>(null);
  const isFirstMessage = useRef(urlInfo.isNewConversation);
  const didLoadSettingsRef = useRef<string | null>(null);

  /* -------------------------- Mobile handlers ---------------------------- */
  const closeMobileSidebars = useCallback(() => {
    setIsLeftSidebarOpen(false);
    setIsRightSidebarOpen(false);
  }, []);

  const toggleLeftSidebar = useCallback(() => {
    setIsLeftSidebarOpen(prev => !prev);
    setIsRightSidebarOpen(false); // Close right if opening left
  }, []);

  const toggleRightSidebar = useCallback(() => {
    setIsRightSidebarOpen(prev => !prev);
    setIsLeftSidebarOpen(false); // Close left if opening right
  }, []);

  // Close mobile sidebars on escape key
  useEffect(() => {
    const handleEscape = (e: KeyboardEvent) => {
      if (e.key === 'Escape') {
        closeMobileSidebars();
      }
    };
    document.addEventListener('keydown', handleEscape);
    return () => document.removeEventListener('keydown', handleEscape);
  }, [closeMobileSidebars]);


  /* -------------------------- persist prefs ------------------------------ */
  useEffect(() => {
    localStorage.setItem(
      'showCharacterImages',
      showCharacterImages ? 'true' : 'false'
    );
  }, [showCharacterImages]);

  // Persist TTS toggle state
  useEffect(() => {
    localStorage.setItem('auto_tts', ttsEnabled ? 'true' : 'false');
  }, [ttsEnabled]);

  // Persist STT toggle state
  useEffect(() => {
    localStorage.setItem('stt_enabled', sttEnabled ? 'true' : 'false');
  }, [sttEnabled]);

  // Set effectiveUserId from user context with fallback
  useEffect(() => {
    const userId = eff.userId || 'admin';
    console.log('🔄 Chat: Setting effective user ID to:', userId, '(eff:', eff, ')');
    setEffectiveUserId(userId);
  }, [eff.userId]);

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
          const resp = await fetch(`/api/settings/connections?userId=${encodeURIComponent(effectiveUserId)}`, { headers: eff.headers });
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

  // Close mobile sidebars when conversation changes (mobile UX)
  useEffect(() => {
    closeMobileSidebars();
  }, [currentConversation.id, closeMobileSidebars]);

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
              `/api/settings/DefaultCharacterId?userId=${effectiveUserId}`,
              { headers: eff.headers }
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
            const res = await fetch(`/api/llm/lmstudio/model?${qs.join('&')}`, { headers: eff.headers });
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
            const r = await fetch(`/api/llm/models?${qs.join('&')}`, { headers: eff.headers });
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
              text: `Hello ${user?.username || 'there'}! How can I help you today?`,
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

  /* -------------------- Update welcome message with username ----------- */
  useEffect(() => {
    if (!user?.username || messages.length === 0) return;
    
    // Find and update welcome message if it exists and still has fallback text
    const welcomeMsg = messages.find(msg => msg.id === 'welcome' && msg.sender === 'ai');
    if (welcomeMsg && (welcomeMsg.text.includes('there!') || welcomeMsg.text.includes('Default User'))) {
      setMessages(prevMessages => 
        prevMessages.map(msg => 
          msg.id === 'welcome' && msg.sender === 'ai'
            ? { ...msg, text: `Hello ${user.username}! How can I help you today?` }
            : msg
        )
      );
    }
  }, [user?.username, messages]);

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
    if (!effectiveUserId) {
      console.warn('🔄 Chat: No effective user ID, cannot create conversation');
      return;
    }

    try {
      console.log('🔄 Chat: Starting new conversation');
      
      // Create the conversation in the backend first
      const newConversation = await conversationService.createConversation(
        'New Chat',
        effectiveUserId
      );
      
      console.log('🔄 Chat: Created conversation:', newConversation);

      // Set initial welcome message
      setMessages([
        {
          id: 'welcome',
          sender: 'ai',
          text: `Hello ${user?.username || 'there'}! How can I help you today?`,
          timestamp: new Date().toISOString()
        }
      ]);

      // Update current conversation with the real conversation ID
      setCurrentConversation({ 
        id: newConversation.id, 
        title: newConversation.title 
      });

      // Navigate to the new conversation
      navigate(
        generateChatUrl({
          conversationId: newConversation.id,
          characterName: selectedCharacter?.name
        }),
        { replace: true }
      );

      isFirstMessage.current = true;
      setInputText('');
      
      console.log('🔄 Chat: New conversation created and navigated');
    } catch (error) {
      console.error('🔄 Chat: Failed to create new conversation:', error);
      // Fallback to the old behavior
      setMessages([
        {
          id: 'welcome',
          sender: 'ai',
          text: `Hello ${user?.username || 'there'}! How can I help you today?`,
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
    }
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
  /*  Universal TTS functionality                                           */
  /* ---------------------------------------------------------------------- */

  const handleUniversalTts = async () => {
    if (!effectiveUserId) {
      setNotice('Please log in to use TTS');
      setTimeout(() => setNotice(''), 2500);
      return;
    }

    // Stop current playback if playing
    if (isUniversalTtsPlaying && universalAudioRef.current) {
      universalAudioRef.current.pause();
      URL.revokeObjectURL(universalAudioRef.current.src);
      universalAudioRef.current = null;
      setIsUniversalTtsPlaying(false);
      console.log('🔊 Universal TTS: Stopped by user');
      return;
    }

    // Check if TTS is enabled
    if (!ttsEnabled) {
      setNotice('Enable TTS to use this feature');
      setTimeout(() => setNotice(''), 2500);
      return;
    }

    setIsUniversalTtsPlaying(true);
    
    try {
      // Collect all AI messages text with length limit
      const aiMessages = messages.filter(m => m.sender === 'ai');
      
      if (aiMessages.length === 0) {
        setNotice('No AI messages to read');
        setTimeout(() => setNotice(''), 2500);
        setIsUniversalTtsPlaying(false);
        return;
      }

      // Limit total text length to prevent TTS provider limits (e.g., 4000 chars)
      const MAX_TTS_LENGTH = 3000;
      let allText = '';
      
      for (const msg of aiMessages) {
        const nextText = allText + (allText ? ' ... ' : '') + msg.text;
        if (nextText.length > MAX_TTS_LENGTH) break;
        allText = nextText;
      }
      
      if (!allText.trim()) {
        setNotice('No text to synthesize');
        setTimeout(() => setNotice(''), 2500);
        setIsUniversalTtsPlaying(false);
        return;
      }

      console.log('🔊 Universal TTS: Synthesizing text (length:', allText.length, ')');
      
      // Use TTS service to synthesize text
      const audioBlob = await ttsService.synthesize(allText, effectiveUserId, ttsVoiceId || undefined);
      
      if (audioBlob && audioBlob.size > 0) {
        const audioUrl = URL.createObjectURL(audioBlob);
        const audio = new Audio(audioUrl);
        universalAudioRef.current = audio;
        
        const cleanup = () => {
          setIsUniversalTtsPlaying(false);
          URL.revokeObjectURL(audioUrl);
          universalAudioRef.current = null;
        };
        
        audio.onended = () => {
          cleanup();
          console.log('🔊 Universal TTS: Playback ended');
        };
        
        audio.onerror = (e) => {
          console.error('🔊 Universal TTS: Playback error:', e);
          cleanup();
          setNotice('Audio playback failed');
          setTimeout(() => setNotice(''), 2500);
        };
        
        await audio.play();
        console.log('🔊 Universal TTS: Started playing');
      } else {
        console.error('🔊 Universal TTS: Empty audio blob');
        setIsUniversalTtsPlaying(false);
        setNotice('TTS synthesis failed');
        setTimeout(() => setNotice(''), 2500);
      }
    } catch (error) {
      console.error('🔊 Universal TTS: Error:', error);
      setIsUniversalTtsPlaying(false);
      setNotice('TTS failed');
      setTimeout(() => setNotice(''), 2500);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  Handle "remember this" command                                        */
  /* ---------------------------------------------------------------------- */

  const handleRememberCommand = async (memoryContent: string) => {
    if (!effectiveUserId) {
      console.warn('🧠 Memory: No effective user ID, cannot save memory');
      // Show user message that memory couldn't be saved
      const errorMessage: Message = {
        id: Date.now().toString(),
        sender: 'ai',
        text: 'I cannot save memories right now because no user is logged in.',
        timestamp: new Date().toISOString()
      };
      setMessages((prev) => [...prev, errorMessage]);
      setInputText('');
      return;
    }

    try {
      console.log('🧠 Memory: Saving memory:', memoryContent);
      
      // Show user message first
      const userMessage: Message = {
        id: Date.now().toString(),
        sender: 'user',
        text: `Remember this: ${memoryContent}`,
        timestamp: new Date().toISOString()
      };
      setMessages((prev) => [...prev, userMessage]);
      setInputText('');
      setIsLoading(true);

      // Save to memory API
      const response = await fetch('/api/memory', {
        method: 'POST',
        headers: { 
          'Content-Type': 'application/json',
          ...(effectiveUserId && { 'Authorization': `Bearer ${effectiveUserId}` })
        },
        body: JSON.stringify({
          userId: effectiveUserId,
          content: memoryContent,
          category: 'Personal',
          isShared: false
        }),
      });

      if (response.ok) {
        const savedMemory = await response.json();
        console.log('🧠 Memory: Saved successfully:', savedMemory);
        
        // Show success message
        const successMessage: Message = {
          id: Date.now().toString(),
          sender: 'ai',
          text: `✅ I've remembered: "${memoryContent}". I'll keep this in mind for our future conversations.`,
          timestamp: new Date().toISOString()
        };
        setMessages((prev) => [...prev, successMessage]);
      } else {
        console.error('🧠 Memory: Failed to save memory:', response.status);
        
        // Show error message
        const errorMessage: Message = {
          id: Date.now().toString(),
          sender: 'ai',
          text: `❌ Sorry, I couldn't save that memory. Please try again later.`,
          timestamp: new Date().toISOString()
        };
        setMessages((prev) => [...prev, errorMessage]);
      }
    } catch (error) {
      console.error('🧠 Memory: Error saving memory:', error);
      
      // Show error message
      const errorMessage: Message = {
        id: Date.now().toString(),
        sender: 'ai',
        text: `❌ Sorry, I encountered an error while trying to save that memory.`,
        timestamp: new Date().toISOString()
      };
      setMessages((prev) => [...prev, errorMessage]);
    } finally {
      setIsLoading(false);
    }
  };

  /* ---------------------------------------------------------------------- */
  /*  Submit message                                                        */
  /* ---------------------------------------------------------------------- */

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!inputText.trim() || isLoading) return;

    const textToSend = inputText.trim();

    // Check for "remember this" command
    const rememberRegex = /^(?:remember this:?\s*|remember:?\s*|\/remember\s*)(.*)/i;
    const rememberMatch = textToSend.match(rememberRegex);
    
    if (rememberMatch) {
      const memoryContent = rememberMatch[1].trim();
      if (memoryContent) {
        await handleRememberCommand(memoryContent);
      } else {
        // Show error for empty remember command
        const errorMessage: Message = {
          id: Date.now().toString(),
          sender: 'ai',
          text: 'Please provide content to remember. Example: "remember this: my dog\'s name is Fede"',
          timestamp: new Date().toISOString()
        };
        setMessages((prev) => [...prev, errorMessage]);
        setInputText('');
      }
      return;
    }

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
      <div className="flex flex-grow overflow-hidden relative">
        {/* --------------- LEFT SIDEBAR (sessions) - RESPONSIVE ----------------------- */}
        {/* Desktop: Fixed sidebar */}
        <div className="hidden lg:flex w-64 border-r bg-white">
          <ChatSidebar
            userId={effectiveUserId || ''}
            currentSessionId={currentConversation.id || null}
            onSelectSession={handleSelectConversation}
            onNewSession={handleNewConversation}
          />
        </div>

        {/* Mobile/Tablet: Drawer overlay */}
        {isLeftSidebarOpen && (
          <div className="lg:hidden fixed inset-0 z-50 flex">
            {/* Backdrop */}
            <div 
              className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
              onClick={closeMobileSidebars}
            />
            {/* Drawer */}
            <div className="relative flex flex-col w-80 max-w-[80vw] bg-white shadow-xl">
              <div className="flex items-center justify-between p-4 border-b">
                <h2 className="text-lg font-medium">Conversations</h2>
                <button 
                  onClick={closeMobileSidebars}
                  className="p-2 text-gray-400 hover:text-gray-600 rounded-lg"
                >
                  <X size={20} />
                </button>
              </div>
              <div className="flex-1 overflow-hidden">
                <ChatSidebar
                  userId={effectiveUserId || ''}
                  currentSessionId={currentConversation.id || null}
                  onSelectSession={handleSelectConversation}
                  onNewSession={handleNewConversation}
                />
              </div>
            </div>
          </div>
        )}

        {/* --------------- MAIN CHAT AREA - RESPONSIVE -------------------------------- */}
        <div className="flex-grow flex flex-col overflow-hidden min-w-0">
          {/* header - Mobile-Responsive */}
          <div className="px-4 py-2 bg-white border-b">
            <div className="flex justify-between items-center mb-2">
              <div className="flex items-center gap-3">
                {/* Mobile: Hamburger menu for conversations */}
                <button
                  onClick={toggleLeftSidebar}
                  className="lg:hidden p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
                  title="Open conversations"
                >
                  <MessageSquare size={20} />
                </button>
                
                <h1 className="text-lg sm:text-xl font-semibold text-gray-800 truncate">
                  {currentConversation.title}
                </h1>
              </div>
              
              <div className="flex items-center gap-2">
                {/* Mobile: Tools menu */}
                <button
                  onClick={toggleRightSidebar}
                  className="lg:hidden p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-lg transition-colors"
                  title="Open tools"
                >
                  <Settings size={20} />
                </button>
                
                <button
                  onClick={handleNewConversation}
                  className="p-2 text-gray-500 hover:text-primary-500 hover:bg-gray-100 rounded-lg transition-colors"
                  title="New Chat"
                >
                  <Plus size={20} />
                </button>
                
                {/* Universal TTS Button */}
                <button
                  onClick={handleUniversalTts}
                  disabled={isLoading || messages.filter(m => m.sender === 'ai').length === 0}
                  className={`p-2 rounded-lg transition-colors ${
                    isUniversalTtsPlaying
                      ? 'text-red-600 hover:text-red-700 bg-red-50'
                      : 'text-purple-600 hover:text-purple-700 hover:bg-purple-50'
                  }`}
                  title={isUniversalTtsPlaying ? 'Stop reading all messages' : 'Read all AI messages aloud'}
                >
                  {isUniversalTtsPlaying ? (
                    <StopCircle size={20} />
                  ) : (
                    <PlayCircle size={20} />
                  )}
                </button>
              </div>
            </div>

            {/* Mobile-Responsive Chat Controls */}
            <div className="flex flex-col sm:flex-row flex-wrap items-start sm:items-center gap-3 sm:gap-4">
              {/* Top Row: Character & Status - Mobile Priority */}
              <div className="flex flex-wrap items-center gap-2 sm:gap-4">
                <CharacterSelector
                  selectedCharacterId={selectedCharacter?.id || null}
                  onCharacterSelect={handleCharacterSelect}
                />
                {selectedCharacter && (
                  <span className="flex items-center text-sm text-blue-600">
                    <span className="w-2 h-2 bg-blue-500 rounded-full mr-2" />
                    <span className="hidden sm:inline">Active: </span>{selectedCharacter.name}
                  </span>
                )}
              </div>

              {/* Middle Row: Voice Controls & Options */}
              <div className="flex flex-wrap items-center gap-2 sm:gap-4 w-full sm:w-auto">
                {/* Voice interaction toggles - Touch-friendly */}
                <div className="flex items-center gap-2">
                  {/* TTS Toggle */}
                  <button
                    onClick={() => setTtsEnabled(!ttsEnabled)}
                    className={`p-3 sm:p-2 rounded-lg transition-colors touch-manipulation ${
                      ttsEnabled 
                        ? 'bg-blue-100 text-blue-600 border border-blue-300' 
                        : 'bg-gray-100 text-gray-500 border border-gray-300 hover:bg-gray-200'
                    }`}
                    title={`Text-to-Speech: ${ttsEnabled ? 'ON' : 'OFF'}`}
                    disabled={isLoading}
                  >
                    {ttsEnabled ? <Volume2 size={16} /> : <VolumeX size={16} />}
                  </button>

                  {/* STT Toggle */}
                  <button
                    onClick={() => setSttEnabled(!sttEnabled)}
                    className={`p-3 sm:p-2 rounded-lg transition-colors touch-manipulation ${
                      sttEnabled 
                        ? 'bg-green-100 text-green-600 border border-green-300' 
                        : 'bg-gray-100 text-gray-500 border border-gray-300 hover:bg-gray-200'
                    }`}
                    title={`Speech-to-Text: ${sttEnabled ? 'ON' : 'OFF'}`}
                    disabled={isLoading}
                  >
                    {sttEnabled ? <Mic size={16} /> : <MicOff size={16} />}
                  </button>

                  {/* Voice Mode Indicator */}
                  {ttsEnabled && sttEnabled && (
                    <div className="flex items-center text-xs text-green-600 font-medium">
                      <div className="w-2 h-2 bg-green-500 rounded-full mr-1 animate-pulse" />
                      <span className="hidden sm:inline">Voice Mode</span>
                    </div>
                  )}
                </div>

                {/* Image toggle - Compact on mobile */}
                <label className="text-sm flex items-center gap-1 touch-manipulation">
                  <input
                    type="checkbox"
                    checked={showCharacterImages}
                    onChange={(e) => setShowCharacterImages(e.target.checked)}
                    className="w-4 h-4"
                  />
                  <span className="hidden sm:inline">Use Image</span>
                  <span className="sm:hidden">Img</span>
                </label>
              </div>

              {/* Bottom Row: Voice & Model Selection */}
              <div className="flex flex-wrap items-center gap-2 sm:gap-4 w-full sm:w-auto">
                {/* Voice selector - More compact on mobile */}
                <div className="flex-shrink-0">
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
                </div>

                {/* LLM dropdown - Full width on mobile */}
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
                          llmEngine: newEngine || undefined,
                          llmModel: newModel || undefined,
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
                  className="px-3 py-2 border rounded-lg text-sm disabled:opacity-50 w-full sm:w-auto min-w-[200px] touch-manipulation"
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

          {/* composer - Mobile-Responsive */}
          <form onSubmit={handleSubmit} className="p-3 sm:p-4 border-t bg-white">
            <div className="flex items-end gap-2 sm:gap-3">
              <div className="flex-grow">
                <input
                  type="text"
                  placeholder="Type your message…"
                  value={inputText}
                  onChange={(e) => setInputText(e.target.value)}
                  className="w-full px-4 py-3 sm:py-2 border rounded-full focus:ring-2 focus:ring-primary-500 focus:border-primary-500 text-base sm:text-sm touch-manipulation"
                  disabled={isLoading}
                />
              </div>
              
              {/* Action buttons - Touch-friendly */}
              <div className="flex items-center gap-1 sm:gap-2">
                {/* File upload - Main upload button */}
                <button 
                  type="button" 
                  className="p-2 sm:p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-full transition-colors touch-manipulation"
                  onClick={() => fileInputRef.current?.click()}
                  title="Upload file"
                >
                  <Paperclip size={18} />
                </button>
                
                {/* Camera - Single camera button (not implemented) */}
                <button 
                  type="button" 
                  className="p-2 sm:p-2 text-gray-500 hover:text-gray-700 hover:bg-gray-100 rounded-full transition-colors touch-manipulation"
                  title="Camera (coming soon)"
                  disabled={true}
                >
                  <Camera size={18} />
                </button>
                
                {/* Send button - Prominently displayed */}
                <button
                  type="submit"
                  className="p-3 sm:p-2 bg-primary-500 text-white rounded-full disabled:opacity-50 hover:bg-primary-600 transition-colors touch-manipulation min-w-[44px] min-h-[44px] sm:min-w-[36px] sm:min-h-[36px] flex items-center justify-center"
                  disabled={!inputText.trim() || isLoading}
                  title="Send message"
                >
                  <Send size={18} />
                </button>
              </div>
            </div>
            
          </form>
        </div>

        {/* --------------- RIGHT SIDEBAR (tools) - RESPONSIVE ------------------------- */}
        {/* Desktop: Fixed sidebar */}
        <div className="hidden lg:flex w-80 border-l bg-white flex-col">
          <div className="p-4 border-b">
            <h2 className="text-lg font-medium">Tools</h2>
          </div>

          <div className="flex-1 overflow-y-auto">
            <BrainExplorer />

            <div className="p-4 space-y-4">
              <FileManager 
                conversationId={currentConversation.id || ''}
                onFileDeleted={() => {
                  // Refresh file list or handle file deletion
                  setNotice('File deleted');
                  setTimeout(() => setNotice(''), 2500);
                }}
              />
              
              {/* Hidden file input for upload */}
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
                    const resp = await fetch('/api/upload/files', { method: 'POST', body: form, headers: eff.headers });
                    if (!resp.ok) throw new Error(`Upload failed: ${resp.status}`);
                    setNotice('Files uploaded and processing started');
                    setTimeout(() => setNotice(''), 2500);
                    // Trigger FileManager refresh by updating a state or calling a refresh method
                  } catch (upErr) {
                    console.error('Upload error:', upErr);
                    setNotice('Upload failed');
                    setTimeout(() => setNotice(''), 2500);
                  } finally {
                    e.target.value = '';
                  }
                }}
              />
            </div>
          </div>
        </div>

        {/* Mobile/Tablet: Tools drawer overlay */}
        {isRightSidebarOpen && (
          <div className="lg:hidden fixed inset-0 z-50 flex justify-end">
            {/* Backdrop */}
            <div 
              className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
              onClick={closeMobileSidebars}
            />
            {/* Drawer */}
            <div className="relative flex flex-col w-80 max-w-[80vw] bg-white shadow-xl">
              <div className="flex items-center justify-between p-4 border-b">
                <h2 className="text-lg font-medium">Tools</h2>
                <button 
                  onClick={closeMobileSidebars}
                  className="p-2 text-gray-400 hover:text-gray-600 rounded-lg"
                >
                  <X size={20} />
                </button>
              </div>
              
              <div className="flex-1 overflow-y-auto">
                <BrainExplorer />

                <div className="p-4 space-y-4">
                  <FileManager 
                    conversationId={currentConversation.id || ''}
                    onFileDeleted={() => {
                      // Refresh file list or handle file deletion
                      setNotice('File deleted');
                      setTimeout(() => setNotice(''), 2500);
                    }}
                  />
                </div>
              </div>
            </div>
          </div>
        )}
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
