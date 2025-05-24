import apiService from './apiService';

/**
 * Service for chat-related API calls
 */
const chatService = {
  /**
   * Sends a message to the AI and gets a response
   * @param conversationId The ID of the conversation
   * @param userId The ID of the user
   * @param message The message to send
   * @param characterId Optional character ID to use for this message
   * @returns The AI's response
   */
  async sendMessage(conversationId: string, userId: string, message: string, characterId?: string | null): Promise<string> {
    try {
      // Skip API call if userId or conversationId is not valid
      if (!userId || userId === 'demo-user-id' || !conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid IDs for chat message, returning mock response');
        // Return a mock AI response
        return `I'm sorry, but I'm currently in demo mode and can't process your request: "${message}". Please ensure you have a valid user account to use the full functionality.`;
      }

      const requestBody: any = {
        conversationId,
        userId,
        message
      };

      // Add characterId if provided
      if (characterId) {
        requestBody.characterId = characterId;
      }

      const response = await apiService.post('/api/conversation/chat', requestBody);
      return response.data.response;
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
      const response = await apiService.get(url);
      return response.data;
    } catch (error) {
      console.error('Error getting LLM settings:', error);
      throw error;
    }
  },

  /**
   * Updates the LLM settings
   * @param engine The LLM engine (ollama or lmstudio)
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
      return response.status === 200;
    } catch (error) {
      console.error('Error updating LLM settings:', error);
      throw error;
    }
  }
};

export default chatService;
