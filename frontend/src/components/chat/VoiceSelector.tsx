import React, { useState, useEffect } from 'react';
import { Volume2 } from 'lucide-react';
import ttsService from '../../services/ttsService';
import { useInitialization } from '../../contexts/InitializationContext';

interface VoiceSelectorProps {
  onVoiceChange?: (voiceId: string) => void;
  disabled?: boolean;
}

const VoiceSelector: React.FC<VoiceSelectorProps> = ({ onVoiceChange, disabled = false }) => {
  const { user } = useInitialization();
  const [selectedVoice, setSelectedVoice] = useState<string>('glados');
  const [availableVoices, setAvailableVoices] = useState<string[]>([]);
  const [loading, setLoading] = useState(false);

  // Load available voices and current settings
  useEffect(() => {
    const loadVoicesAndSettings = async () => {
      if (!user?.id) return;
      
      setLoading(true);
      try {
        // Load current TTS settings
        const settings = await ttsService.getSettings(user.id);
        const currentVoice = settings.voiceId || 'glados';
        setSelectedVoice(currentVoice);
        
        // Load available voices based on current provider
        const provider = settings.ttsProvider || 'fishspeech';
        if (provider === 'fishspeech') {
          // For Fish Speech, use hardcoded voices
          const voices = ['jazzy', 'glados', 'scarlet'];
          setAvailableVoices(voices);
        } else {
          // For ElevenLabs, use API or hardcoded list
          const voices = ['Rachel', 'Domi', 'Bella', 'Antoni'];
          setAvailableVoices(voices);
        }
      } catch (error) {
        console.error('Failed to load voices and settings:', error);
        // Fallback to hardcoded Fish Speech voices
        setAvailableVoices(['jazzy', 'glados', 'scarlet']);
      } finally {
        setLoading(false);
      }
    };

    loadVoicesAndSettings();
  }, [user?.id]);

  const handleVoiceChange = async (voiceId: string) => {
    if (!user?.id) return;
    
    setSelectedVoice(voiceId);
    
    try {
      // Update the voice setting in the database
      await ttsService.updateSettings({
        userId: user.id,
        voiceId: voiceId
      });
      
      // Notify parent component
      if (onVoiceChange) {
        onVoiceChange(voiceId);
      }
    } catch (error) {
      console.error('Failed to update voice setting:', error);
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
        value={selectedVoice}
        onChange={(e) => handleVoiceChange(e.target.value)}
        className="px-3 py-2 bg-white border border-gray-300 rounded-lg text-sm focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 min-w-[120px]"
        disabled={disabled || loading}
      >
        {loading ? (
          <option value="">Loading voices...</option>
        ) : (
          availableVoices.map(voice => (
            <option key={voice} value={voice}>
              {formatVoiceName(voice)}
            </option>
          ))
        )}
      </select>
    </div>
  );
};

export default VoiceSelector;
