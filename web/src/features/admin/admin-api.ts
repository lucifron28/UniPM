import { httpClient } from '@/api/http-client'

export type Page<T> = {
  totalCount: number
  page: number
  pageSize: number
  items: T[]
}

export type Account = {
  id: string
  displayName: string
  email: string
  role: string
  isActive: boolean
}

export type GsdHead = {
  activeHead: Account | null
  accounts: Account[]
  needsResolution: boolean
}

export type Overview = {
  activeAccounts: number
  inactiveAccounts: number
  assets: number
  schedules: number
  inspections: number
  draftForms: number
  submittedForms: number
  acknowledgedForms: number
  acknowledgements: number
}

export type Asset = {
  id: string
  assetCode: string
  assetCategory: string
  building: string | null
  department: string | null
  location: string | null
  qrCodeValue: string | null
  status: string
  createdAt: string
  updatedAt: string
}

export type Schedule = {
  id: string
  assetId: string
  assetCode: string
  assetCategory: string
  department: string | null
  scheduleDate: string
  pmCycle: string
  periodType: string
  status: string
  quarter: string | null
  semester: string | null
  year: number | null
  academicYear: string | null
  inspectorUserId: string | null
  inspectorDisplayName: string | null
  supervisorUserId: string | null
  supervisorDisplayName: string | null
  completedAt: string | null
}

export type Inspection = {
  id: string
  scheduleId: string
  assetId: string
  assetCode: string
  dateInspected: string
  inspectorUserId: string
  inspectorDisplayName: string | null
  isOperational: boolean
  formId: string | null
  formStatus: string | null
  formFileNumber: string | null
}

export type DraftInspection = {
  id: string
  scheduleId: string
  inspectorUserId: string
  dateInspected: string
  isOperational: boolean
  remarks: string | null
  actionsRecommendations: string | null
  updatedAt: string
  assetCode: string | null
}

export type PreventiveMaintenanceForm = {
  id: string
  assetCategory: string
  building: string | null
  department: string | null
  pmCycle: string | null
  periodType: string
  quarter: string | null
  semester: string | null
  year: number | null
  academicYear: string | null
  status: string
  inspections: DraftInspection[]
}

export type MaintenanceForm = {
  id: string
  fileNumber: string | null
  assetCategory: string
  building: string | null
  department: string | null
  pmCycle: string | null
  periodType: string
  quarter: string | null
  semester: string | null
  year: number | null
  academicYear: string | null
  status: string
  inspectionCount: number
  hasAcknowledgement: boolean
  updatedAt: string
}

export type DeleteResult = {
  assetsRemoved: number
  schedulesRemoved: number
  inspectionsRemoved: number
  formsRemoved: number
  acknowledgementsRemoved: number
  photoFilesRemoved: number
  photoFileCleanupFailures: number
}

export type ValidationDatasetResult = {
  deleted: DeleteResult
  assetsSeeded: number
  schedulesSeeded: number
  inspectionsSeeded: number
  formsSeeded: number
  acknowledgementsSeeded: number
  wmsReferralsSeeded: number
  wmsReferralAuditsSeeded: number
}

async function get<T>(url: string, params?: Record<string, unknown>) {
  return (await httpClient.get<T>(url, { params })).data
}

async function post<T>(url: string, data?: unknown) {
  return (await httpClient.post<T>(url, data)).data
}

async function put<T>(url: string, data: unknown) {
  return (await httpClient.put<T>(url, data)).data
}

async function patch<T>(url: string, data: unknown) {
  return (await httpClient.patch<T>(url, data)).data
}

async function remove<T>(url: string) {
  return (await httpClient.delete<T>(url)).data
}

const root = '/api/v1/admin'
const accountRoot = `${root}/accounts`

