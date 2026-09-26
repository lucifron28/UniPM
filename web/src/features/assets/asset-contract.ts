import { z } from 'zod'
import type {
  AssetCategoryResponse,
  AssetResponse,
  CreateAssetDto,
} from '@/api/generated/models'

export const assetCategoryCodes = [
  'fire-extinguisher',
  'fire-alarm',
  'emergency-light',
  'water-drinking-station',
] as const

export const assetStatusCodes = ['Active', 'Inactive', 'Retired'] as const

const optionalText = z.string().max(256).nullable()

export const assetSchema = z
  .object({
    id: z.string().uuid(),
    assetCode: z.string().trim().min(1).max(64),
    assetCategory: z.enum(assetCategoryCodes),
    building: optionalText,
    department: optionalText,
    location: optionalText,
    verificationLatitude: z.number().finite().min(-90).max(90).nullable(),
    verificationLongitude: z.number().finite().min(-180).max(180).nullable(),
    verificationRadiusMeters: z.number().finite().positive().nullable(),
    qrCodeValue: z.string().trim().min(1).max(128).nullable(),
    status: z.enum(assetStatusCodes),
    createdAt: z.string().datetime({ offset: true }),
    updatedAt: z.string().datetime({ offset: true }),
  })
  .strict()

export type Asset = z.infer<typeof assetSchema>

const assetCategorySchema = z
  .object({
    code: z.enum(assetCategoryCodes),
    displayName: z.string().trim().min(1).max(128),
  })
  .strict()

export type AssetCategory = z.infer<typeof assetCategorySchema>

const optionalFiniteNumber = z
  .string()
  .trim()
  .transform((value, context) => {
    if (!value) return null
    const parsed = Number(value)
    if (!Number.isFinite(parsed)) {
      context.addIssue({ code: 'custom', message: 'Enter a finite number.' })
      return z.NEVER
    }
    return parsed
  })
  .optional()

export const verificationLocationFieldSchemas = {
  verificationLatitude: optionalFiniteNumber.refine(
    (value) => value == null || (value >= -90 && value <= 90),
    'Latitude must be between -90 and 90.',
  ),
  verificationLongitude: optionalFiniteNumber.refine(
    (value) => value == null || (value >= -180 && value <= 180),
    'Longitude must be between -180 and 180.',
  ),
  verificationRadiusMeters: optionalFiniteNumber.refine(
    (value) => value == null || value > 0,
    'Radius must be greater than zero.',
  ),
}

type VerificationLocationValues = {
  verificationLatitude?: number | null | undefined
  verificationLongitude?: number | null | undefined
  verificationRadiusMeters?: number | null | undefined
}

function validateVerificationLocation(
  values: VerificationLocationValues,
  context: z.RefinementCtx,
) {
  const keys = [
    'verificationLatitude',
    'verificationLongitude',
    'verificationRadiusMeters',
  ] as const
  const supplied = keys.filter((key) => values[key] != null)
  if (supplied.length === 0 || supplied.length === keys.length) return

  keys
    .filter((key) => values[key] == null)
    .forEach((key) => {
      context.addIssue({
        code: 'custom',
        path: [key],
        message: 'Enter latitude, longitude, and radius together.',
      })
    })
}

export const verificationLocationSchema = z
  .object(verificationLocationFieldSchemas)
  .superRefine(validateVerificationLocation)

export const createAssetFieldSchemas = {
  assetCode: z
    .string()
    .trim()
    .min(1, 'Asset code is required.')
    .max(64, 'Asset code must not exceed 64 characters.')
    .transform((value) => value.toUpperCase()),
  assetCategory: z.enum(assetCategoryCodes, {
    message: 'Choose an asset category.',
  }),
  building: z
    .string()
    .trim()
    .max(256, 'Building must not exceed 256 characters.')
    .optional(),
  department: z
    .string()
    .trim()
    .max(256, 'Department must not exceed 256 characters.')
    .optional(),
  location: z
    .string()
    .trim()
    .max(256, 'Location must not exceed 256 characters.')
    .optional(),
  ...verificationLocationFieldSchemas,
}

export const createAssetSchema = z
  .object(createAssetFieldSchemas)
  .superRefine(validateVerificationLocation)

export type CreateAssetValues = z.input<typeof createAssetSchema>

export function parseAsset(value: AssetResponse): Asset {
  return assetSchema.parse(value)
}

export function parseAssets(values: AssetResponse[]): Asset[] {
  return z.array(assetSchema).parse(values)
}

export function parseAssetCategories(
  values: AssetCategoryResponse[],
): AssetCategory[] {
  return z.array(assetCategorySchema).parse(values)
}

export function toCreateAssetDto(values: CreateAssetValues): CreateAssetDto {
  const parsed = createAssetSchema.parse(values)
  const emptyToNull = (value: string | undefined) => value || null
  return {
    assetCode: parsed.assetCode,
    assetCategory: parsed.assetCategory,
    building: emptyToNull(parsed.building),
    department: emptyToNull(parsed.department),
    location: emptyToNull(parsed.location),
    verificationLatitude: parsed.verificationLatitude ?? null,
    verificationLongitude: parsed.verificationLongitude ?? null,
    verificationRadiusMeters: parsed.verificationRadiusMeters ?? null,
  }
}
