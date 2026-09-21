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

const scheduleOperations = [
  ['/api/v1/schedules', 'post', 'CreateSchedule', '201', 'ScheduleResponse'],
  ['/api/v1/schedules', 'get', 'ListSchedules', '200', 'ScheduleResponse'],
  ['/api/v1/schedules/{id}', 'get', 'GetSchedule', '200', 'ScheduleResponse'],
  [
    '/api/v1/reference-data/schedule-statuses',
    'get',
    'ListScheduleStatuses',
    '200',
    'ScheduleReferenceResponse',
  ],
  [
    '/api/v1/reference-data/schedule-period-types',
    'get',
    'ListSchedulePeriodTypes',
    '200',
    'ScheduleReferenceResponse',
  ],
  [
    '/api/v1/reference-data/schedule-quarters',
    'get',
    'ListScheduleQuarters',
    '200',
    'ScheduleReferenceResponse',
  ],
]

const inspectionOperations = [
  [
    '/api/v1/inspections',
    'get',
    'ListInspections',
    '200',
    'InspectionResponse',
  ],
  [
    '/api/v1/inspections/{id}',
    'get',
    'GetInspection',
    '200',
    'InspectionResponse',
  ],
  [
    '/api/v1/inspections/history/{assetId}',
    'get',
    'GetInspectionHistory',
    '200',
    'InspectionHistoryResponse',
  ],
]

const preventiveMaintenanceFormOperations = [
  [
    '/api/v1/preventive-maintenance-forms',
    'post',
    'CreatePreventiveMaintenanceFormDraft',
  ],
  [
    '/api/v1/preventive-maintenance-forms',
    'get',
    'ListPreventiveMaintenanceForms',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}',
    'get',
    'GetPreventiveMaintenanceForm',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/submit',
    'post',
    'SubmitPreventiveMaintenanceForm',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/acknowledge',
    'post',
    'AcknowledgePreventiveMaintenanceForm',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/corrective-handoff',
    'get',
    'GetCorrectiveMaintenanceHandoff',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/inspections',
    'post',
    'AddPreventiveMaintenanceFormDraftInspection',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/inspections/{inspectionId}',
    'put',
    'UpdatePreventiveMaintenanceFormDraftInspection',
  ],
  [
    '/api/v1/preventive-maintenance-forms/{id}/inspections/{inspectionId}',
    'delete',
    'DeletePreventiveMaintenanceFormDraftInspection',
  ],
]

const pmPeriodDashboardOperations = [
  [
    '/api/v1/pm-period-dashboard/cycles',
    'get',
    'ListPmPeriodDashboardCycles',
    '200',
  ],
  ['/api/v1/pm-period-dashboard', 'get', 'GetPmPeriodDashboard', '200'],
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

for (const [
  path,
  method,
  operationId,
  status,
  schemaName,
] of scheduleOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required schedule operation: ${operationId}.`)
  }

  const schema =
    operation.responses?.[status]?.content?.['application/json']?.schema
  if (!schema) {
    throw new Error(
      `Required schedule operation ${operationId} is missing its JSON success schema.`,
    )
  }

  if (operationId.startsWith('List')) {
    if (
      schema.type !== 'array' ||
      schema.items?.$ref !== `#/components/schemas/${schemaName}`
    ) {
      throw new Error(
        `Required schedule operation ${operationId} must return ${schemaName}[].`,
      )
    }
  } else {
    if (schema.$ref !== `#/components/schemas/${schemaName}`) {
      throw new Error(
        `Required schedule operation ${operationId} must return ${schemaName}.`,
      )
    }
  }
}

for (const [
  path,
  method,
  operationId,
  status,
  schemaName,
] of inspectionOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required inspection operation: ${operationId}.`)
  }

  const schema =
    operation.responses?.[status]?.content?.['application/json']?.schema
  if (!schema) {
    throw new Error(
      `Required inspection operation ${operationId} is missing its JSON success schema.`,
    )
  }

  if (operationId === 'GetInspection') {
    if (schema.$ref !== `#/components/schemas/${schemaName}`) {
      throw new Error(
        `Required inspection operation ${operationId} must return ${schemaName}.`,
      )
    }
  } else if (
    schema.type !== 'array' ||
    schema.items?.$ref !== `#/components/schemas/${schemaName}`
  ) {
    throw new Error(
      `Required inspection operation ${operationId} must return ${schemaName}[].`,
    )
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

for (const [path, method, operationId] of preventiveMaintenanceFormOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(
      `Missing required preventive-maintenance form operation: ${operationId}.`,
    )
  }
}

