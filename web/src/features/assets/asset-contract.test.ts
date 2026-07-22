import { describe, expect, it } from 'vitest'
import {
  assetSchema,
  parseAssetCategories,
  toCreateAssetDto,
} from '@/features/assets/asset-contract'

const asset = {
  id: '11111111-1111-4111-8111-111111111111',
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Ground floor',
  qrCodeValue: 'UNIPM-FIREEXTINGUISHER-11111111',
  status: 'Active',
  createdAt: '2026-07-19T00:00:00+00:00',
  updatedAt: '2026-07-19T00:00:00+00:00',
}

describe('asset API contracts', () => {
  it('accepts only the public asset response shape', () => {
    expect(assetSchema.parse(asset)).toMatchObject(asset)
    expect(() =>
      assetSchema.parse({ ...asset, descriptionEmbedding: '[1,2,3]' }),
    ).toThrow()
  })

  it('rejects categories outside the current study scope', () => {
    expect(() =>
      assetSchema.parse({ ...asset, assetCategory: 'generator' }),
    ).toThrow()
  })

  it('normalizes create values without inventing optional metadata', () => {
    expect(
      toCreateAssetDto({
        assetCode: ' fe-001 ',
        assetCategory: 'fire-extinguisher',
        building: '',
        department: ' GSD ',
        location: '',
      }),
    ).toEqual({
      assetCode: 'FE-001',
      assetCategory: 'fire-extinguisher',
      building: null,
      department: 'GSD',
      location: null,
    })
  })

  it('uses reference-data labels instead of a client-side category label list', () => {
    expect(
      parseAssetCategories([
        { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
      ]),
    ).toEqual([
      { code: 'fire-extinguisher', displayName: 'Fire extinguishers' },
    ])
  })
})
