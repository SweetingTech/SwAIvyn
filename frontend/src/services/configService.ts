interface Endpoints {
  api: string;
  chatHub: string;
  voiceHub: string;
  notificationHub: string;
  [key: string]: string;
}

class ConfigService {
  private static instance: ConfigService;
  private endpoints: Endpoints | null = null;
  private isLoading = false;
  private loadPromise: Promise<Endpoints> | null = null;

  private constructor() {}

  public static getInstance(): ConfigService {
    if (!ConfigService.instance) {
      ConfigService.instance = new ConfigService();
    }
    return ConfigService.instance;
  }

  public async getEndpoints(): Promise<Endpoints> {
    if (this.endpoints) {
      return this.endpoints;
    }

    if (this.isLoading) {
      return this.loadPromise!;
    }

    this.isLoading = true;
    this.loadPromise = this.fetchEndpoints();
    
    try {
      this.endpoints = await this.loadPromise;
      return this.endpoints;
    } finally {
      this.isLoading = false;
    }
  }

  public async getApiBaseUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.api;
  }

  public async getChatHubUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.chatHub;
  }

  public async getVoiceHubUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.voiceHub;
  }

  public async getNotificationHubUrl(): Promise<string> {
    const endpoints = await this.getEndpoints();
    return endpoints.notificationHub;
  }

  public async getModels(engine: string) {
    // Always use relative path so requests route through frontend proxy (Nginx) to backend
    const response = await fetch(`/api/llm/models?engine=${engine}`);
    if (response.ok) {
      return response.json();
    }
    throw new Error('Failed to fetch models');
  }

  private async fetchEndpoints(): Promise<Endpoints> {
    try {
      // First try to fetch from the backend
      const response = await fetch('/api/config');
      
      if (response.ok) {
        return await response.json();
      }
    } catch (error) {
      console.warn('Failed to fetch configuration from server, using defaults', error);
    }

    // Fallback to default values if the fetch fails
    return {
      api: '/api',
      chatHub: '/hubs/chat',
      voiceHub: '/hubs/voice',
      notificationHub: '/hubs/notification'
    };
  }
}

export default ConfigService.getInstance();