for (const [path, method, operationId, status] of pmPeriodDashboardOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required PM dashboard operation: ${operationId}.`)
  }

  const schema =
    operation.responses?.[status]?.content?.['application/json']?.schema
  if (!schema) {
    throw new Error(
      `Required PM dashboard operation ${operationId} is missing its JSON success schema.`,
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

const scheduleFields = [
  'id',
  'assetId',
  'scheduleDate',
  'periodType',
  'status',
  'quarter',
  'semester',
  'year',
  'academicYear',
  'assignedToUserId',
  'completedAt',
  'createdAt',
  'updatedAt',
  'asset',
]
const scheduleProperties =
  snapshot.components?.schemas?.ScheduleResponse?.properties
if (
  !scheduleProperties ||
  scheduleFields.some((field) => !scheduleProperties[field])
) {
  throw new Error(
    'ScheduleResponse is missing one or more required public fields.',
  )
}

const scheduleAssetFields = [
  'id',
  'assetCode',
  'assetCategory',
  'building',
  'department',
  'location',
]
const scheduleAssetProperties =
  snapshot.components?.schemas?.ScheduleAssetResponse?.properties
if (
  !scheduleAssetProperties ||
  scheduleAssetFields.some((field) => !scheduleAssetProperties[field])
) {
  throw new Error(
    'ScheduleAssetResponse is missing one or more required public fields.',
  )
}

const inspectionFields = [
  'id',
  'scheduleId',
  'assetId',
  'inspectorUserId',
  'dateInspected',
  'isOperational',
  'remarks',
  'actionsRecommendations',
]
const inspectionProperties =
  snapshot.components?.schemas?.InspectionResponse?.properties
if (
  !inspectionProperties ||
  inspectionFields.some((field) => !inspectionProperties[field])
) {
  throw new Error(
    'InspectionResponse is missing one or more required public fields.',
  )
}

const formRowFields = [
  'id',
  'scheduleId',
  'assetId',
  'inspectorUserId',
  'dateInspected',
  'dateAccomplished',
  'isOperational',
  'remarks',
  'actionsRecommendations',
  'waterReplaceCarbonFilter',
  'waterReplaceSedimentFilter',
  'waterCheckUvLight',
  'assetCode',
  'location',
  'skilledWorkerIdentity',
]
const formRowProperties =
  snapshot.components?.schemas?.DraftInspectionRowResponse?.properties
if (
  !formRowProperties ||
  formRowFields.some((field) => !formRowProperties[field])
) {
  throw new Error(
    'DraftInspectionRowResponse is missing one or more required PMIS review fields.',
  )
}

const pmDashboardBatchFields = [
  'onTimeCompliancePercent',
  'fieldWorkCompletedAt',
]
const pmDashboardBatchProperties =
  snapshot.components?.schemas?.PmPeriodDashboardBatchResponse?.properties
if (
  !pmDashboardBatchProperties ||
  pmDashboardBatchFields.some((field) => !pmDashboardBatchProperties[field])
) {
  throw new Error(
    'PmPeriodDashboardBatchResponse is missing one or more acknowledgement-review fields.',
  )
}

const pmDashboardAssetFields = [
  'remarks',
  'actionsRecommendations',
]
const pmDashboardAssetProperties =
  snapshot.components?.schemas?.PmPeriodDashboardAssetRowResponse?.properties
if (
  !pmDashboardAssetProperties ||
  pmDashboardAssetFields.some((field) => !pmDashboardAssetProperties[field])
) {
  throw new Error(
    'PmPeriodDashboardAssetRowResponse is missing one or more acknowledgement-review fields.',
  )
}

console.log(
  'OpenAPI auth, asset, schedule, inspection, and preventive-maintenance form contract sanity check passed.',
)
