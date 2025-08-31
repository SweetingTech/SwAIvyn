import apiService from './apiService';

// Matches backend DTOs
export interface ChatSettings {
  llmEngine: string;
  llmModel: string;
  ttsProvider: string;
  ttsVoiceId: string;
  enabledEngines?: Record<string, boolean>;
  engineModels?: Record<string, string>;
}

export interface UpdateChatSettingsPayload {
  llmEngine: string;
  llmModel: string;
  ttsProvider?: string; // Optional as per backend UpdateChatSettingsRequest
  ttsVoiceId?: string;  // Optional as per backend UpdateChatSettingsRequest
}

/**
 * Service for chat-related API calls
 */
const chatService = {
  /**
   * Sends a message to the AI and gets a response
   * @param conversationId The ID of the conversation
   * @param message The message to send
   * @param characterId Optional character ID to use for this message
   * @param engineOverride Optional engine to override default LLM for this message
   * @param modelOverride Optional model to override default LLM for this message
   * @returns The AI's response
   */
  async sendMessage(conversationId: string, message: string, characterId?: string | null, engineOverride?: string | null, modelOverride?: string | null): Promise<string> {
    try {
      // Skip API call if conversationId is not valid
      if (!conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid conversation ID for chat message, returning mock response');
        // Return a mock AI response
        return `I'm sorry, but I'm currently in demo mode and can't process your request: "${message}". Please ensure you have a valid conversation.`;
      }

      const requestBody: any = {
        conversationId,
        message,
        userId: null // Backend handles user ID
      };

      if (characterId && characterId !== 'null' && characterId !== 'undefined') {
        requestBody.characterId = characterId;
      } else {
        requestBody.characterId = "default";
      }

      if (engineOverride) {
        requestBody.engine = engineOverride;
      }
      if (modelOverride) { // only include model if engine is also overridden
        requestBody.model = modelOverride;
      }

      const response = await apiService.post('/api/conversation/chat', requestBody);
      return response.response;
    } catch (error) {
      console.error('Error sending message:', error);
      // Return a friendly error message instead of throwing
      return "I'm sorry, I couldn't process your message due to a technical issue. Please try again later.";
    }
  },

  /**
   * Gets the consolidated chat settings for a user.
   * @param userId The ID of the user.
   * @returns The chat settings (LLM engine/model, TTS provider/voice).
   */
  async getChatSettings(userId: string): Promise<ChatSettings> {
    try {
      const url = `/api/chat/settings/${userId}`;
      console.log('🔄 ChatService: Getting chat settings from:', url);
      const response = await apiService.get(url);
      console.log('🔄 ChatService: Chat settings API response:', response);
      return response as ChatSettings;
    } catch (error) {
      console.error('Error getting chat settings:', error);
      throw error;
    }
  },

  /**
   * Updates the consolidated chat settings for a user.
   * @param userId The ID of the user.
   * @param settings The settings payload.
   * @returns True if successful, otherwise throws error.
   */
  async updateChatSettings(userId: string, settings: UpdateChatSettingsPayload): Promise<boolean> {
    try {
      const url = `/api/chat/settings/${userId}`;
      console.log('🔄 ChatService: Updating chat settings at:', url, 'with payload:', settings);
      // The backend returns { message: "Chat settings updated successfully." } which apiService should handle.
      // We'll assume success if no error is thrown by apiService.post
      await apiService.put(url, settings);
      console.log('🔄 ChatService: Chat settings updated successfully.');
      return true;
    } catch (error) {
      console.error('Error updating chat settings:', error);
      throw error;
    }
  },

  // Old methods - can be marked @deprecated or removed later if no longer used.
  /**
   * @deprecated Use getChatSettings instead.
   * Gets the current LLM settings
   * @param userId Optional user ID for user-specific settings
   * @returns The current LLM settings (engine and model)
   */
  async getLlmSettings(userId?: string): Promise<{ engine: string, model: string }> {
    try {
      const url = userId ? `/api/settings/llm?userId=${userId}` : '/api/settings/llm';
      console.log('🔄 Making LLM settings API call to:', url);
      console.log('🔄 UserId parameter:', userId);
      console.log('🔄 UserId type:', typeof userId);
      const response = await apiService.get(url);
      console.log('🔄 LLM settings API response:', response);
      return response;
    } catch (error) {
      console.error('Error getting LLM settings:', error);
      throw error;
    }
  },

  /**
   * @deprecated Use updateChatSettings instead.
   * Updates the LLM settings
   * @param engine The LLM engine (ollama, lmstudio, openai or claude)
   * @param model The LLM model (for Ollama)
   * @param userId Optional user ID for user-specific settings
   * @returns Success status
   */
  async updateLlmSettings(engine: string, model: string, userId?: string): Promise<boolean> {
    try {
      const response = await apiService.put('/api/settings/llm', {
        engine,
        model,
        userId
      });
      return response && response.success;
    } catch (error) {
      console.error('Error updating LLM settings:', error);
      throw error;
    }
  },

  async getLlmModels(engine: string) {
    try {
      return await apiService.get(`/api/llm/models?engine=${engine}`);
    } catch (error) {
      console.error('Error getting models:', error);
      throw error;
    }
  }
};

export default chatService;
