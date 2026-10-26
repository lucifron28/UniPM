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

const adminOperations = [
  [
    '/api/v1/admin/accounts',
    'get',
    'ListAdminManagedAccounts',
    '200',
    'AdminAccountPageResponse',
  ],
  [
    '/api/v1/admin/accounts',
    'post',
    'CreateAdminManagedAccount',
    '201',
    'AdminAccountResponse',
  ],
  [
    '/api/v1/admin/accounts/{id}',
    'put',
    'UpdateAdminManagedAccount',
    '200',
    'AdminAccountResponse',
  ],
  [
    '/api/v1/admin/accounts/{id}',
    'delete',
    'DeleteAdminManagedAccount',
    '200',
    'AdminAccountDeleteResponse',
  ],
  [
    '/api/v1/admin/accounts/{id}/reset-password',
    'post',
    'ResetAdminManagedAccountPassword',
    '204',
    null,
  ],
  [
    '/api/v1/admin/accounts/gsd-head',
    'get',
    'GetAdminGsdHead',
    '200',
    'AdminGsdHeadResponse',
  ],
  [
    '/api/v1/admin/accounts/gsd-head',
    'patch',
    'UpdateAdminGsdHead',
    '200',
    'AdminAccountResponse',
  ],
  [
    '/api/v1/admin/accounts/gsd-head',
    'put',
    'ReplaceAdminGsdHead',
    '200',
    'AdminGsdHeadResponse',
  ],
  [
    '/api/v1/admin/overview',
    'get',
    'GetAdminOverview',
    '200',
    'AdminOverviewResponse',
  ],
  [
    '/api/v1/admin/assets',
    'get',
    'ListAdminAssets',
    '200',
    'AdminAssetPageResponse',
  ],
  ['/api/v1/admin/assets', 'post', 'CreateAdminAsset', '201', 'AdminAssetRow'],
  [
    '/api/v1/admin/assets/{id}',
    'put',
    'UpdateAdminAsset',
    '200',
    'AdminAssetRow',
  ],
  [
    '/api/v1/admin/assets/{id}',
    'delete',
    'DeleteAdminAsset',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/assets/bulk-delete',
    'post',
    'BulkDeleteAdminAssets',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/assets/all',
    'delete',
    'ClearAdminAssets',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/schedules',
    'get',
    'ListAdminSchedules',
    '200',
    'AdminSchedulePageResponse',
  ],
  [
    '/api/v1/admin/maintenance/schedules/{id}',
    'put',
    'UpdateAdminSchedule',
    '200',
    'ScheduleResponse',
  ],
  [
    '/api/v1/admin/maintenance/schedules/{id}',
    'delete',
    'DeleteAdminSchedule',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/schedules/bulk-delete',
    'post',
    'BulkDeleteAdminSchedules',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/inspections',
    'get',
    'ListAdminInspections',
    '200',
    'AdminInspectionPageResponse',
  ],
  [
    '/api/v1/admin/maintenance/inspections/{id}',
    'delete',
    'DeleteAdminInspection',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/inspections/bulk-delete',
    'post',
    'BulkDeleteAdminInspections',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/forms',
    'get',
    'ListAdminForms',
    '200',
    'AdminFormPageResponse',
  ],
  [
    '/api/v1/admin/maintenance/forms/{id}',
    'put',
    'UpdateAdminForm',
    '200',
    'AdminFormRow',
  ],
  [
    '/api/v1/admin/maintenance/forms/{id}',
    'delete',
    'DeleteAdminForm',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/forms/{id}/acknowledgement',
    'delete',
    'DeleteAdminAcknowledgement',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/maintenance/forms/{id}/return-to-draft',
    'post',
    'ReturnAdminFormToDraft',
    '204',
    null,
  ],
  [
    '/api/v1/admin/demo-data/maintenance',
    'delete',
    'ClearAdminMaintenanceData',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/demo-data/assets',
    'delete',
    'ClearAdminAssetsAndMaintenance',
    '200',
    'AdminDeleteResult',
  ],
  [
    '/api/v1/admin/demo-data/validation/load',
    'post',
    'LoadAdminValidationDataset',
    '200',
    'AdminValidationDatasetResponse',
  ],
  [
    '/api/v1/admin/demo-data/validation/reset',
    'post',
    'ResetAndSeedAdminValidationDataset',
    '200',
    'AdminValidationDatasetResponse',
  ],
]