export const adminApi = {
  overview: () => get<Overview>(`${root}/overview`),
  accounts: (params: Record<string, unknown>) =>
    get<Page<Account>>(`${accountRoot}`, params),
  createAccount: (body: {
    displayName: string
    email: string
    role: string
    initialPassword: string
    isActive: boolean
  }) => post<Account>(accountRoot, body),
  updateAccount: (id: string, body: Omit<Account, 'id'>) =>
    put<Account>(`${accountRoot}/${id}`, body),
  resetPassword: (id: string, newPassword: string) =>
    post<void>(`${accountRoot}/${id}/reset-password`, { newPassword }),
  deleteAccount: (id: string) =>
    remove<{
      accessRemoved: boolean
      retainedForHistory: boolean
      currentAssignmentsCleared: number
    }>(`${accountRoot}/${id}`),
  gsdHead: () => get<GsdHead>(`${accountRoot}/gsd-head`),
  updateGsdHead: (body: { displayName: string; email: string }) =>
    patch<Account>(`${accountRoot}/gsd-head`, body),
  replaceGsdHead: (body: {
    replacementUserId?: string
    displayName?: string
    email?: string
    initialPassword?: string
  }) => put<GsdHead>(`${accountRoot}/gsd-head`, body),
  assets: (params: Record<string, unknown>) =>
    get<Page<Asset>>(`${root}/assets`, params),
  createAsset: (body: {
    assetCode: string
    assetCategory: string
    building?: string
    department?: string
    location?: string
    verificationLatitude?: number
    verificationLongitude?: number
    verificationRadiusMeters?: number
  }) => post<Asset>(`${root}/assets`, body),
  updateAsset: (
    id: string,
    body: {
      assetCode: string
      assetCategory: string
      status: string
      building?: string
      department?: string
      location?: string
      verificationLatitude?: number
      verificationLongitude?: number
      verificationRadiusMeters?: number
    },
  ) => put<Asset>(`${root}/assets/${id}`, body),
  deleteAsset: (id: string) => remove<DeleteResult>(`${root}/assets/${id}`),
  deleteAssets: (ids: string[]) =>
    post<DeleteResult>(`${root}/assets/bulk-delete`, { ids }),
  clearAssets: () => remove<DeleteResult>(`${root}/assets/all`),
  schedules: (params: Record<string, unknown>) =>
    get<Page<Schedule>>(`${root}/maintenance/schedules`, params),
  updateSchedule: (
    id: string,
    body: {
      assetId: string
      scheduleDate: string
      periodType: string
      quarter: string | null
      semester: string | null
      academicYear: string | null
      status: string
      assignedToUserId: string | null
      assignedSupervisorUserId: string | null
    },
  ) => put<Schedule>(`${root}/maintenance/schedules/${id}`, body),
  deleteSchedule: (id: string) =>
    remove<DeleteResult>(`${root}/maintenance/schedules/${id}`),
  deleteSchedules: (ids: string[]) =>
    post<DeleteResult>(`${root}/maintenance/schedules/bulk-delete`, { ids }),
  inspections: (params: Record<string, unknown>) =>
    get<Page<Inspection>>(`${root}/maintenance/inspections`, params),
  deleteInspection: (id: string) =>
    remove<DeleteResult>(`${root}/maintenance/inspections/${id}`),
  deleteInspections: (ids: string[]) =>
    post<DeleteResult>(`${root}/maintenance/inspections/bulk-delete`, { ids }),
  forms: (params: Record<string, unknown>) =>
    get<Page<MaintenanceForm>>(`${root}/maintenance/forms`, params),
  getForm: (id: string) =>
    get<PreventiveMaintenanceForm>(
      `/api/v1/preventive-maintenance-forms/${id}`,
    ),
  createForm: (body: {
    assetCategory: string
    building?: string | null
    department?: string | null
    periodType: string
    quarter?: string | null
    semester?: string | null
    year?: number | null
    academicYear?: string | null
  }) =>
    post<PreventiveMaintenanceForm>(
      '/api/v1/preventive-maintenance-forms/',
      body,
    ),
  addInspection: (
    formId: string,
    body: {
      scheduleId: string
      inspectorUserId: string
      dateInspected: string
      isOperational: boolean
      remarks?: string | null
      actionsRecommendations?: string | null
    },
  ) =>
    post<DraftInspection>(
      `/api/v1/preventive-maintenance-forms/${formId}/inspections`,
      body,
    ),
  updateInspection: (
    formId: string,
    inspectionId: string,
    body: {
      expectedUpdatedAt?: string
      dateInspected: string
      isOperational: boolean
      remarks?: string | null
      actionsRecommendations?: string | null
    },
  ) =>
    put<DraftInspection>(
      `/api/v1/preventive-maintenance-forms/${formId}/inspections/${inspectionId}`,
      body,
    ),
  updateForm: (
    id: string,
    body: {
      fileNumber: string | null
      assetCategory: string
      building: string | null
      department: string | null
      pmCycle: string | null
      periodType: string
      quarter: string | null
      semester: string | null
      year: number | null
      academicYear: string | null
    },
  ) => put<MaintenanceForm>(`${root}/maintenance/forms/${id}`, body),
  deleteForm: (id: string) =>
    remove<DeleteResult>(`${root}/maintenance/forms/${id}`),
  deleteAcknowledgement: (id: string) =>
    remove<DeleteResult>(`${root}/maintenance/forms/${id}/acknowledgement`),
  returnFormToDraft: (id: string) =>
    post<void>(`${root}/maintenance/forms/${id}/return-to-draft`),
  clearMaintenance: () => remove<DeleteResult>(`${root}/demo-data/maintenance`),
  clearAssetsAndMaintenance: () =>
    remove<DeleteResult>(`${root}/demo-data/assets`),
  loadValidationDataset: () =>
    post<ValidationDatasetResult>(`${root}/demo-data/validation/load`),
  resetValidationDataset: () =>
    post<ValidationDatasetResult>(`${root}/demo-data/validation/reset`),
}
