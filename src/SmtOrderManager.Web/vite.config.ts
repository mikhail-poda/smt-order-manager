import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// The production build goes straight into the API's wwwroot, so the API serves the UI from the
// same origin. During development, the Vite server forwards /api to the API instead.
export default defineConfig({
  plugins: [react()],
  build: {
    outDir: '../SmtOrderManager.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    proxy: {
      '/api': 'http://localhost:5080',
    },
  },
});
