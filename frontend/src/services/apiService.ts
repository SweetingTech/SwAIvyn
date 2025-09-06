import axios from 'axios';

/**
 * Helper function to check if a URL contains demo-user-id
 * @param url The URL to check
 * @returns True if the URL contains demo-user-id
 */
const isDemoUserRequest = (url: string): boolean => {
  return url.includes('demo-user-id');
};

/**
 * Helper function to create a mock response for demo mode
 * @returns A mock axios response
 */
const createMockResponse = () => {
  return {
    data: [],
    status: 200,
    statusText: 'OK',
    headers: {},
    config: {}
  };
};

/**
 * Disallow direct external calls from the frontend. All traffic must go through backend.
 */
const isExternalUrl = (url: string): boolean => {
  if (!/^https?:\/\//i.test(url)) return false; // relative URLs are fine
  try {
    const target = new URL(url, window.location.origin);
    return target.origin !== window.location.origin;
  } catch {
    return true; // treat malformed absolute URLs as external
  }
};

/**
 * Base API service for making HTTP requests
 */
const apiService = {
  /**
   * Makes a GET request to the specified URL
   * @param url The URL to make the request to
   * @param config Optional axios config
   * @returns The response data
   */
  async get(url: string, config = {}) {
    // Skip actual API call for demo-user-id requests
    if (isDemoUserRequest(url)) {
      console.warn(`DEMO MODE: Skipping GET request to ${url}`);
      return createMockResponse().data;
    }

    // Disallow external URLs from the frontend
    if (isExternalUrl(url)) {
      throw new Error(`Blocked external GET from frontend: ${url}`);
    }

    try {
      const response = await axios.get(url, config);
      return response.data;
    } catch (error) {
      console.error(`GET request failed for ${url}:`, error);
      throw error;
    }
  },

  /**
   * Makes a POST request to the specified URL
   * @param url The URL to make the request to
   * @param data The data to send
   * @param config Optional axios config
   * @returns The response data
   */
  async post(url: string, data: any = {}, config = {}) {
    // Skip actual API call for demo-user-id requests
    if (isDemoUserRequest(url) || (data && typeof data === 'object' && 'userId' in data && data.userId === 'demo-user-id')) {
      console.warn(`DEMO MODE: Skipping POST request to ${url}`);

      // For conversation creation, return a mock conversation
      if (url === '/api/conversation') {
        return {
          id: `temp-${Date.now()}`,          userId: data?.userId || 'demo-user-id',
          title: data?.title || 'Demo Chat',
          folderId: data?.folderId || null,
          createdAt: new Date().toISOString(),
          lastUpdated: new Date().toISOString()
        };
      }

      // For chat messages, return a mock response
      if (url === '/api/conversation/chat') {
        return {
          response: `I'm in demo mode and can't process your request: "${data?.message || 'unknown'}". Please ensure you have a valid user account.`
        };
      }

      // For message append, return a mock message
      if (url === '/api/conversation/message') {
        return {
          id: `temp-${Date.now()}`,          conversationId: data?.conversationId || 'temp-id',
          role: data?.role || 'user',
          content: data?.content || '',
          timestamp: new Date().toISOString()
        };
      }

      return createMockResponse().data;
    }

    // Disallow external URLs from the frontend
    if (isExternalUrl(url)) {
      throw new Error(`Blocked external POST from frontend: ${url}`);
    }

    try {
      const response = await axios.post(url, data, config);
      return response.data;
    } catch (error) {
      console.error(`POST request failed for ${url}:`, error);
      // Log additional details for debugging
      if (axios.isAxiosError && axios.isAxiosError(error)) {
        console.error('Response status:', error.response?.status);
        console.error('Response data:', error.response?.data);
        console.error('Request data:', data);
      } else {
        console.error('Non-axios error:', error);
      }
      throw error;
    }
  },

  /**
   * Makes a PUT request to the specified URL
   * @param url The URL to make the request to
   * @param data The data to send
   * @param config Optional axios config
   * @returns The response data
   */
  async put(url: string, data = {}, config = {}) {
    // Skip actual API call for demo-user-id requests
    if (isDemoUserRequest(url) || (data && typeof data === 'object' && 'userId' in data && data.userId === 'demo-user-id')) {
      console.warn(`DEMO MODE: Skipping PUT request to ${url}`);
      return { success: true };
    }

    // Disallow external URLs from the frontend
    if (isExternalUrl(url)) {
      throw new Error(`Blocked external PUT from frontend: ${url}`);
    }

    try {
      const response = await axios.put(url, data, config);
      return response.data;
    } catch (error) {
      console.error(`PUT request failed for ${url}:`, error);
      throw error;
    }
  },

  /**
   * Makes a DELETE request to the specified URL
   * @param url The URL to make the request to
   * @param config Optional axios config
   * @returns The response data
   */
  async delete(url: string, config = {}) {
    // Skip actual API call for demo-user-id requests
    if (isDemoUserRequest(url)) {
      console.warn(`DEMO MODE: Skipping DELETE request to ${url}`);
      return null;
    }

    // Disallow external URLs from the frontend
    if (isExternalUrl(url)) {
      throw new Error(`Blocked external DELETE from frontend: ${url}`);
    }

    try {
      const response = await axios.delete(url, config);
      return response.data;
    } catch (error) {
      try {
        if ((axios as any).isAxiosError && (axios as any).isAxiosError(error)) {
          const status = (error as any)?.response?.status;
          if (status === 404) {
            console.warn(`DELETE 404 for ${url} (suppressing error log)`);
          } else {
            console.error(`DELETE request failed for ${url}:`, error);
          }
        } else {
          console.error(`DELETE request failed for ${url}:`, error);
        }
      } catch {
        // fallback log
        console.error(`DELETE request failed for ${url}:`, error);
      }
      throw error;
    }
  }
};

export default apiService;