const adminRequestSchemas = [
  ['/api/v1/admin/accounts', 'post', 'AdminCreateAccountRequest'],
  ['/api/v1/admin/accounts/{id}', 'put', 'AdminUpdateAccountRequest'],
  [
    '/api/v1/admin/accounts/{id}/reset-password',
    'post',
    'AdminResetPasswordRequest',
  ],
  ['/api/v1/admin/assets', 'post', 'CreateAssetDto'],
  ['/api/v1/admin/assets/{id}', 'put', 'AdminUpdateAssetRequest'],
  ['/api/v1/admin/assets/bulk-delete', 'post', 'AdminBulkDeleteRequest'],
  [
    '/api/v1/admin/maintenance/schedules/{id}',
    'put',
    'AdminUpdateScheduleRequest',
  ],
  ['/api/v1/admin/maintenance/forms/{id}', 'put', 'AdminUpdateFormRequest'],
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
    '/api/v1/preventive-maintenance-forms/options',
    'get',
    'GetPreventiveMaintenanceFormOptions',
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
  responseSchemaName,
] of adminOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  if (operation?.operationId !== operationId) {
    throw new Error(`Missing required Admin operation: ${operationId}.`)
  }
  const response = operation.responses?.[status]
  if (!response) {
    throw new Error(
      `Required Admin operation ${operationId} is missing its ${status} response.`,
    )
  }
  if (responseSchemaName) {
    const schema = response.content?.['application/json']?.schema
    if (schema?.$ref !== `#/components/schemas/${responseSchemaName}`) {
      throw new Error(
        `Admin operation ${operationId} must return ${responseSchemaName}.`,
      )
    }
  }
}

for (const [path, method, schemaName] of adminRequestSchemas) {
  const schema =
    snapshot.paths?.[path]?.[method]?.requestBody?.content?.['application/json']
      ?.schema
  if (schema?.$ref !== `#/components/schemas/${schemaName}`) {
    throw new Error(
      `Admin operation ${method.toUpperCase()} ${path} must use ${schemaName}.`,
    )
  }
}

const adminScheduleParameters =
  snapshot.paths?.['/api/v1/admin/maintenance/schedules']?.get?.parameters ?? []
if (!adminScheduleParameters.some((parameter) => parameter.name === 'status')) {
  throw new Error('ListAdminSchedules is missing its status filter.')
}

const adminAccountProperties =
  snapshot.components?.schemas?.AdminAccountResponse?.properties
