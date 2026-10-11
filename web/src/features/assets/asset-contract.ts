import { z } from 'zod'
import type {
  AssetRegistryOptionsResponse,
  AssetRegistryPageResponse,
  AssetRegistrySummaryResponse,
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
    hasVerificationLocation: z.boolean(),
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
    scheduledMonths: z.preprocess(
      (value) => {
        if (value == null) return []
        if (!Array.isArray(value)) return value
        return value.map((month) =>
          typeof month === 'string' ? Number(month) : month,
        )
      },
      z.array(z.number().int().min(1).max(12)),
    ),
  })
  .strict()

export type AssetCategory = z.infer<typeof assetCategorySchema>

const assetRegistryPageSchema = z
  .object({
    items: z.array(assetSchema),
    page: z.coerce.number().int().positive(),
    pageSize: z.coerce.number().int().positive(),
    totalCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

const assetRegistryOptionsSchema = z
  .object({
    categories: z.array(assetCategorySchema),
    statuses: z.array(z.enum(assetStatusCodes)),
    departments: z.array(z.string().trim().min(1).max(256)),
    buildings: z.array(z.string().trim().min(1).max(256)),
  })
  .strict()

const assetRegistryCategorySummarySchema = z
  .object({
    assetCategory: z.enum(assetCategoryCodes),
    displayName: z.string().trim().min(1).max(128),
    totalCount: z.coerce.number().int().nonnegative(),
    activeCount: z.coerce.number().int().nonnegative(),
    inactiveCount: z.coerce.number().int().nonnegative(),
    retiredCount: z.coerce.number().int().nonnegative(),
  })
  .strict()

const assetRegistrySummarySchema = z
  .object({
    totalCount: z.coerce.number().int().nonnegative(),
    activeCount: z.coerce.number().int().nonnegative(),
    inactiveCount: z.coerce.number().int().nonnegative(),
    retiredCount: z.coerce.number().int().nonnegative(),
    categories: z.array(assetRegistryCategorySummarySchema),
  })
  .strict()

export type AssetRegistryPage = z.infer<typeof assetRegistryPageSchema>
export type AssetRegistryOptions = z.infer<typeof assetRegistryOptionsSchema>
export type AssetRegistryCategorySummary = z.infer<
  typeof assetRegistryCategorySummarySchema
>
export type AssetRegistrySummary = z.infer<typeof assetRegistrySummarySchema>

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
    .min(1, 'Department is required for preventive maintenance assets.')
    .max(256, 'Department must not exceed 256 characters.'),
  location: z
    .string()
    .trim()
    .max(256, 'Location must not exceed 256 characters.')
    .optional(),
}

export const createAssetSchema = z.object(createAssetFieldSchemas)

export type CreateAssetValues = z.input<typeof createAssetSchema>

export function parseAsset(value: AssetResponse): Asset {
  return assetSchema.parse(value)
}

export function parseAssets(values: AssetResponse[]): Asset[] {
  return z.array(assetSchema).parse(values)
}

export function parseAssetRegistryPage(
  value: AssetRegistryPageResponse,
): AssetRegistryPage {
  return assetRegistryPageSchema.parse(value)
}

export function parseAssetRegistryOptions(
  value: AssetRegistryOptionsResponse,
): AssetRegistryOptions {
  return assetRegistryOptionsSchema.parse(value)
}

export function parseAssetRegistrySummary(
  value: AssetRegistrySummaryResponse,
): AssetRegistrySummary {
  return assetRegistrySummarySchema.parse(value)
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
