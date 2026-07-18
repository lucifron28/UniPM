import { mkdir, writeFile } from 'node:fs/promises'

const source =
  process.env.UNIPM_OPENAPI_URL ?? 'http://localhost:5000/openapi/v1.json'
const url = new URL(source)
if (
  !['http:', 'https:'].includes(url.protocol) ||
  url.username ||
  url.password
) {
  throw new Error(
    'UNIPM_OPENAPI_URL must be an HTTP or HTTPS URL without embedded credentials.',
  )
}

const response = await fetch(url)
if (!response.ok)
  throw new Error(`OpenAPI pull failed with HTTP ${response.status}.`)
const document = await response.json()
if (
  typeof document.openapi !== 'string' ||
  typeof document.info !== 'object' ||
  typeof document.paths !== 'object'
) {
  throw new Error('The response is not a valid OpenAPI document.')
}
await mkdir(new URL('../openapi/', import.meta.url), { recursive: true })
await writeFile(
  new URL('../openapi/unipm-v1.json', import.meta.url),
  `${JSON.stringify(document, null, 2)}\n`,
)
console.log(`Saved OpenAPI snapshot from ${url.origin}${url.pathname}.`)
