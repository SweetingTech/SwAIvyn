import React, { useState, useEffect, ChangeEvent } from 'react';

interface Character {
  id: string;
  userId: string;
  name: string;
  imagePath: string;
  personality: string;
  voiceSettings: string;
}

interface CharacterEditorProps {
  userId: string;
  characterId?: string;
  onSave: () => void;
}

/**
 * CharacterEditor component allows creating and editing AI character profiles.
 * Fully commented and designed for integration with backend APIs.
 */
const CharacterEditor: React.FC<CharacterEditorProps> = ({ userId, characterId, onSave }) => {
  const [character, setCharacter] = useState<Character>({
    id: '',
    userId,
    name: '',
    imagePath: '',
    personality: '',
    voiceSettings: ''
  });

  // Fetch character data if editing an existing character
  useEffect(() => {
    if (characterId) {
      fetch(`/api/character/${userId}`)
        .then(res => res.json())
        .then(data => {
          const existing = data.find((c: Character) => c.id === characterId);
          if (existing) setCharacter(existing);
        })
        .catch(console.error);
    }
  }, [characterId, userId]);

  // Handle input changes
  const handleChange = (e: ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) => {
    const { name, value } = e.target;
    setCharacter(prev => ({ ...prev, [name]: value }));
  };

  // Handle form submission to save character
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    const method = characterId ? 'PUT' : 'POST';
    const url = characterId ? `/api/character/${characterId}` : '/api/character';
    const response = await fetch(url, {
      method,
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(character)
    });
    if (response.ok) {
      onSave();
    } else {
      alert('Failed to save character');
    }
  };

  return (
    <form onSubmit={handleSubmit} className="p-4 max-w-md mx-auto">
      <h2 className="text-xl font-semibold mb-4">{characterId ? 'Edit' : 'Create'} Character</h2>
      <div className="mb-4">
        <label className="block mb-1 font-semibold">Name</label>
        <input
          type="text"
          name="name"
          value={character.name}
          onChange={handleChange}
          className="w-full border border-gray-300 rounded px-3 py-2"
          required
        />
      </div>
      <div className="mb-4">
        <label className="block mb-1 font-semibold">Image Path</label>
        <input
          type="text"
          name="imagePath"
          value={character.imagePath}
          onChange={handleChange}
          className="w-full border border-gray-300 rounded px-3 py-2"
        />
      </div>
      <div className="mb-4">
        <label className="block mb-1 font-semibold">Personality</label>
        <textarea
          name="personality"
          value={character.personality}
          onChange={handleChange}
          className="w-full border border-gray-300 rounded px-3 py-2"
          rows={4}
        />
      </div>
      <div className="mb-4">
        <label className="block mb-1 font-semibold">Voice Settings</label>
        <input
          type="text"
          name="voiceSettings"
          value={character.voiceSettings}
          onChange={handleChange}
          className="w-full border border-gray-300 rounded px-3 py-2"
        />
      </div>
      <button
        type="submit"
        className="bg-primary-500 text-white px-4 py-2 rounded hover:bg-primary-600"
      >
        Save
      </button>
    </form>
  );
};

export default CharacterEditor;
