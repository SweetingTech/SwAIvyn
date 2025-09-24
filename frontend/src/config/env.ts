export interface AppEnv {
  readonly mode: string;
  readonly isDevelopment: boolean;
  readonly apiBaseUrl: string;
  readonly stagewiseEnabled: boolean;
}

interface RawImportMetaEnv {
  readonly MODE?: string;
  readonly VITE_API_BASE_URL?: string;
  readonly VITE_STAGEWISE_ENABLED?: string;
}

const rawEnv = (import.meta as unknown as { env?: RawImportMetaEnv }).env || {};
const mode = rawEnv.MODE ?? 'development';
const isDevelopment = mode === 'development';
const rawApiBaseUrl = (rawEnv.VITE_API_BASE_URL ?? '').trim();

if (!rawApiBaseUrl && !isDevelopment) {
  throw new Error('Missing required environment variable VITE_API_BASE_URL for production build.');
}

export const env: AppEnv = {
  mode,
  isDevelopment,
  apiBaseUrl: rawApiBaseUrl.replace(/\/$/, ''),
  stagewiseEnabled: rawEnv.VITE_STAGEWISE_ENABLED === 'true',
};