if (
  !adminAccountProperties ||
  adminAccountProperties.password ||
  adminAccountProperties.passwordHash
) {
  throw new Error(
    'Admin account responses must not expose passwords or password hashes.',
  )
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

const savedReportsPath = '/api/v1/analytics/pm/v3/saved-reports'
const savedReportItemPath = `${savedReportsPath}/{id}`
const savedReportPreviewPath = `${savedReportItemPath}/preview`
const savedReportOperations = [
  [savedReportsPath, 'get', 'ListSavedAnalyticsReports', '200'],
  [
    savedReportsPath,
    'post',
    'CreateSavedAnalyticsReport',
    '201',
    'SavedAnalyticsReportRequest',
    'SavedAnalyticsReportResponse',
  ],
  [
    savedReportItemPath,
    'put',
    'UpdateSavedAnalyticsReport',
    '200',
    'SavedAnalyticsReportRequest',
    'SavedAnalyticsReportResponse',
  ],
  [savedReportItemPath, 'delete', 'DeleteSavedAnalyticsReport', '204'],
  [
    savedReportPreviewPath,
    'post',
    'PreviewSavedAnalyticsReport',
    '200',
    null,
    'PmAnalyticsV3PreviewResponse',
  ],
]

for (const [
  path,
  method,
  operationId,
  status,
  requestSchemaName,
  responseSchemaName,
] of savedReportOperations) {
  const operation = snapshot.paths?.[path]?.[method]
  const requestSchema = requestSchemaName
    ? operation?.requestBody?.content?.['application/json']?.schema
    : null
  const response = operation?.responses?.[status]
  const responseSchema =
    response?.content?.['application/json']?.schema
  const idParameter = operation?.parameters?.find(
    (parameter) => parameter.name === 'id' && parameter.in === 'path',
  )

  if (
    operation?.operationId !== operationId ||
    !response ||
    (path.includes('{id}') &&
      (!idParameter?.required ||
        idParameter.schema?.type !== 'string' ||
        idParameter.schema?.format !== 'uuid')) ||
    (requestSchemaName &&
      (operation?.requestBody?.required !== true ||
        requestSchema?.$ref !==
          `#/components/schemas/${requestSchemaName}`)) ||
    (responseSchemaName &&
      responseSchema?.$ref !== `#/components/schemas/${responseSchemaName}`)
  ) {
    throw new Error(
      `Saved-report operation ${operationId} has an unexpected request or success response.`,
    )
  }
}

const savedReports =
  snapshot.paths?.[savedReportsPath]?.get?.responses?.['200']?.content?.[
    'application/json'
  ]?.schema
if (
  savedReports?.type !== 'array' ||
  savedReports.items?.$ref !==
    '#/components/schemas/SavedAnalyticsReportResponse'
) {
  throw new Error(
    'ListSavedAnalyticsReports must return SavedAnalyticsReportResponse items.',
  )
}

const deleteSavedReport = snapshot.paths?.[savedReportItemPath]?.delete
const deleteRevision = deleteSavedReport?.parameters?.find(
  (parameter) => parameter.name === 'revision' && parameter.in === 'query',
)
if (
  !deleteRevision?.required ||
  deleteRevision.schema?.type !== 'string' ||
  deleteRevision.schema?.format !== 'uuid'
) {
  throw new Error(
    'DeleteSavedAnalyticsReport must require a UUID revision query parameter.',
  )
}

const savedRequest =
  snapshot.components?.schemas?.SavedAnalyticsReportRequest
const savedRequestRequired = new Set(savedRequest?.required ?? [])
if (
  !savedRequest?.properties?.name ||
  !savedRequest?.properties?.plan ||
  !['name', 'plan'].every((field) => savedRequestRequired.has(field))
) {
  throw new Error(
    'SavedAnalyticsReportRequest must require its name and validated plan.',
  )
}

const savedResponse =
  snapshot.components?.schemas?.SavedAnalyticsReportResponse
const savedResponseRequired = new Set(savedResponse?.required ?? [])
if (
  !['id', 'name', 'plan', 'periodMode', 'revision', 'updatedAtUtc'].every(
    (field) =>
      field in (savedResponse?.properties ?? {}) &&
      savedResponseRequired.has(field),
  )
) {
  throw new Error(
    'SavedAnalyticsReportResponse is missing required saved-report fields.',
  )
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

const preventiveMaintenanceFormList =
  snapshot.paths?.['/api/v1/preventive-maintenance-forms']?.get
const preventiveMaintenanceFormListSchema =
  preventiveMaintenanceFormList?.responses?.['200']?.content?.[
    'application/json'
  ]?.schema
if (
  preventiveMaintenanceFormListSchema?.$ref !==
  '#/components/schemas/PreventiveMaintenanceFormPageResponse'
) {
  throw new Error(
    'ListPreventiveMaintenanceForms must return PreventiveMaintenanceFormPageResponse.',
  )
}

for (const name of ['page', 'pageSize', 'supervisor']) {
  if (
    !preventiveMaintenanceFormList?.parameters?.some(
      (parameter) => parameter.name === name,
    )
  ) {
    throw new Error(
      `ListPreventiveMaintenanceForms is missing its ${name} parameter.`,
    )
  }
}

const formPageProperties =
  snapshot.components?.schemas?.PreventiveMaintenanceFormPageResponse
    ?.properties
const formPageRequired = new Set(
  snapshot.components?.schemas?.PreventiveMaintenanceFormPageResponse
    ?.required ?? [],
)
if (
  !formPageProperties?.items ||
  formPageProperties.items.type !== 'array' ||
  formPageProperties.items.items?.$ref !==
    '#/components/schemas/PreventiveMaintenanceFormResponse' ||
  !formPageProperties.page ||
  !formPageProperties.pageSize ||
  !formPageProperties.totalCount ||
  ['items', 'page', 'pageSize', 'totalCount'].some(
    (field) => !formPageRequired.has(field),
  )
) {
  throw new Error(
    'PreventiveMaintenanceFormPageResponse is missing its item or pagination fields.',
  )
}

const formOptionsOperation =
  snapshot.paths?.['/api/v1/preventive-maintenance-forms/options']?.get
const formOptionsSchema =
  formOptionsOperation?.responses?.['200']?.content?.['application/json']
    ?.schema
if (
  formOptionsSchema?.$ref !==
  '#/components/schemas/PreventiveMaintenanceFormOptionsResponse'
) {
  throw new Error(
    'GetPreventiveMaintenanceFormOptions must return PreventiveMaintenanceFormOptionsResponse.',
  )
}

const formOptionsProperties =
  snapshot.components?.schemas?.PreventiveMaintenanceFormOptionsResponse
    ?.properties
const formOptionsRequired = new Set(
  snapshot.components?.schemas?.PreventiveMaintenanceFormOptionsResponse
    ?.required ?? [],
)
if (
  !formOptionsProperties?.categories ||
  formOptionsProperties.categories.type !== 'array' ||
  formOptionsProperties.categories.items?.type !== 'string' ||
  !formOptionsProperties.departments ||
  formOptionsProperties.departments.type !== 'array' ||
  formOptionsProperties.departments.items?.type !== 'string' ||
  !formOptionsProperties.pmCycles ||
  formOptionsProperties.pmCycles.type !== 'array' ||
  formOptionsProperties.pmCycles.items?.type !== 'string' ||
  !formOptionsProperties.totalCount ||
  ['categories', 'departments', 'pmCycles', 'totalCount'].some(
    (field) => !formOptionsRequired.has(field),
  )
) {
  throw new Error(
    'PreventiveMaintenanceFormOptionsResponse is missing its filter values or totalCount.',
  )
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
  'OpenAPI auth, Admin, asset, schedule, inspection registry, WMS referral, preventive-maintenance form, PM analytics V2/V3, and saved-report contract checks passed.',
)
