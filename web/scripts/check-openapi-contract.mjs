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
  ['/api/info', 'get', 'GetApiInfo', false],
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

const scheduleCoverageReviewOperation = [
  '/api/v1/schedules/coverage-review',
  'get',
  'ListScheduleCoverageReview',
  '200',
  'ScheduleCoverageReviewPage',
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
  [
    '/api/v1/inspections/{id}/wms-referral',
    'get',
    'GetInspectionWmsReferral',
    '200',
    'InspectionWmsReferralDetailResponse',
  ],
  [
    '/api/v1/inspections/{id}/wms-referral',
    'put',
    'UpdateInspectionWmsReferral',
    '200',
    'InspectionWmsReferralResponse',
  ],
]

const inspectionPhotoOperations = [
  ['/api/v1/inspections/{id}/photo', 'get', 'GetInspectionPhotoEvidence'],
  ['/api/v1/inspections/{id}/photo', 'put', 'ReplaceInspectionPhotoEvidence'],
  ['/api/v1/inspections/{id}/photo', 'delete', 'DeleteInspectionPhotoEvidence'],
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

const pmAnalyticsV2Operations = [
  [
    '/api/v1/analytics/pm/v2/interpret',
    'InterpretPmAnalyticsV2Question',
    'PmAnalyticsV2InterpretRequest',
    'PmAnalyticsV2InterpretationResponse',
  ],
  [
    '/api/v1/analytics/pm/v2/query',
    'QueryPmAnalyticsV2',
    'PmAnalyticsV2QueryRequest',
    'PmAnalyticsV2QueryResponse',
  ],
]

const pmAnalyticsV3Operations = [
  [
    '/api/v1/analytics/pm/v3/catalog',
    'get',
    'GetPmAnalyticsV3Catalog',
    null,
    'PmAnalyticsV3CatalogResponse',
  ],
  [
    '/api/v1/analytics/pm/v3/catalog/values',
    'get',
    'GetPmAnalyticsV3FilterValues',
    null,
    'PmAnalyticsV3FilterValuesResponse',
  ],
  [
    '/api/v1/analytics/pm/v3/interpret',
    'post',
    'InterpretPmAnalyticsV3Question',
    'PmAnalyticsV3InterpretRequest',
    'PmAnalyticsV3InterpretationResponse',
  ],
  [
    '/api/v1/analytics/pm/v3/preview',
    'post',
    'PreviewPmAnalyticsV3Report',
    'PmAnalyticsV3PreviewRequest',
    'PmAnalyticsV3PreviewResponse',
  ],
  [
    '/api/v1/analytics/pm/v3/execute',
    'post',
    'ExecutePmAnalyticsV3Report',
    'PmAnalyticsV3ExecuteRequest',
    'PmAnalyticsV3ExecutionResponse',
  ],
]

const registryOperations = [
  ['/api/v1/assets/registry', 'ListAssetRegistry', 'AssetRegistryPageResponse'],
  [
    '/api/v1/assets/registry/options',
    'GetAssetRegistryOptions',
    'AssetRegistryOptionsResponse',
  ],
  [
    '/api/v1/assets/registry/summary',
    'GetAssetRegistrySummary',
    'AssetRegistrySummaryResponse',
  ],
  [
    '/api/v1/schedules/registry',
    'ListScheduleRegistry',
    'ScheduleRegistryPageResponse',
  ],
  [
    '/api/v1/schedules/registry/options',
    'GetScheduleRegistryOptions',
    'ScheduleRegistryOptionsResponse',
  ],
  [
    '/api/v1/schedules/registry/summary',
    'GetScheduleRegistrySummary',
    'ScheduleRegistrySummaryResponse',
  ],
  [
    '/api/v1/inspections/registry',
    'ListInspectionRegistry',
    'InspectionRegistryPageResponse',
  ],
  [
    '/api/v1/inspections/registry/options',
    'GetInspectionRegistryOptions',
    'InspectionRegistryOptionsResponse',
  ],
  [
    '/api/v1/inspections/registry/summary',
    'GetInspectionRegistrySummary',
    'InspectionRegistrySummaryResponse',
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
  requestSchemaName,
  responseSchemaName,
] of pmAnalyticsV3Operations) {
  const operation = snapshot.paths?.[path]?.[method]
  const requestSchema = requestSchemaName
    ? operation?.requestBody?.content?.['application/json']?.schema
    : null
  const responseSchema =
    operation?.responses?.['200']?.content?.['application/json']?.schema

  if (
    operation?.operationId !== operationId ||
    (requestSchemaName &&
      requestSchema?.$ref !== `#/components/schemas/${requestSchemaName}`) ||
    responseSchema?.$ref !== `#/components/schemas/${responseSchemaName}`
  ) {
    throw new Error(
      `PM analytics V3 operation ${operationId} must use ${requestSchemaName ?? 'no request body'} and return ${responseSchemaName}.`,
    )
  }
}

const pmAnalyticsV3RequiredFields = {
  PmAnalyticsV3ReportPlan: [
    'schemaVersion',
    'catalogVersion',
    'subject',
    'mode',
    'period',
    'categories',
    'measures',
    'baseFilters',
    'matchingFilters',
    'aggregateFilters',
    'orderBy',
    'presentation',
    'columns',
  ],
  PmAnalyticsV3PreviewResponse: [
    'previewId',
    'expiresAtUtc',
    'plan',
    'cyclesByCategory',
    'summary',
    'scopeNotes',
  ],
  PmAnalyticsV3ExecutionResponse: [
    'schemaVersion',
    'catalogVersion',
    'asOfUtc',
    'plan',
    'cyclesByCategory',
    'mode',
    'columns',
    'population',
    'page',
    'pageSize',
    'totalPages',
    'aggregateRows',
    'records',
    'scopeNotes',
  ],
}

for (const [schemaName, fields] of Object.entries(
  pmAnalyticsV3RequiredFields,
)) {
  const properties = snapshot.components?.schemas?.[schemaName]?.properties
  if (!properties || fields.some((field) => !(field in properties))) {
    throw new Error(
      `PM analytics V3 schema ${schemaName} is missing a required field.`,
    )
  }
}

{
  const [path, method, operationId, status, schemaName] =
    scheduleCoverageReviewOperation
  const operation = snapshot.paths?.[path]?.[method]
  const schema =
    operation?.responses?.[status]?.content?.['application/json']?.schema
  if (
    operation?.operationId !== operationId ||
    schema?.$ref !== `#/components/schemas/${schemaName}`
  ) {
    throw new Error(
      `Required schedule operation ${operationId} must return ${schemaName}.`,
    )
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

  if (
    operationId === 'GetInspection' ||
    operationId === 'GetInspectionWmsReferral' ||
    operationId === 'UpdateInspectionWmsReferral'
  ) {
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

const inspectionListParameters =
  snapshot.paths?.['/api/v1/inspections']?.get?.parameters ?? []
if (
  !inspectionListParameters.some(
    (parameter) => parameter.name === 'wmsReferralStatus',
  )
) {
  throw new Error('ListInspections is missing the WMS follow-up status filter.')
}

const wmsReferralPut =
  snapshot.paths?.['/api/v1/inspections/{id}/wms-referral']?.put
const wmsReferralPutRequest =
  wmsReferralPut?.requestBody?.content?.['application/json']?.schema
if (
  wmsReferralPutRequest?.$ref !==
  '#/components/schemas/UpdateInspectionWmsReferralDto'
) {
  throw new Error(
    'UpdateInspectionWmsReferral is missing its revisioned JSON request DTO.',
  )
}

for (const [path, method, operationId] of inspectionPhotoOperations) {
  if (snapshot.paths?.[path]?.[method]?.operationId !== operationId) {
    throw new Error(
      `Missing required inspection photo operation: ${operationId}.`,
    )
  }
}

const inspectionPhotoPath = snapshot.paths?.['/api/v1/inspections/{id}/photo']
if (
  !inspectionPhotoPath?.get?.responses?.['200']?.content?.['image/jpeg'] ||
  !inspectionPhotoPath?.put?.requestBody?.content?.['image/jpeg']
) {
  throw new Error(
    'Inspection photo operations must use the image/jpeg media type.',
  )
}
for (const status of ['400', '401', '403', '404', '409']) {
  if (!wmsReferralPut?.responses?.[status]) {
    throw new Error(
      `UpdateInspectionWmsReferral is missing its ${status} response.`,
    )
  }
}

const wmsReferralGet =
  snapshot.paths?.['/api/v1/inspections/{id}/wms-referral']?.get
for (const status of ['401', '403', '404']) {
  if (!wmsReferralGet?.responses?.[status]) {
    throw new Error(
      `GetInspectionWmsReferral is missing its ${status} response.`,
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

for (const [
  path,
  operationId,
  requestSchemaName,
  responseSchemaName,
] of pmAnalyticsV2Operations) {
  const operation = snapshot.paths?.[path]?.post
  const requestSchema =
    operation?.requestBody?.content?.['application/json']?.schema
  const responseSchema =
    operation?.responses?.['200']?.content?.['application/json']?.schema

  if (
    operation?.operationId !== operationId ||
    requestSchema?.$ref !== `#/components/schemas/${requestSchemaName}` ||
    responseSchema?.$ref !== `#/components/schemas/${responseSchemaName}`
  ) {
    throw new Error(
      `PM analytics V2 operation ${operationId} must use ${requestSchemaName} and return ${responseSchemaName}.`,
    )
  }
}

const pmAnalyticsV2RequiredFields = {
  PmAnalyticsV2Plan: [
    'schemaVersion',
    'metric',
    'assetCategory',
    'period',
    'comparison',
    'presentation',
    'countBasis',
    'department',
    'building',
    'assetCode',
    'condition',
    'timeliness',
    'groupBy',
    'ranking',
  ],
  PmAnalyticsV2QueryRequest: ['plan', 'sourceScope', 'page', 'pageSize'],
  PmAnalyticsV2QueryResponse: [
    'asOfUtc',
    'plan',
    'population',
    'result',
    'cycles',
    'groups',
    'comparison',
    'sources',
    'scopeNote',
  ],
  PmAnalyticsV2PopulationResponse: [
    'eligibleAssetCycleCount',
    'eligibleDistinctAssetCount',
    'matchingAssetCycleCount',
    'matchingDistinctAssetCount',
    'sourceTotalCount',
    'totalPages',
  ],
  PmAnalyticsV2SourceResponse: [
    'assetCode',
    'currentDepartment',
    'currentBuilding',
    'pmCycle',
    'deadline',
    'completedAt',
    'scheduleStatus',
    'formStatus',
  ],
}

for (const [schemaName, fields] of Object.entries(
  pmAnalyticsV2RequiredFields,
)) {
  const properties = snapshot.components?.schemas?.[schemaName]?.properties
  if (!properties || fields.some((field) => !(field in properties))) {
    throw new Error(
      `PM analytics V2 schema ${schemaName} is missing a required field.`,
    )
  }
}

for (const [path, operationId, schemaName] of registryOperations) {
  const operation = snapshot.paths?.[path]?.get
  const schema =
    operation?.responses?.['200']?.content?.['application/json']?.schema
  if (
    operation?.operationId !== operationId ||
    schema?.$ref !== `#/components/schemas/${schemaName}`
  ) {
    throw new Error(
      `Registry operation ${operationId} must return ${schemaName}.`,
    )
  }
}

const registryFilterParameters = [
  [
    '/api/v1/assets/registry',
    [
      'assetCategory',
      'status',
      'building',
      'department',
      'search',
      'page',
      'pageSize',
    ],
  ],
  [
    '/api/v1/schedules/registry',
    [
      'assetId',
      'status',
      'quarter',
      'year',
      'pmCycle',
      'department',
      'assetCategory',
      'search',
      'page',
      'pageSize',
    ],
  ],
  [
    '/api/v1/inspections/registry',
    [
      'assetId',
      'scheduleId',
      'year',
      'quarter',
      'pmCycle',
      'department',
      'assetCategory',
      'search',
      'page',
      'pageSize',
    ],
  ],
]
for (const [path, expectedParameters] of registryFilterParameters) {
  const parameters = snapshot.paths?.[path]?.get?.parameters ?? []
  for (const expected of expectedParameters) {
    if (!parameters.some((parameter) => parameter.name === expected)) {
      throw new Error(
        `Registry operation ${path} is missing the ${expected} parameter.`,
      )
    }
  }
}

const registryResponseFields = [
  ['AssetRegistryPageResponse', ['items', 'page', 'pageSize', 'totalCount']],
  [
    'ScheduleRegistryPageResponse',
    ['items', 'page', 'pageSize', 'totalCount', 'overallCount'],
  ],
  [
    'InspectionRegistryPageResponse',
    ['items', 'page', 'pageSize', 'totalCount', 'overallCount'],
  ],
  ['ScheduleRegistryItemResponse', ['pmCycle', 'asset']],
  ['InspectionRegistryItemResponse', ['schedule', 'asset']],
  ['InspectionRegistryScheduleContext', ['id', 'pmCycle']],
  [
    'InspectionRegistryAssetContext',
    ['id', 'assetCode', 'assetCategory', 'department'],
  ],
]
for (const [schemaName, fields] of registryResponseFields) {
  const properties = snapshot.components?.schemas?.[schemaName]?.properties
  if (!properties || fields.some((field) => !properties[field])) {
    throw new Error(
      `${schemaName} is missing one or more web registry contract fields.`,
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
  'externalPmNumber',
  'wmsReferralRevision',
  'correctiveFollowUpStatus',
  'hasPhotoEvidence',
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

const referralRequestFields = ['externalPmNumber', 'expectedRevision']
const referralRequestProperties =
  snapshot.components?.schemas?.UpdateInspectionWmsReferralDto?.properties
if (
  !referralRequestProperties ||
  referralRequestFields.some((field) => !referralRequestProperties[field])
) {
  throw new Error(
    'UpdateInspectionWmsReferralDto is missing number or expectedRevision.',
  )
}

const referralResponseFields = [
  'inspectionId',
  'externalPmNumber',
  'revision',
  'recordedByUserId',
  'recordedAt',
  'lastUpdatedByUserId',
  'lastUpdatedAt',
  'followUpStatus',
]
const referralResponseProperties =
  snapshot.components?.schemas?.InspectionWmsReferralResponse?.properties
if (
  !referralResponseProperties ||
  referralResponseFields.some((field) => !referralResponseProperties[field])
) {
  throw new Error(
    'InspectionWmsReferralResponse is missing required audit fields.',
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

const handoffRowFields = [
  'wmsPmNumber',
  'wmsReferralRevision',
  'followUpStatus',
  'canRecordWmsReferral',
]
const handoffRowProperties =
  snapshot.components?.schemas?.CorrectiveMaintenanceHandoffRowResponse
    ?.properties
if (
  !handoffRowProperties ||
  handoffRowFields.some((field) => !handoffRowProperties[field])
) {
  throw new Error(
    'Corrective handoff rows are missing the WMS reference state.',
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

const pmDashboardAssetFields = ['remarks', 'actionsRecommendations']
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
  'OpenAPI auth, asset, schedule, inspection registry, WMS referral, preventive-maintenance form, and PM analytics V2/V3 contract sanity check passed.',
)
