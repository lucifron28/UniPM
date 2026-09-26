import { describe, expect, it } from 'vitest'
import {
  assetSchema,
  createAssetSchema,
  parseAssetVerificationLocation,
  parseAssetCategories,
  toCreateAssetDto,
  verificationLocationSchema,
} from '@/features/assets/asset-contract'

const asset = {
  id: '11111111-1111-4111-8111-111111111111',
  assetCode: 'FE-001',
  assetCategory: 'fire-extinguisher',
  building: 'Main Building',
  department: 'GSD',
  location: 'Ground floor',
  hasVerificationLocation: false,
  qrCodeValue: 'UNIPM-FIREEXTINGUISHER-11111111',
  status: 'Active',
  createdAt: '2026-07-19T00:00:00+00:00',
  updatedAt: '2026-07-19T00:00:00+00:00',
}

describe('asset API contracts', () => {
  it('accepts only the public asset response shape', () => {
    expect(assetSchema.parse(asset)).toMatchObject(asset)
    expect(() =>
      assetSchema.parse({
        ...asset,
        verificationLatitude: 14.6,
        verificationLongitude: 120.9,
        verificationRadiusMeters: 25,
      }),
    ).toThrow()
    expect(() =>
      assetSchema.parse({ ...asset, descriptionEmbedding: '[1,2,3]' }),
    ).toThrow()
  })

  it('parses exact verification coordinates only from the configuration contract', () => {
    expect(
      parseAssetVerificationLocation({
        verificationLatitude: 14.6,
        verificationLongitude: 120.9,
        verificationRadiusMeters: 25,
      }),
    ).toEqual({
      verificationLatitude: 14.6,
      verificationLongitude: 120.9,
      verificationRadiusMeters: 25,
    })
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
      verificationLatitude: null,
      verificationLongitude: null,
      verificationRadiusMeters: null,
    })
  })

  it('validates optional verification values as finite, bounded, and all-or-none', () => {
    expect(
      verificationLocationSchema.parse({
        verificationLatitude: '90',
        verificationLongitude: '-180',
        verificationRadiusMeters: '0.1',
      }),
    ).toEqual({
      verificationLatitude: 90,
      verificationLongitude: -180,
      verificationRadiusMeters: 0.1,
    })
    expect(
      verificationLocationSchema.safeParse({
        verificationLatitude: '14.6',
        verificationLongitude: '',
        verificationRadiusMeters: '',
      }).success,
    ).toBe(false)
    expect(
      verificationLocationSchema.safeParse({
        verificationLatitude: '91',
        verificationLongitude: '181',
        verificationRadiusMeters: '0',
      }).success,
    ).toBe(false)
    expect(
      verificationLocationSchema.safeParse({
        verificationLatitude: 'NaN',
        verificationLongitude: '0',
        verificationRadiusMeters: 'Infinity',
      }).success,
    ).toBe(false)
  })

  it('converts configured verification values to numbers for the create DTO', () => {
    expect(
      toCreateAssetDto({
        assetCode: 'FE-002',
        assetCategory: 'fire-extinguisher',
        verificationLatitude: '14.5995',
        verificationLongitude: '120.9842',
        verificationRadiusMeters: '25',
      }),
    ).toMatchObject({
      verificationLatitude: 14.5995,
      verificationLongitude: 120.9842,
      verificationRadiusMeters: 25,
    })
    expect(
      createAssetSchema.safeParse({
        assetCode: 'FE-003',
        assetCategory: 'fire-extinguisher',
        verificationLatitude: '14.6',
      }).success,
    ).toBe(false)
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
