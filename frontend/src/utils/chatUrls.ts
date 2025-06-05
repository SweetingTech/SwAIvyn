/**
 * Utility functions for managing chat URLs with session and character information
 */

export interface ChatUrlParams {
  conversationId?: string;
  characterName?: string;
}

export interface ParsedChatUrl {
  conversationId: string | null;
  characterName: string | null;
  isNewConversation: boolean;
}

/**
 * Generates a chat URL with conversation and character information
 */
export function generateChatUrl(params: ChatUrlParams): string {
  const { conversationId = 'new', characterName } = params;
  
  if (characterName) {
    return `/chat/${conversationId}_${encodeURIComponent(characterName)}`;
  }
  
  return `/chat/${conversationId}`;
}

/**
 * Parses a chat URL parameter to extract conversation and character information
 */
export function parseChatUrl(sessionCharacter?: string): ParsedChatUrl {
  if (!sessionCharacter) {
    return {
      conversationId: null,
      characterName: null,
      isNewConversation: true
    };
  }

  // Split on the last underscore to handle character names with underscores
  const lastUnderscoreIndex = sessionCharacter.lastIndexOf('_');
  
  if (lastUnderscoreIndex === -1) {
    // No underscore found, treat as conversation ID only
    return {
      conversationId: sessionCharacter === 'new' ? null : sessionCharacter,
      characterName: null,
      isNewConversation: sessionCharacter === 'new'
    };
  }

  const conversationId = sessionCharacter.substring(0, lastUnderscoreIndex);
  const characterName = decodeURIComponent(sessionCharacter.substring(lastUnderscoreIndex + 1));

  return {
    conversationId: conversationId === 'new' ? null : conversationId,
    characterName,
    isNewConversation: conversationId === 'new'
  };
}

/**
 * Sanitizes a character name for use in URLs
 */
export function sanitizeCharacterName(name: string): string {
  return name.replace(/[^a-zA-Z0-9\-_]/g, '');
}

/**
 * Creates a default chat URL with the first available character
 */
export function createDefaultChatUrl(characterName?: string): string {
  return generateChatUrl({
    conversationId: 'new',
    characterName: characterName || 'GLaDOS'
  });
}
