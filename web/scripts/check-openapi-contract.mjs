import { readFile } from 'node:fs/promises'
import { resolve } from 'node:path'

const snapshotPath =
  process.env.OPENAPI_SNAPSHOT_PATH ||
  process.argv[2] ||
  new URL('../openapi/unipm-v1.json', import.meta.url)

const snapshot = JSON.parse(
  await readFile(
    typeof snapshotPath === 'string' ? resolve(snapshotPath) : snapshotPath,
    'utf8',
  ),
)
const requiredOperations = [
  ['/api/v1/auth/login', 'post', 'Login', true],
  ['/api/v1/auth/refresh', 'post', 'RefreshSession', true],
  ['/api/v1/auth/logout', 'post', 'Logout', false],
  ['/api/v1/auth/me', 'get', 'GetCurrentUser', true],
]

const assetOperations = [
  ['/api/v1/assets', 'post', 'CreateAsset', '201', 'AssetResponse'],
  ['/api/v1/assets', 'get', 'ListAssets', '200', null],
  ['/api/v1/assets/{id}', 'get', 'GetAsset', '200', 'AssetResponse'],
  [
    '/api/v1/assets/by-qr/{qrCodeValue}',
    'get',
    'GetAssetByQr',
    '200',
    'AssetResponse',
  ],
]

for (const [path, method, operationId, requiresSchema] of requiredOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required auth operation: ${operationId}.`)
  }
  if (requiresSchema) {
    const schema =
      operation.responses?.['200']?.content?.['application/json']?.schema
    if (!schema) {
      throw new Error(
        `Required auth operation ${operationId} is missing its JSON success schema.`,
      )
    }
  }
}

for (const [path, method, operationId, status, schemaName] of assetOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required asset operation: ${operationId}.`)
  }

  const schema =
    operation.responses?.[status]?.content?.['application/json']?.schema
  if (!schema) {
    throw new Error(
      `Required asset operation ${operationId} is missing its JSON success schema.`,
    )
  }

  if (schemaName) {
    if (schema.$ref !== `#/components/schemas/${schemaName}`) {
      throw new Error(
        `Required asset operation ${operationId} must return ${schemaName}.`,
      )
    }
  } else if (
    schema.type !== 'array' ||
    schema.items?.$ref !== '#/components/schemas/AssetResponse'
  ) {
    throw new Error(
      'Required asset operation ListAssets must return AssetResponse[].',
    )
  }
}

const assetFields = [
  'id',
  'assetCode',
  'assetCategory',
  'building',
  'department',
  'location',
  'qrCodeValue',
  'status',
  'createdAt',
  'updatedAt',
]
const assetProperties = snapshot.components?.schemas?.AssetResponse?.properties
if (!assetProperties || assetFields.some((field) => !assetProperties[field])) {
  throw new Error(
    'AssetResponse is missing one or more required public fields.',
  )
}

console.log('OpenAPI auth and asset contract sanity check passed.')
