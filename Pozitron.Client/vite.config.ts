import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'

// https://vite.dev/config/
export default defineConfig({
  plugins: [vue()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5088',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:5088',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
