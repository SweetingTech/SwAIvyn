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
  llmEngine?: string;
  llmModel?: string;
  ttsProvider?: string; // Optional as per backend UpdateChatSettingsRequest
  ttsVoiceId?: string;  // Optional as per backend UpdateChatSettingsRequest
  enabledEngines?: Record<string, boolean>;
  engineModels?: Record<string, string>;
}

/**
 * Service for chat-related API calls
 */
const chatService = {
  /**
   * Sends a message to the AI and gets a response
   * @param conversationId The ID of the conversation
   * @param message The message to send
   * @param userId The ID of the user sending the message
   * @param characterId Optional character ID to use for this message
   * @param engineOverride Optional engine to override default LLM for this message
   * @param modelOverride Optional model to override default LLM for this message
   * @returns The AI's response
   */
  async sendMessage(
    conversationId: string,
    message: string,
    userId?: string | null,
    characterId?: string | null,
    engineOverride?: string | null,
    modelOverride?: string | null
  ): Promise<string> {
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
        userId: userId || null
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
      // Strip undefined or empty values so we don't overwrite existing settings unintentionally
      const payload = Object.fromEntries(
        Object.entries(settings).filter(([, v]) => v !== undefined && v !== '')
      );

      console.log('🔄 ChatService: Updating chat settings at:', url, 'with payload:', payload);
      // The backend returns { message: "Chat settings updated successfully." } which apiService should handle.
      // We'll assume success if no error is thrown by apiService.put
      await apiService.put(url, payload);
      console.log('🔄 ChatService: Chat settings updated successfully.');
      return true;
    } catch (error) {
      console.error('Error updating chat settings:', error);
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
