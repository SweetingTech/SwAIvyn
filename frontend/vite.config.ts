import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  // Default to BFF on correct port for environment
  const proxyTarget = env.VITE_PROXY_TARGET || 'http://localhost:8000';

  return {
    plugins: [
      react(),
      VitePWA({
        registerType: 'autoUpdate',
        includeAssets: ['favicon.ico', 'default-avatar.png'],
        manifest: {
          name: 'SwAIvyn',
          short_name: 'SwAIvyn',
          description: 'SwAIvyn – Self-hosted AI assistant platform',
          theme_color: '#1e1e2e',
          background_color: '#1e1e2e',
          display: 'standalone',
          orientation: 'portrait',
          start_url: '/',
          scope: '/',
          icons: [
            {
              src: 'pwa-192.png',
              sizes: '192x192',
              type: 'image/png',
            },
            {
              src: 'pwa-512.png',
              sizes: '512x512',
              type: 'image/png',
            },
            {
              src: 'pwa-512.png',
              sizes: '512x512',
              type: 'image/png',
              purpose: 'any maskable',
            },
          ],
        },
        workbox: {
          // Cache API responses for offline graceful degradation
          runtimeCaching: [
            {
              // Cache conversation list and messages for offline access
              urlPattern: /^.*\/api\/conversation.*/i,
              handler: 'NetworkFirst',
              options: {
                cacheName: 'api-conversations',
                expiration: {
                  maxEntries: 100,
                  maxAgeSeconds: 24 * 60 * 60, // 24 hours
                },
                networkTimeoutSeconds: 5,
              },
            },
            {
              // Cache chat settings for offline access
              urlPattern: /^.*\/api\/chat\/settings.*/i,
              handler: 'NetworkFirst',
              options: {
                cacheName: 'api-settings',
                expiration: {
                  maxEntries: 20,
                  maxAgeSeconds: 24 * 60 * 60,
                },
                networkTimeoutSeconds: 5,
              },
            },
            {
              // Cache character data
              urlPattern: /^.*\/api\/characters.*/i,
              handler: 'NetworkFirst',
              options: {
                cacheName: 'api-characters',
                expiration: {
                  maxEntries: 50,
                  maxAgeSeconds: 24 * 60 * 60,
                },
                networkTimeoutSeconds: 5,
              },
            },
          ],
        },
        devOptions: {
          enabled: false,
        },
      }),
    ],
    optimizeDeps: {
      exclude: ['lucide-react'],
    },
    server: {
      host: '0.0.0.0', // listen on 0.0.0.0 so LAN clients can connect
      port: 5000,
      strictPort: true,
      proxy: {
        '/hubs': {
          target: proxyTarget,
          changeOrigin: true,
          ws: true,
        },
        '/api': {
          target: proxyTarget,
          changeOrigin: true,
        },
        // Serve uploaded/static assets from the backend during dev
        '/uploads': {
          target: proxyTarget,
          changeOrigin: true,
        }
      }
    },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    sourcemap: false,
    // Ensure assets use relative paths
    assetsDir: 'assets',
    rollupOptions: {
      output: {
        manualChunks: undefined
      }
    }
  },
  base: './' // Use relative paths
  };
});
