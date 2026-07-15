import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'node:path';

// https://vitejs.dev/config/
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');

  // Dev proxy target: the SISLAB API (http avoids self-signed cert friction).
  const apiTarget = env.VITE_API_PROXY_TARGET || 'http://localhost:5121';

  return {
    plugins: [react()],
    resolve: {
      alias: {
        '@': path.resolve(__dirname, './src'),
      },
    },
    server: {
      port: 5173,
      // Same-origin calls to /api are forwarded to the backend, avoiding CORS.
      // In this mode VITE_API_BASE_URL can stay empty (see shared/api/http.ts).
      proxy: {
        '/api': {
          target: apiTarget,
          changeOrigin: true,
          secure: false,
        },
      },
    },
    build: {
      rollupOptions: {
        output: {
          // Split heavy vendors so ECharts doesn't bloat the app entry chunk.
          manualChunks: {
            echarts: ['echarts', 'echarts-for-react'],
            react: ['react', 'react-dom', 'react-router-dom'],
            query: ['@tanstack/react-query'],
          },
        },
      },
    },
  };
});
