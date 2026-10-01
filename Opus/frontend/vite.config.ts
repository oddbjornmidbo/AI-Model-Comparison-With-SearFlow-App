import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// The API runs on :5080 by default (see backend launchSettings); override with SEATFLOW_API_URL.
// /api is proxied so no CORS setup is needed.
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: { '/api': process.env.SEATFLOW_API_URL ?? 'http://localhost:5080' },
  },
})
