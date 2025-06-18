import React, { useState, useEffect, useCallback } from 'react';
import { Volume2 } from 'lucide-react';
import ttsService from '../../services/ttsService'; // Keep for getVoices
import { useInitialization } from '../../contexts/InitializationContext';

interface VoiceSelectorProps {
  provider?: string | null;
  voiceId?: string | null;
  onSettingsChange?: (provider: string, voiceId: string) => void;
  disabled?: boolean;
}

const VoiceSelector: React.FC<VoiceSelectorProps> = ({
  provider,
  voiceId,
  onSettingsChange,
  disabled = false,
}) => {
  const { user } = useInitialization();
  // availableVoices is still internal state, fetched based on provider prop
  const [availableVoices, setAvailableVoices] = useState<string[]>([]);
  const [loadingVoices, setLoadingVoices] = useState(false);

  const safeProvider = provider || 'fishspeech'; // Default to fishspeech if prop is null/undefined
  const safeVoiceId = voiceId || 'glados';     // Default to glados if prop is null/undefined

  // Load available voices when provider changes
  const fetchVoicesForProvider = useCallback(async (currentProvider: string) => {
    if (!user?.id || !currentProvider) {
      setAvailableVoices(['glados']); // Default fallback
      return;
    }
    
    setLoadingVoices(true);
    try {
      console.log(`🔄 VoiceSelector: Fetching voices for provider: ${currentProvider}`);
      // Assuming ttsService.getVoices can take a provider
      // If not, this logic needs to adapt to how voices are fetched per provider
      let voices: string[] = [];
      if (currentProvider === 'fishspeech') {
        voices = ['jazzy', 'glados', 'scarlet']; // Hardcoded for fishspeech as before
      } else if (currentProvider === 'elevenlabs') {
        voices = ['Rachel', 'Domi', 'Bella', 'Antoni']; // Hardcoded for elevenlabs
      } else if (currentProvider === 'openai') {
        voices = ['alloy', 'echo', 'fable', 'onyx', 'nova', 'shimmer']; // Standard OpenAI voices
      } else {
        console.warn(`🔄 VoiceSelector: Unknown TTS provider "${currentProvider}", using default voices.`);
        voices = ['glados']; // Fallback
      }
      setAvailableVoices(voices);
      console.log(`🔄 VoiceSelector: Voices for ${currentProvider}:`, voices);

      // If current voiceId from props isn't in the new list, maybe select the first one?
      // Or let parent handle it via onSettingsChange if needed.
      // For now, if safeVoiceId is not in 'voices', the dropdown might show blank or the first option.
      // This can be improved if necessary by calling onSettingsChange with the first available voice.

    } catch (error) {
      console.error(`Failed to load voices for provider ${currentProvider}:`, error);
      setAvailableVoices(['glados']); // Fallback
    } finally {
      setLoadingVoices(false);
    }
  }, [user?.id]);

  useEffect(() => {
    fetchVoicesForProvider(safeProvider);
  }, [safeProvider, fetchVoicesForProvider]);


  const handleInternalVoiceChange = (newVoiceId: string) => {
    if (onSettingsChange) {
      onSettingsChange(safeProvider, newVoiceId);
    }
  };

  const formatVoiceName = (voice: string) => {
    // Capitalize first letter and handle special cases
    switch (voice.toLowerCase()) {
      case 'glados':
        return 'GLaDOS';
      case 'jazzy':
        return 'Jazzy';
      case 'scarlet':
        return 'Scarlet';
      default:
        return voice.charAt(0).toUpperCase() + voice.slice(1);
    }
  };

  return (
    <div className="flex items-center">
      <Volume2 size={16} className="text-gray-500 mr-2" />
      <label htmlFor="voice-selector" className="sr-only">Select Voice</label>
      <select
        id="voice-selector"
        value={safeVoiceId} // Controlled by prop
        onChange={(e) => handleInternalVoiceChange(e.target.value)}
        className="px-3 py-2 bg-white border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 min-w-[120px]"
        disabled={disabled || loadingVoices}
      >
        {loadingVoices ? (
          <option value="">Loading voices...</option>
        ) : (
          availableVoices.map(v => (
            <option key={v} value={v}>
              {formatVoiceName(v)}
            </option>
          ))
        )}
      </select>
    </div>
  );
};

export default VoiceSelector;
