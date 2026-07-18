import { defineConfig } from 'orval'

export default defineConfig({
  unipm: {
    input: { target: './openapi/unipm-v1.json' },
    output: {
      client: 'react-query',
      httpClient: 'axios',
      mode: 'split',
      target: './src/api/generated/endpoints.ts',
      schemas: './src/api/generated/models',
      clean: true,
      prettier: true,
      override: {
        mutator: { path: './src/api/http-client.ts', name: 'customInstance' },
      },
    },
  },
})
