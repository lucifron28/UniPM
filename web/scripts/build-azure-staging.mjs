import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { build } from 'vite'

const scriptDirectory = path.dirname(fileURLToPath(import.meta.url))
const webRoot = path.resolve(scriptDirectory, '..')
const emptyEnvDirectory = process.argv[2]

if (!emptyEnvDirectory) {
  throw new Error(
    'Pass an empty environment directory for the production build.',
  )
}

if (process.env.VITE_API_BASE_URL !== '/') {
  throw new Error('The packaged frontend must use the same-origin API base /.')
}

await build({
  root: webRoot,
  configFile: path.join(webRoot, 'vite.config.ts'),
  envDir: path.resolve(emptyEnvDirectory),
  mode: 'production',
})
