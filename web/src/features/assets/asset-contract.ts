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

export const createAssetSchema = z.object({
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
})

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
  }
}
