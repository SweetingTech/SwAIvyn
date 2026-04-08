import React, { useState, useEffect, ChangeEvent, useRef } from 'react';
import yaml from 'js-yaml';
import apiService from '../services/apiService';

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
 * Supports 3D model (VRM / glTF) uploads via the `avatar` YAML field.
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

  // 3D model upload state
  const [model3dFile, setModel3dFile] = useState<File | null>(null);
  const [model3dPreviewUrl, setModel3dPreviewUrl] = useState<string | null>(null);
  const [model3dError, setModel3dError] = useState<string | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Generate system prompt and character name from YAML
  const systemPrompt = useYamlToPrompt(character.yamlProfile);
  const characterName = useCharacterName(character.yamlProfile);

  // Fetch character data if editing an existing character
  useEffect(() => {
    if (characterId) {
      apiService.get(`/api/character/${characterId}`)
        .then(data => {
          if (data) setCharacter(data);
        })
        .catch(console.error);
    }
  }, [characterId]);

  // Revoke 3D model object URL on unmount to avoid memory leaks
  useEffect(() => {
    return () => {
      if (model3dPreviewUrl) {
        URL.revokeObjectURL(model3dPreviewUrl);
      }
    };
  }, [model3dPreviewUrl]);

  // Handle YAML input changes
  const handleChange = (e: ChangeEvent<HTMLTextAreaElement>) => {
    setCharacter(prev => ({ ...prev, yamlProfile: e.target.value }));
  };

  // Handle 3D model file selection (VRM / glTF)
  const handle3dModelChange = (e: ChangeEvent<HTMLInputElement>) => {
    setModel3dError(null);
    const file = e.target.files?.[0];
    if (!file) return;

    const allowedExtensions = ['.vrm', '.glb', '.gltf'];
    const ext = file.name.toLowerCase().slice(file.name.lastIndexOf('.'));
    if (!allowedExtensions.includes(ext)) {
      setModel3dError('Please upload a VRM, GLB, or GLTF file.');
      return;
    }
    const maxBytes = 100 * 1024 * 1024; // 100 MB
    if (file.size > maxBytes) {
      setModel3dError('File must be smaller than 100 MB.');
      return;
    }

    const objectUrl = URL.createObjectURL(file);
    setModel3dFile(file);
    setModel3dPreviewUrl(objectUrl);

    // Inject the object URL into the YAML `avatar` field so the 3D scene can use it.
    setCharacter(prev => {
      try {
        const parsed = yaml.load(prev.yamlProfile) as Record<string, unknown> ?? {};
        parsed['avatar_3d'] = objectUrl;
        return { ...prev, yamlProfile: yaml.dump(parsed) };
      } catch {
        return prev;
      }
    });
  };

  // Handle form submission to save YAML character profile
  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    try {
      // Attempt to parse YAML to validate it
      yaml.load(character.yamlProfile);

      const url = characterId ? `/api/character/${characterId}/yaml` : '/api/character/yaml';
      const requestBody = {
        yamlProfile: character.yamlProfile
      };

      if (characterId) {
        await apiService.put(url, requestBody);
      } else {
        await apiService.post(url, requestBody);
      }
      
      onSave();
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
avatar_3d: none
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

      {/* 3D Model Upload */}
      <div className="mt-4 p-4 border border-dashed border-gray-300 rounded-lg bg-gray-50">
        <h3 className="font-semibold text-sm mb-1">3D Avatar Model <span className="font-normal text-gray-500">(optional)</span></h3>
        <p className="text-xs text-gray-500 mb-2">
          Upload a <strong>VRM</strong>, <strong>GLB</strong>, or <strong>GLTF</strong> file to use a 3D model in the
          Voice Room. The file is loaded locally in the browser and its URL is stored in the{' '}
          <code className="bg-gray-100 px-1 rounded">avatar_3d</code> YAML field.
        </p>
        <div className="flex items-center gap-3 flex-wrap">
          <input
            ref={fileInputRef}
            type="file"
            accept=".vrm,.glb,.gltf"
            onChange={handle3dModelChange}
            className="hidden"
            id="avatar3d-upload"
          />
          <label
            htmlFor="avatar3d-upload"
            className="cursor-pointer px-3 py-1.5 text-sm bg-primary-500 text-white rounded hover:bg-primary-600 transition-colors"
          >
            Choose 3D Model
          </label>
          {model3dFile && (
            <span className="text-sm text-gray-700 truncate max-w-xs">
              ✅ {model3dFile.name}
            </span>
          )}
          {model3dPreviewUrl && (
            <a
              href={model3dPreviewUrl}
              target="_blank"
              rel="noopener noreferrer"
              className="text-xs text-primary-600 hover:underline"
            >
              Preview URL
            </a>
          )}
        </div>
        {model3dError && (
          <p className="mt-1 text-xs text-red-600">{model3dError}</p>
        )}
      </div>
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