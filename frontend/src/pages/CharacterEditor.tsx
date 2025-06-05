import React, { useState, useEffect, ChangeEvent } from 'react';
import yaml from 'js-yaml';

/**
 * Hook: Converts YAML to a system prompt string usable by LLMs.
 */
const useYamlToPrompt = (yamlString: string): string => {
  try {
    const parsed = yaml.load(yamlString) as any;
    if (!parsed || typeof parsed !== 'object') return '';

    return `You are roleplaying as the AI character below. Remain fully in character.

Name: ${parsed.name || 'Unknown Character'}
Description: ${parsed.description || ''}
Personality: ${parsed.personality || ''}
Scenario: ${parsed.scenario || ''}
Tags: ${(parsed.tags || []).join(', ')}
Talkativeness Level: ${parsed.talkativeness ?? '0.5'}

System Prompt: ${parsed.system_prompt || ''}
Post-History Instructions: ${parsed.post_history_instructions || ''}

Respond using the character's voice and personality at all times.

First Message:
${parsed.first_message || ''}

Example Dialogue:
${parsed.message_example || ''}`;
  } catch (error) {
    console.error('Failed to parse YAML for prompt:', error);
    return '';
  }
};

/**
 * Hook: Extracts character name from YAML
 */
const useCharacterName = (yamlString: string): string => {
  try {
    const parsed = yaml.load(yamlString) as any;
    return parsed?.name || 'Unnamed Character';
  } catch (error) {
    return 'Unnamed Character';
  }
};

interface Character {
  id: string;
  userId: string;
  name: string;
  yamlProfile: string;
  createdAt: string;
  lastModified: string;
}

interface CharacterEditorProps {
  userId: string;
  characterId?: string;
  onSave: () => void;
  onCancel?: () => void;
}

/**
 * CharacterEditor component allows creating and editing YAML-based AI character profiles.
 * Uses a single YAML input field for flexibility and full character customization.
 */
const CharacterEditor: React.FC<CharacterEditorProps> = ({ userId, characterId, onSave, onCancel }) => {
  const [character, setCharacter] = useState<Character>({
    id: '',
    userId,
    name: '',
    yamlProfile: '',
    createdAt: '',
    lastModified: ''
  });

  // Generate system prompt and character name from YAML
  const systemPrompt = useYamlToPrompt(character.yamlProfile);
  const characterName = useCharacterName(character.yamlProfile);

  // Fetch character data if editing an existing character
  useEffect(() => {
    if (characterId) {
      fetch(`/api/character/${characterId}`)
        .then(res => res.json())
        .then(data => {
          if (data) setCharacter(data);
        })
        .catch(console.error);
    }
  }, [characterId]);

  // Handle YAML input changes
  const handleChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    setCharacter(prev => ({ ...prev, yamlProfile: e.target.value }));
  };

  // Handle form submission to save YAML character profile
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      // Attempt to parse YAML to validate it
      yaml.load(character.yamlProfile);

      const method = characterId ? 'PUT' : 'POST';
      const url = characterId ? `/api/character/${characterId}/yaml` : '/api/character/yaml';
      const requestBody = {
        userId: character.userId,
        yamlProfile: character.yamlProfile
      };

      const response = await fetch(url, {
        method,
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(requestBody)
      });

      if (response.ok) {
        onSave();
      } else {
        const errorText = await response.text();
        alert('Failed to save character: ' + errorText);
      }
    } catch (error) {
      alert('Invalid YAML: ' + error);
    }
  };

  return (
    <form onSubmit={handleSubmit} className="p-4 max-w-3xl mx-auto">
      <h2 className="text-xl font-semibold mb-4">
        {characterId ? 'Edit' : 'Create'} Character (YAML Format)
      </h2>

      {characterName !== 'Unnamed Character' && (
        <div className="mb-4 p-3 bg-blue-50 border border-blue-200 rounded">
          <h3 className="font-medium text-blue-800">Character: {characterName}</h3>
        </div>
      )}

      <label className="block mb-2 font-semibold">Character YAML</label>
      <textarea
        value={character.yamlProfile}
        onChange={handleChange}
        rows={30}
        className="w-full border border-gray-300 rounded px-3 py-2 font-mono text-sm"
        placeholder={`name: Character Name
description: >
  Detailed description of your character's appearance, background, and traits.

personality: >
  Character's personality traits, behavior patterns, and quirks.

scenario: >
  The setting or situation where conversations take place.

first_message: >
  The character's opening greeting or message.

message_example: |
  <START>
  {{char}}: Hello there! How can I help you today?
  {{user}}: I'm looking for some advice.
  {{char}}: I'd be happy to help! What's on your mind?

creator_notes: >
  Notes about the character creation, inspiration, or usage tips.

tags:
  - Tag1
  - Tag2
  - Tag3

avatar: none
chat: ""
talkativeness: 0.5
favorite: false
spec: chara_card_v3
spec_version: "3.0"
character_version: "1.0"

extensions:
  world: ""
  depth_prompt:
    prompt: ""
    depth: 4
    role: ""
  alternate_greetings: []
  group_only_greetings: []`}
      />
      <div className="flex gap-4 mt-4">
        <button
          type="submit"
          className="bg-primary-500 text-white px-4 py-2 rounded hover:bg-primary-600"
        >
          Save Character
        </button>

        {onCancel && (
          <button
            type="button"
            onClick={onCancel}
            className="bg-gray-500 text-white px-4 py-2 rounded hover:bg-gray-600"
          >
            Cancel
          </button>
        )}

        {systemPrompt && (
          <button
            type="button"
            onClick={() => {
              // Store character context for chat
              localStorage.setItem('activeCharacter', JSON.stringify({
                id: character.id,
                name: characterName,
                systemPrompt: systemPrompt
              }));
              // Navigate to chat
              window.location.href = '/chat';
            }}
            className="bg-green-500 text-white px-4 py-2 rounded hover:bg-green-600"
          >
            Start Chat with {characterName}
          </button>
        )}
      </div>

      {systemPrompt && (
        <div className="mt-6">
          <h3 className="font-semibold mb-2">System Prompt Preview:</h3>
          <pre className="bg-gray-100 p-3 rounded overflow-x-auto text-sm whitespace-pre-wrap max-h-64 overflow-y-auto">
            {systemPrompt}
          </pre>
        </div>
      )}
    </form>
  );
};

export default CharacterEditor;