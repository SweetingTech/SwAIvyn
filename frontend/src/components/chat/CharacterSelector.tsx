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
    if (userId && userId !== 'demo-user-id') {
      loadCharacters();
    }
  }, [userId]);

  // Auto-select character from localStorage when characters are loaded
  useEffect(() => {
    if (characters.length > 0 && !selectedCharacterId) {
      const storedCharacterId = localStorage.getItem('selectedCharacterId');
      if (storedCharacterId && storedCharacterId !== 'null') {
        const storedCharacter = characters.find(c => c.id === storedCharacterId);
        if (storedCharacter) {
          onCharacterSelect(storedCharacter);
        }
      }
    }
  }, [characters, selectedCharacterId, onCharacterSelect]);

  const loadCharacters = async () => {
    try {
      setLoading(true);
      setError(null);
      const response = await apiService.get(`/api/character/user/${userId}`);

      // Handle different response formats
      let charactersData = [];
      if (Array.isArray(response)) {
        charactersData = response;
      } else if (response && Array.isArray(response.data)) {
        charactersData = response.data;
      } else if (response && response.characters && Array.isArray(response.characters)) {
        charactersData = response.characters;
      }

      console.log('Characters loaded:', charactersData);
      setCharacters(charactersData);
    } catch (err) {
      console.error('Error loading characters:', err);
      setError('Failed to load characters');
      setCharacters([]);
    } finally {
      setLoading(false);
    }
  };

  const selectedCharacter = Array.isArray(characters) ? characters.find(c => c.id === selectedCharacterId) : null;

  const handleCharacterSelect = (character: Character | null) => {
    onCharacterSelect(character);
    setIsOpen(false);
  };

  // Don't render in demo mode
  if (userId === 'demo-user-id') {
    return null;
  }

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
