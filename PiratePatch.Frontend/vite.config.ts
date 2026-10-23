import { defineConfig } from 'vite'
import { svelte } from '@sveltejs/vite-plugin-svelte'
import { compression } from 'vite-plugin-compression2'

export default defineConfig({
  // nginx serves the .gz files next to the originals (gzip_static), so the server never compresses at request time.
  plugins: [svelte(), compression({ algorithms: ['gzip'] })],
  server: {
    proxy: {
      // The API; the browser only ever talks to one origin, in development as in production.
      '/api': { target: 'http://localhost:5001', changeOrigin: false },
    },
  },
})
