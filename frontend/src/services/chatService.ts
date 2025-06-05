import apiService from './apiService';

/**
 * Service for chat-related API calls
 */
const chatService = {
  /**
   * Sends a message to the AI and gets a response
   * @param conversationId The ID of the conversation
   * @param message The message to send
   * @param characterId Optional character ID to use for this message
   * @returns The AI's response
   */
  async sendMessage(conversationId: string, message: string, characterId?: string | null): Promise<string> {
    try {
      // Skip API call if conversationId is not valid
      if (!conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid conversation ID for chat message, returning mock response');
        // Return a mock AI response
        return `I'm sorry, but I'm currently in demo mode and can't process your request: "${message}". Please ensure you have a valid conversation.`;
      }

      const requestBody: any = {
        conversationId,
        message
        // Note: userID removed - backend will use default user automatically
      };

      // Add characterId if provided and valid, otherwise use "default"
      if (characterId && characterId !== 'null' && characterId !== 'undefined') {
        requestBody.characterId = characterId;
      } else {
        // When no character is selected, explicitly request the default character
        requestBody.characterId = "default";
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
