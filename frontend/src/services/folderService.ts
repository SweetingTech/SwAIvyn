import apiService from './apiService';

export interface Folder {
  id: string;
  userId: string;
  name: string;
  parentId: string | null;
  createdAt: string;
  lastUpdated: string;
}

/**
 * Service for folder-related API calls
 */
const folderService = {
  /**
   * Gets all folders for a user
   * @param userId User ID
   * @returns List of folders
   */
  async getFolders(userId: string): Promise<Folder[]> {
    try {
      // Skip API call if userId is not a valid GUID
      if (!userId || userId === 'demo-user-id') {
        console.warn('Invalid user ID for folder fetch, returning empty array');
        return [];
      }

      const response = await apiService.get(`/api/folder/user/${userId}`);
      return response.data;
    } catch (error) {
      console.error('Error getting folders:', error);
      // Return empty array on error instead of throwing
      return [];
    }
  },

  /**
   * Gets a folder by ID
   * @param id Folder ID
   * @returns The folder
   */
  async getFolder(id: string): Promise<Folder> {
    try {
      const response = await apiService.get(`/api/folder/${id}`);
      return response.data;
    } catch (error) {
      console.error(`Error getting folder ${id}:`, error);
      throw error;
    }
  },

  /**
   * Creates a new folder
   * @param userId User ID
   * @param name Folder name
   * @param parentId Optional parent folder ID
   * @returns The created folder
   */
  async createFolder(userId: string, name: string, parentId?: string): Promise<Folder> {
    try {
      const response = await apiService.post('/api/folder', {
        userId,
        name,
        parentId
      });
      return response.data;
    } catch (error) {
      console.error('Error creating folder:', error);
      throw error;
    }
  },

  /**
   * Updates a folder's name
   * @param id Folder ID
   * @param name New name
   * @returns Success status
   */
  async updateName(id: string, name: string): Promise<boolean> {
    try {
      const response = await apiService.put(`/api/folder/${id}/name`, {
        name
      });
      return response.status === 200;
    } catch (error) {
      console.error(`Error updating folder name ${id}:`, error);
      throw error;
    }
  },

  /**
   * Updates a folder's parent
   * @param id Folder ID
   * @param parentId New parent folder ID
   * @returns Success status
   */
  async updateParent(id: string, parentId: string | null): Promise<boolean> {
    try {
      const response = await apiService.put(`/api/folder/${id}/parent`, {
        parentId
      });
      return response.status === 200;
    } catch (error) {
      console.error(`Error updating folder parent ${id}:`, error);
      throw error;
    }
  },

  /**
   * Deletes a folder
   * @param id Folder ID
   * @returns Success status
   */
  async deleteFolder(id: string): Promise<boolean> {
    try {
      const response = await apiService.delete(`/api/folder/${id}`);
      return response.status === 204;
    } catch (error) {
      console.error(`Error deleting folder ${id}:`, error);
      throw error;
    }
  },

  /**
   * Gets or creates the root folder for a user
   * @param userId User ID
   * @returns The root folder
   */
  async getRootFolder(userId: string): Promise<Folder> {
    try {
      const response = await apiService.get(`/api/folder/root/${userId}`);
      return response.data;
    } catch (error) {
      console.error(`Error getting root folder for user ${userId}:`, error);
      throw error;
    }
  }
};

export default folderService;
