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
      return response.data;
    } catch (error) {
      console.error('Error getting conversations:', error);
      throw error;
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
      return response.data;
    } catch (error) {
      console.error(`Error getting conversation ${id}:`, error);
      throw error;
    }
  },

  /**
   * Creates a new conversation
   * @param userId User ID
   * @param title Conversation title
   * @param folderId Optional folder ID
   * @returns The created conversation
   */
  async createConversation(userId: string, title: string, folderId?: string): Promise<Conversation> {
    try {
      // Skip API call if userId is not a valid GUID
      if (!userId || userId === 'demo-user-id') {
        console.warn('Invalid user ID for conversation creation');
        // Return a mock conversation with a temporary ID
        return {
          id: `temp-${Date.now()}`,
          userId: userId,
          title: title,
          folderId: folderId || null,
          createdAt: new Date().toISOString(),
          lastUpdated: new Date().toISOString()
        };
      }

      const response = await apiService.post('/api/conversation', {
        userId,
        title,
        folderId
      });
      return response.data;
    } catch (error) {
      console.error('Error creating conversation:', error);
      // Return a mock conversation with a temporary ID on error
      return {
        id: `temp-${Date.now()}`,
        userId: userId,
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
      return response.status === 200;
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
      return response.status === 200;
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
      return response.status === 204;
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
      const response = await apiService.get(`/api/conversation/recent/${userId}`);
      return response.data;
    } catch (error) {
      // If no recent conversation, return null instead of throwing
      if (error.response && error.response.status === 404) {
        return null;
      }
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
      const response = await apiService.get(`/api/conversation/${conversationId}/messages`);
      return response.data;
    } catch (error) {
      console.error(`Error getting messages for conversation ${conversationId}:`, error);
      throw error;
    }
  },

  /**
   * Appends a message to a conversation
   * @param conversationId Conversation ID
   * @param userId User ID
   * @param role Message role (user, assistant, system)
   * @param content Message content
   * @returns The created message
   */
  async appendMessage(conversationId: string, userId: string, role: string, content: string): Promise<Message> {
    try {
      // Skip API call if userId or conversationId is not valid
      if (!userId || userId === 'demo-user-id' || !conversationId || conversationId.startsWith('temp-')) {
        console.warn('Invalid IDs for message append, returning mock message');
        // Return a mock message
        return {
          id: `temp-${Date.now()}`,
          conversationId: conversationId,
          role: role,
          content: content,
          timestamp: new Date().toISOString()
        };
      }

      const response = await apiService.post('/api/conversation/message', {
        conversationId,
        userId,
        role,
        content
      });
      return response.data;
    } catch (error) {
      console.error(`Error appending message to conversation ${conversationId}:`, error);
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
      return response.status === 200;
    } catch (error) {
      console.error(`Error updating last open time for conversation ${id}:`, error);
      throw error;
    }
  }
};

export default conversationService;
