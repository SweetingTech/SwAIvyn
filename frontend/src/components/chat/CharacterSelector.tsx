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
  userId: string;
  selectedCharacterId: string | null;
  onCharacterSelect: (character: Character | null) => void;
  disabled?: boolean;
}

/**
 * CharacterSelector component allows users to choose which AI character
 * to chat with from their available characters.
 */
const CharacterSelector: React.FC<CharacterSelectorProps> = ({
  userId,
  selectedCharacterId,
  onCharacterSelect,
  disabled = false
}) => {
  const [characters, setCharacters] = useState<Character[]>([]);
  const [isOpen, setIsOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Load characters when component mounts or userId changes
  useEffect(() => {
    console.log('CharacterSelector mounted with userId:', userId);
    if (userId) {
      loadCharacters();
    }
  }, [userId]);

  // Auto-select character from localStorage when characters are loaded
  useEffect(() => {
    console.log('useEffect triggered - characters:', characters, 'selectedCharacterId:', selectedCharacterId);

    // Enhanced safety checks for characters array
    if (!characters) {
      console.log('Characters is null/undefined, skipping auto-select');
      return;
    }

    if (!Array.isArray(characters)) {
      console.error('Characters is not an array in useEffect:', typeof characters, characters);
      // Force reset to empty array if it's not an array
      setCharacters([]);
      return;
    }

    if (characters.length === 0) {
      console.log('Characters array is empty, skipping auto-select');
      return;
    }

    if (selectedCharacterId) {
      console.log('Character already selected, skipping auto-select');
      return;
    }

    const storedCharacterId = localStorage.getItem('selectedCharacterId');
    console.log('Stored character ID from localStorage:', storedCharacterId);

    if (storedCharacterId && storedCharacterId !== 'null') {
      // Quadruple-check that characters is still an array before calling find
      if (Array.isArray(characters) && characters.length > 0) {
        console.log('Searching for stored character in array of length:', characters.length);
        try {
          const storedCharacter = characters.find(c => c && c.id === storedCharacterId);
          console.log('Found stored character:', storedCharacter);

          if (storedCharacter) {
            console.log('Auto-selecting character:', storedCharacter.name);
            onCharacterSelect(storedCharacter);
          } else {
            console.log('Stored character not found in current characters list');
          }
        } catch (findError) {
          console.error('Error in find operation during auto-select:', findError);
          console.error('Characters state at error time:', characters);
        }
      } else {
        console.error('Characters became non-array or empty before find call:', typeof characters, characters);
      }
    }
  }, [characters, selectedCharacterId, onCharacterSelect]);

  const loadCharacters = async () => {
    try {
      setLoading(true);
      setError(null);
      console.log('Loading characters for userId:', userId);

      // Add check for demo user ID to prevent API call
      if (userId === 'demo-user-id') {
        console.log('Demo user detected, using empty characters array');
        setCharacters([]);
        return;
      }

      // Skip API calls completely for invalid user IDs
      if (!userId || userId === 'null' || userId === 'undefined') {
        console.warn('Invalid userId, skipping character loading:', userId);
        setCharacters([]);
        return;
      }

      const response = await apiService.get(`/api/character/user/${userId}`);
      console.log('Raw API response type:', typeof response, 'isArray:', Array.isArray(response));
      console.log('Raw API response:', response);

      // Initialize with empty array as default
      let charactersData: Character[] = [];

      // Handle null/undefined response
      if (response === null || response === undefined) {
        console.warn('API response is null/undefined, using empty array');
        charactersData = [];
      }
      // Handle direct array response (this should be the normal case now)
      else if (Array.isArray(response)) {
        console.log('Response is a direct array with length:', response.length);
        charactersData = response;
      }
      // Handle unexpected response types
      else {
        console.error('Unexpected response type:', typeof response);
        console.error('Response value:', response);
        charactersData = [];
      }

      // Final validation that we have an array
      if (!Array.isArray(charactersData)) {
        console.error('CRITICAL: charactersData is not an array after processing!');
        console.error('Type:', typeof charactersData, 'Value:', charactersData);
        charactersData = [];
      }

      // Validate each character object has required properties
      const validatedCharacters = charactersData.filter(char => {
        if (!char || typeof char !== 'object') {
          console.warn('Invalid character object:', char);
          return false;
        }
        if (!char.id || typeof char.id !== 'string') {
          console.warn('Character missing valid id:', char);
          return false;
        }
        return true;
      });

      console.log(`Final characters data: ${validatedCharacters.length} valid characters out of ${charactersData.length} total`);

      // Ensure we ALWAYS set an array, never anything else
      const finalCharacters = Array.isArray(validatedCharacters) ? validatedCharacters : [];
      console.log('Setting characters state to array with length:', finalCharacters.length);
      setCharacters(finalCharacters);

    } catch (err) {
      console.error('Error loading characters:', err);
      setError('Failed to load characters');
      // Always set empty array on error
      setCharacters([]);
    } finally {
      setLoading(false);
    }
  };

  // Calculate selected character with additional safety checks
  const selectedCharacter = (() => {
    if (!selectedCharacterId) {
      return null;
    }

    if (!characters) {
      console.warn('Characters is null/undefined when calculating selectedCharacter');
      return null;
    }

    if (!Array.isArray(characters)) {
      console.error('Characters is not an array when calculating selectedCharacter:', typeof characters, characters);
      return null;
    }

    try {
      return characters.find(c => c && c.id === selectedCharacterId) || null;
    } catch (error) {
      console.error('Error in find operation for selectedCharacter:', error);
      return null;
    }
  })();

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
              {/* Default option */}
              <button
                onClick={() => handleCharacterSelect(null)}
                className={`w-full flex items-center space-x-3 px-3 py-2 text-left hover:bg-gray-50 ${
                  !selectedCharacterId ? 'bg-blue-50 border-r-2 border-blue-500' : ''
                }`}
              >
                <User size={16} className="text-gray-400" />
                <div>
                  <div className="text-sm font-medium text-gray-900">
                    Default (GLaDOS)
                  </div>
                  <div className="text-xs text-gray-500">
                    Default AI assistant
                  </div>
                </div>
              </button>

              {/* Character options */}
              {Array.isArray(characters) && characters.map((character) => (
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

              {(!Array.isArray(characters) || characters.length === 0) && !loading && (
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
