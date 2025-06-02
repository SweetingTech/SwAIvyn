import React, { useState, useEffect } from 'react';
import { ChevronDown, User, Bot } from 'lucide-react';
import apiService from '../../services/apiService';

interface Character {
  id: string;
  name: string;
  description: string;
  personality: string;
  systemPrompt: string;
  yamlProfile: string;
  userId: string;
}

interface CharacterSelectorProps {
  selectedCharacterId: string | null;
  onCharacterSelect: (character: Character | null) => void;
  disabled?: boolean;
}

/**
 * CharacterSelector component allows users to choose which AI character
 * to chat with from their available characters.
 */
const CharacterSelector: React.FC<CharacterSelectorProps> = ({
  selectedCharacterId,
  onCharacterSelect,
  disabled = false
}) => {
  const [characters, setCharacters] = useState<Character[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load characters on mount and clean up old localStorage data
  useEffect(() => {
    // Clean up old localStorage data (migration to sessionStorage)
    if (localStorage.getItem('selectedCharacterId')) {
      console.log('Migrating character selection from localStorage to sessionStorage');
      const oldCharacterId = localStorage.getItem('selectedCharacterId');
      if (oldCharacterId && oldCharacterId !== 'null') {
        sessionStorage.setItem('selectedCharacterId', oldCharacterId);
      }
      localStorage.removeItem('selectedCharacterId');
    }

    loadCharacters();
  }, []);

  // Validate current selection when characters are loaded
  useEffect(() => {
    if (characters.length === 0) {
      return;
    }

    // Check if the current selectedCharacterId is valid
    if (selectedCharacterId) {
      const currentCharacter = characters.find(c => c.id === selectedCharacterId);
      if (!currentCharacter) {
        // Current selected character ID is invalid, clear it
        console.warn('Current selected character ID not found in database, clearing:', selectedCharacterId);
        onCharacterSelect(null);
        return;
      }
      // Current selection is valid, make sure we have full character data
      if (currentCharacter) {
        onCharacterSelect(currentCharacter);
      }
    }
  }, [characters, selectedCharacterId, onCharacterSelect]);

  // New aggregate character loading system
  const loadCharacters = async () => {
    try {
      setLoading(true);
      setError(null);
      console.log('🔄 CharacterSelector: Starting aggregate character loading...');

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
        console.log('🔍 CharacterSelector: Loading global characters...');
        const response = await fetch('/api/character/global');

        if (response.ok) {
          const globalCharacters = await response.json();

          if (Array.isArray(globalCharacters) && globalCharacters.length > 0) {
            aggregatedCharacters = globalCharacters;
            console.log(`✅ CharacterSelector: Loaded ${globalCharacters.length} global characters:`,
              globalCharacters.map(c => c.name));
          } else {
            console.log('❌ CharacterSelector: No global characters found');
          }
        } else {
          console.log(`❌ CharacterSelector: Failed to fetch global characters: ${response.status}`);
        }
      } catch (error) {
        console.log('❌ CharacterSelector: Error fetching global characters:', error);
      }

      // If no global characters found, provide default GLaDOS as fallback
      if (aggregatedCharacters.length === 0) {
        aggregatedCharacters = [defaultGLaDOS];
        console.log('✅ CharacterSelector: Using default GLaDOS character as fallback');
      }

      console.log(`🎉 CharacterSelector: Aggregate loading complete! Total characters: ${aggregatedCharacters.length}`);
      console.log(`📋 CharacterSelector: Final character list:`, aggregatedCharacters.map(c => `${c.name} (${c.id})`));

      setCharacters(aggregatedCharacters);

    } catch (error) {
      console.error('💥 CharacterSelector: Critical error in aggregate loading:', error);

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

      setCharacters([fallbackGLaDOS]);
      console.log('🆘 CharacterSelector: Using ultimate fallback GLaDOS character');

    } finally {
      setLoading(false);
    }
  };

  // Calculate selected character
  const selectedCharacter = selectedCharacterId
    ? characters.find(c => c.id === selectedCharacterId) || null
    : null;

  const handleCharacterSelect = (character: Character | null) => {
    onCharacterSelect(character);
    setIsOpen(false);
  };

  return (
    <div className="relative">
      <button
        onClick={() => setIsOpen(!isOpen)}
        disabled={disabled || loading}
        className="flex items-center space-x-2 px-3 py-2 bg-white border border-gray-300 rounded-lg hover:bg-gray-50 focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 disabled:opacity-50 disabled:cursor-not-allowed min-w-[200px]"
      >
        <div className="flex items-center space-x-2 flex-grow">
          {selectedCharacter ? (
            <>
              <Bot size={16} className="text-blue-500" />
              <div className="text-left">
                <div className="text-sm font-medium text-gray-900 truncate">
                  {selectedCharacter.name}
                </div>
                <div className="text-xs text-gray-500 truncate">
                  {selectedCharacter.description || 'AI Character'}
                </div>
              </div>
            </>
          ) : (
            <>
              <User size={16} className="text-gray-400" />
              <div className="text-left">
                <div className="text-sm font-medium text-gray-900">
                  Default (GLaDOS)
                </div>
                <div className="text-xs text-gray-500">
                  Default AI assistant
                </div>
              </div>
            </>
          )}
        </div>
        <ChevronDown
          size={16}
          className={`text-gray-400 transition-transform ${isOpen ? 'rotate-180' : ''}`}
        />
      </button>

      {isOpen && (
        <div className="absolute top-full left-0 right-0 mt-1 bg-white border border-gray-300 rounded-lg shadow-lg z-50 max-h-64 overflow-y-auto">
          {loading ? (
            <div className="p-3 text-center text-gray-500">
              Loading characters...
            </div>
          ) : error ? (
            <div className="p-3 text-center text-red-500">
              {error}
            </div>
          ) : (
            <>
              {/* No default option needed - GLaDOS is now in the character list */}

              {/* Character options */}
              {characters.map((character) => (
                <button
                  key={character.id}
                  onClick={() => handleCharacterSelect(character)}
                  className={`w-full flex items-center space-x-3 px-3 py-2 text-left hover:bg-gray-50 ${
                    selectedCharacterId === character.id ? 'bg-blue-50 border-r-2 border-blue-500' : ''
                  }`}
                >
                  <Bot size={16} className="text-blue-500" />
                  <div className="flex-grow min-w-0">
                    <div className="text-sm font-medium text-gray-900 truncate">
                      {character.name || 'Unnamed Character'}
                    </div>
                    <div className="text-xs text-gray-500 truncate">
                      {character.description || character.personality || 'Custom AI Character'}
                    </div>
                  </div>
                </button>
              ))}

              {characters.length === 0 && !loading && (
                <div className="p-3 text-center text-gray-500">
                  <div className="text-sm">No custom characters found</div>
                  <div className="text-xs mt-1">
                    Create characters in Settings to see them here
                  </div>
                </div>
              )}
            </>
          )}
        </div>
      )}

      {/* Click outside to close */}
      {isOpen && (
        <div
          className="fixed inset-0 z-40"
          onClick={() => setIsOpen(false)}
        />
      )}
    </div>
  );
};

export default CharacterSelector;
