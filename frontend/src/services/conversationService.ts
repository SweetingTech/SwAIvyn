import apiService from './apiService';

export interface Conversation {
  id: string;
  userId: string;
  title: string;
  folderId: string | null;
  createdAt: string;
  lastUpdated: string;
}

export interface Message {
  id: string;
  conversationId: string;
  role: string;
  content: string;
  timestamp: string;
}

/**
 * Service for conversation-related API calls
 */
const conversationService = {
  /**
   * Gets all conversations for a user
   * @param userId User ID
   * @returns List of conversations
   */
  async getConversations(userId: string): Promise<Conversation[]> {
    try {
      const response = await apiService.get(`/api/conversation/user/${userId}`);
      return Array.isArray(response) ? response : [];
    } catch (error: any) {
      // If it's a 404 or similar "not found" error, return empty array instead of throwing
      if (error.response && (error.response.status === 404 || error.response.status === 204)) {
        return [];
      }
      console.error('Error getting conversations:', error);
      return []; // Return empty array instead of throwing
    }
  },

  /**
   * Gets a conversation by ID
   * @param id Conversation ID
   * @returns The conversation
   */
  async getConversation(id: string): Promise<Conversation> {
    try {
      const response = await apiService.get(`/api/conversation/${id}`);
      return response;
    } catch (error) {
      console.error(`Error getting conversation ${id}:`, error);
      throw error;
    }
  },
  /**
   * Creates a new conversation
   * @param title Conversation title
   * @param folderId Optional folder ID
   * @returns The created conversation
   */
  async createConversation(title: string, folderId?: string): Promise<Conversation> {
    try {
      const requestData = {
        title,
        folderId
        // Note: userID removed - backend will use default user automatically
      };
      
      console.log('🔍 CreateConversation Request:', requestData);
      
      const response = await apiService.post('/api/conversation', requestData);
      
      console.log('🔍 CreateConversation Response:', response);
      
      return response;
    } catch (error) {
      console.error('Error creating conversation:', error);
      // Return a mock conversation with a temporary ID on error
      return {
        id: `temp-${Date.now()}`,
        userId: 'default-user', // Use a default value for mock
        title: title,
        folderId: folderId || null,
        createdAt: new Date().toISOString(),
        lastUpdated: new Date().toISOString()
      };
    }
  },

  /**
   * Updates a conversation's title
   * @param id Conversation ID
   * @param title New title
   * @returns Success status
   */
  async updateTitle(id: string, title: string): Promise<boolean> {
    try {
      const response = await apiService.put(`/api/conversation/${id}/title`, {
        title
      });
      return response && response.success;
    } catch (error) {
      console.error(`Error updating conversation title ${id}:`, error);
      throw error;
    }
  },

  /**
   * Updates a conversation's folder
   * @param id Conversation ID
   * @param folderId New folder ID
   * @returns Success status
   */
  async updateFolder(id: string, folderId: string | null): Promise<boolean> {
    try {
      const response = await apiService.put(`/api/conversation/${id}/folder`, {
        folderId
      });
      return response && response.success;
    } catch (error) {
      console.error(`Error updating conversation folder ${id}:`, error);
      throw error;
    }
  },

  /**
   * Deletes a conversation
   * @param id Conversation ID
   * @returns Success status
   */
  async deleteConversation(id: string): Promise<boolean> {
    try {
      const response = await apiService.delete(`/api/conversation/${id}`);
      return response === null; // DELETE returns null on success
    } catch (error) {
      console.error(`Error deleting conversation ${id}:`, error);
      throw error;
    }
  },

  /**
   * Gets the most recently opened conversation for a user
   * @param userId User ID
   * @returns The most recent conversation
   */
  async getRecentConversation(userId: string): Promise<Conversation | null> {
    // IMPORTANT: Skip API call completely for demo-user-id
    if (!userId || userId === 'demo-user-id') {
      console.warn('Demo user ID detected, skipping API call and returning null');
      return null;
    }

    try {
      // Get all conversations for the user
      const allConversations = await this.getConversations(userId);
      if (!allConversations || allConversations.length === 0) {
        // User has no conversations, return null
        return null;
      }

      // Find the most recent conversation by lastUpdated timestamp
      const sortedConversations = allConversations.sort((a, b) => 
        new Date(b.lastUpdated).getTime() - new Date(a.lastUpdated).getTime()
      );
      
      return sortedConversations[0];
    } catch (error: any) {
      console.error(`Error getting recent conversation for user ${userId}:`, error);
      return null; // Return null on error instead of throwing
    }
  },

  /**
   * Gets all messages for a conversation
   * @param conversationId Conversation ID
   * @returns List of messages
   */
  async getMessages(conversationId: string): Promise<Message[]> {
    try {
      // Validate conversationId
      if (!conversationId || conversationId === 'undefined' || conversationId === 'null') {
        console.warn('Invalid conversation ID for getMessages:', conversationId);
        return [];
      }

      const response = await apiService.get(`/api/conversation/${conversationId}/messages`);
      return response;
    } catch (error) {
      console.error(`Error getting messages for conversation ${conversationId}:`, error);
      return []; // Return empty array instead of throwing
    }
  },

  /**
   * Sets character context for a conversation
   * @param conversationId Conversation ID
   * @param userId User ID
   * @param characterId Character ID (optional)
   * @param systemPrompt Character system prompt
   * @returns Success status
   */
  async setCharacterContext(conversationId: string, userId: string, characterId: string | null, systemPrompt: string): Promise<boolean> {
    try {
      // Skip API call if userId or conversationId is not valid
      if (!userId || userId === 'demo-user-id' || !conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid IDs for character context, skipping');
        return false;
      }

      const response = await apiService.post('/api/conversation/character-context', {
        conversationId,
        userId,
        characterId,
        systemPrompt
      });
      return response && response.success;
    } catch (error) {
      console.error('Error setting character context:', error);
      throw error;
    }
  },  /**
   * Appends a message to a conversation
   * @param conversationId Conversation ID
   * @param userId User ID
   * @param role Message role (user, assistant, system)
   * @param content Message content
   * @returns The created message
   */  async appendMessage(conversationId: string, userId: string, role: string, content: string): Promise<Message> {
    try {
      // Skip API call if userId or conversationId is not valid
      if (!userId || userId === 'demo-user-id' || !conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid IDs for message append, returning mock message', {
          userId,
          conversationId,
          skippingBecause: !userId ? 'no userId' : 
                         userId === 'demo-user-id' ? 'demo user' :
                         !conversationId ? 'no conversationId' :
                         conversationId.startsWith('temp-') ? 'temp conversationId' : 'unknown'
        });
        // Return a mock message
        return {
          id: `temp-${Date.now()}`,
          conversationId: conversationId,
          role: role,
          content: content,
          timestamp: new Date().toISOString()
        };
      }

      // Validate conversationId format, but allow flexible userId formats (seeded dev users are not GUIDs)
      const guidRegex = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
      if (!guidRegex.test(conversationId)) {
        console.error('Invalid conversationId format:', conversationId);
        throw new Error(`Invalid conversation ID format: ${conversationId}`);
      }
      if (!userId) {
        console.error('Missing userId');
        throw new Error('Missing user ID');
      }

      const requestData = {
        conversationId: conversationId,
        userId: userId,
        role,
        content
      };

      console.log('🔍 AppendMessage Request Data:', {
        conversationId,
        userId,
        role,
        content,
        requestData
      });

      const response = await apiService.post('/api/conversation/message', requestData);
      return response;
    } catch (error) {
      console.error(`Error appending message to conversation ${conversationId}:`, error);
      console.error('Request data was:', { conversationId, userId, role, content });
      // Return a mock message on error
      return {
        id: `temp-${Date.now()}`,
        conversationId: conversationId,
        role: role,
        content: content,
        timestamp: new Date().toISOString()
      };
    }
  },

  /**
   * Updates the last open time for a conversation
   * @param id Conversation ID
   * @returns Success status
   */
  async updateLastOpenTime(id: string): Promise<boolean> {
    try {
      const response = await apiService.put(`/api/conversation/${id}/open`, {});
      return response && response.success;
    } catch (error) {
      console.error(`Error updating last open time for conversation ${id}:`, error);
      throw error;
    }
  }
};

export default conversationService;
