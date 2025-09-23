import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  // Default to BFF on correct port for environment
  const proxyTarget = env.VITE_PROXY_TARGET || 'http://localhost:8000';

  return {
    plugins: [react()],
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
