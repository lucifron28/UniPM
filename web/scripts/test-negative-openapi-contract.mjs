import { execFileSync } from 'node:child_process'
import { readFile, writeFile, unlink } from 'node:fs/promises'
import { fileURLToPath } from 'node:url'
import { dirname, resolve } from 'node:path'

const __filename = fileURLToPath(import.meta.url)
const __dirname = dirname(__filename)
const snapshotPath = resolve(__dirname, '../openapi/unipm-v1.json')
const checkScriptPath = resolve(__dirname, './check-openapi-contract.mjs')

const originalRaw = await readFile(snapshotPath, 'utf8')

const cases = [
  {
    name: 'list 200 response schema removed',
    mutate: (data) => {
      delete data.paths['/api/v1/assets']['get'].responses['200'].content
    },
  },
  {
    name: 'detail 200 response changed to missing/void',
    mutate: (data) => {
      delete data.paths['/api/v1/assets/{id}']['get'].responses['200']
    },
  },
  {
    name: 'AssetResponse.assetCode removed',
    mutate: (data) => {
      delete data.components.schemas.AssetResponse.properties.assetCode
    },
  },
]

let passedCases = 0

for (let index = 0; index < cases.length; index++) {
  const c = cases[index]
  const tempPath = resolve(__dirname, `../openapi/temp-negative-${index}.json`)
  const copy = JSON.parse(originalRaw)
  c.mutate(copy)

  await writeFile(tempPath, JSON.stringify(copy, null, 2), 'utf8')

  try {
    execFileSync(process.execPath, [checkScriptPath, tempPath], {
      stdio: 'pipe',
    })
    console.error(
      `Negative OpenAPI contract check FAILED: Case "${c.name}" did not exit with error.`,
    )
    process.exitCode = 1
  } catch {
    console.log(
      `Negative OpenAPI contract check PASSED: Case "${c.name}" correctly failed.`,
    )
    passedCases++
  } finally {
    try {
      await unlink(tempPath)
    } catch {
      // Ignore cleanup error
    }
  }
}

if (passedCases === cases.length) {
  console.log(
    `All ${passedCases} negative OpenAPI contract checks passed successfully.`,
  )
} else {
  console.error(
    `Only ${passedCases}/${cases.length} negative contract checks passed.`,
  )
  process.exit(1)
}
