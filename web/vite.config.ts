/// <reference types="vitest/config" />
import { tanstackRouter } from '@tanstack/router-plugin/vite'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'
import path from 'node:path'

// https://vite.dev/config/
export default defineConfig({
  // One local environment file at the repository root; only VITE_ reaches clients.
  envDir: path.resolve(__dirname, '..'),
  plugins: [
    tanstackRouter({
      target: 'react',
      autoCodeSplitting: true,
      routeFileIgnorePattern:
        '(\\.test\\.[jt]sx?$|guard\\.ts$|login-redirect\\.ts$)',
    }),
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    strictPort: true,
  },
  test: {
    pool: 'vmThreads',
    maxWorkers: 2,
    exclude: ['e2e/**', 'node_modules/**'],
    environment: 'jsdom',
    env: { VITE_API_BASE_URL: 'http://localhost:5000' },
    globals: true,
    setupFiles: ['./src/test/polyfill.ts', './src/test/setup.ts'],
    restoreMocks: true,
    coverage: {
      provider: 'v8',
      exclude: [
        'src/api/generated/**',
        'src/routeTree.gen.ts',
        'src/test/**',
        '**/*.test.{ts,tsx}',
      ],
    },
  },
})
