import { useEffect, useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Link } from '@tanstack/react-router'
import { ZodError } from 'zod'
import {
  getAssetVerificationLocation,
  getGetAssetQueryKey,
  getGetAssetVerificationLocationQueryKey,
  updateAssetVerificationLocation,
} from '@/api/generated/endpoints'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Skeleton } from '@/components/ui/skeleton'
import { useAssetCategories, useAsset } from '@/features/assets/asset-queries'
import {
  parseAssetVerificationLocation,
  verificationLocationSchema,
  type Asset,
} from '@/features/assets/asset-contract'
import { categoryLabel, formatDate } from '@/features/assets/asset-presentation'
import { AssetQrLabel } from '@/features/assets/asset-qr-label'
import type { AssetSearch } from '@/features/assets/asset-registry'
import { useCurrentUser } from '@/features/auth/current-user'
import { InspectionHistory } from '@/features/inspections/inspection-history'
import { toast } from 'sonner'

function DetailItem({ label, value }: { label: string; value: string | null }) {
  return (
    <div>
      <dt className="text-xs font-semibold tracking-[0.08em] text-[var(--text-neutral)] uppercase">
        {label}
      </dt>
      <dd className="mt-1 text-sm break-words text-[var(--text-primary)]">
        {value || 'Not recorded'}
      </dd>
    </div>
  )
}

function AssetVerificationLocationEditor({ asset }: { asset: Asset }) {
  const locationKey = getGetAssetVerificationLocationQueryKey(asset.id)
  const location = useQuery({
    queryKey: locationKey,
    queryFn: ({ signal }) =>
      getAssetVerificationLocation(asset.id, signal).then(
        parseAssetVerificationLocation,
      ),
  })

  if (location.isPending) {
    return (
      <Card role="status" className="p-6 shadow-none">
        Loading inspection verification settings...
      </Card>
    )
  }

  if (location.isError || !location.data) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <p className="text-sm text-[var(--error)]">
          Inspection verification settings could not be loaded.
        </p>
        <Button
          type="button"
          className="mt-3"
          onClick={() => void location.refetch()}
        >
          Retry
        </Button>
      </Card>
    )
  }

  return (
    <AssetVerificationLocationForm
      asset={asset}
      initialLocation={location.data}
    />
  )
}

function AssetVerificationLocationForm({
  asset,
  initialLocation,
}: {
  asset: Asset
  initialLocation: ReturnType<typeof parseAssetVerificationLocation>
}) {
  const queryClient = useQueryClient()
  const locationKey = getGetAssetVerificationLocationQueryKey(asset.id)
  const [latitude, setLatitude] = useState(
    () => initialLocation.verificationLatitude?.toString() ?? '',
  )
  const [longitude, setLongitude] = useState(
    () => initialLocation.verificationLongitude?.toString() ?? '',
  )
  const [radius, setRadius] = useState(
    () => initialLocation.verificationRadiusMeters?.toString() ?? '',
  )
  const [error, setError] = useState<string | null>(null)

  const mutation = useMutation({
    mutationFn: async (values: {
      verificationLatitude: number | null
      verificationLongitude: number | null
      verificationRadiusMeters: number | null
    }) =>
      updateAssetVerificationLocation(asset.id, values).then(
        parseAssetVerificationLocation,
      ),
    onSuccess: (updatedLocation) => {
      queryClient.setQueryData(locationKey, updatedLocation)
      void queryClient.invalidateQueries({
        queryKey: getGetAssetQueryKey(asset.id),
      })
      setLatitude(updatedLocation.verificationLatitude?.toString() ?? '')
      setLongitude(updatedLocation.verificationLongitude?.toString() ?? '')
      setRadius(updatedLocation.verificationRadiusMeters?.toString() ?? '')
      setError(null)
      toast.success('Verification location saved.')
    },
    onError: (cause) => {
      setError(
        cause instanceof ApiError && cause.status === 403
          ? 'Only GSD users can configure an asset verification location.'
          : 'The verification location could not be saved. Review the values and try again.',
      )
    },
  })

  const save = () => {
    setError(null)
    const parsed = verificationLocationSchema.safeParse({
      verificationLatitude: latitude,
      verificationLongitude: longitude,
      verificationRadiusMeters: radius,
    })
    if (!parsed.success) {
      const issue = parsed.error.issues[0]
      setError(issue?.message ?? 'Review the verification location values.')
      const field = issue?.path[0]
      if (typeof field === 'string') document.getElementById(field)?.focus()
      return
    }

    mutation.mutate({
      verificationLatitude: parsed.data.verificationLatitude ?? null,
      verificationLongitude: parsed.data.verificationLongitude ?? null,
      verificationRadiusMeters: parsed.data.verificationRadiusMeters ?? null,
    })
  }

  return (
    <Card className="space-y-4 p-6 shadow-none">
      <div>
        <h2 className="text-lg font-semibold text-[var(--text-primary)]">
          Inspection location verification
        </h2>
        <p className="mt-1 text-sm text-[var(--text-secondary)]">
          Optional experimental reference for inspection attempts. It never
          blocks an inspection.
        </p>
      </div>
      <fieldset className="grid gap-4 sm:grid-cols-3">
        <legend className="sr-only">Verification location values</legend>
        <div className="space-y-2">
          <Label htmlFor="verificationLatitude">Latitude</Label>
          <Input
            id="verificationLatitude"
            type="number"
            step="any"
            min="-90"
            max="90"
            value={latitude}
            onChange={(event) => {
              setLatitude(event.target.value)
              setError(null)
            }}
            aria-invalid={Boolean(error)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="verificationLongitude">Longitude</Label>
          <Input
            id="verificationLongitude"
            type="number"
            step="any"
            min="-180"
            max="180"
            value={longitude}
            onChange={(event) => {
              setLongitude(event.target.value)
              setError(null)
            }}
            aria-invalid={Boolean(error)}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="verificationRadiusMeters">Radius (meters)</Label>
          <Input
            id="verificationRadiusMeters"
            type="number"
            step="any"
            min="0"
            value={radius}
            onChange={(event) => {
              setRadius(event.target.value)
              setError(null)
            }}
            aria-invalid={Boolean(error)}
          />
        </div>
      </fieldset>
      {error && (
        <p role="alert" className="text-sm text-[var(--error)]">
          {error}
        </p>
      )}
      <Button type="button" onClick={save} disabled={mutation.isPending}>
        {mutation.isPending ? 'Saving location...' : 'Save location'}
      </Button>
    </Card>
  )
}

export function AssetDetail({
  assetId,
  registrySearch = {},
}: {
  assetId: string
  registrySearch?: AssetSearch
}) {
  const isValidId =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      assetId,
    )
  const asset = useAsset(assetId, isValidId)
  const categories = useAssetCategories()
  const currentUser = useCurrentUser()
  const heading = useRef<HTMLHeadingElement>(null)

  useEffect(() => {
    if (asset.data) heading.current?.focus()
  }, [asset.data])

  if (!isValidId) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <h1 className="text-xl font-bold text-[var(--text-primary)]">
          Asset not found
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The asset link is invalid. No registry request was made.
        </p>
        <Button asChild variant="secondary" className="mt-5">
          <Link to="/app/assets" search={registrySearch}>
            Return to assets
          </Link>
        </Button>
      </Card>
    )
  }

  if (asset.isPending) {
    return (
      <div
        className="space-y-4"
        role="status"
        aria-label="Loading asset detail"
      >
        <span className="sr-only">Loading asset detail...</span>
        <Skeleton className="h-9 w-56" />
        <Skeleton className="h-64 w-full" />
      </div>
    )
  }

  if (asset.isError || !asset.data) {
    const error = asset.error

    if (error instanceof ZodError) {
      return (
        <Card
          role="alert"
          className="border-[var(--error)] p-6 text-[var(--error)] shadow-none"
        >
          <h1 className="text-xl font-bold">Asset record error</h1>
          <p className="mt-2 text-sm text-[var(--text-secondary)]">
            This asset record could not be loaded due to a data integrity issue.
          </p>
          <div className="mt-5 flex gap-3">
            <Button type="button" onClick={() => void asset.refetch()}>
              Retry
            </Button>
            <Button asChild variant="secondary">
              <Link to="/app/assets" search={registrySearch}>
                Return to assets
              </Link>
            </Button>
          </div>
        </Card>
      )
    }

    if (error instanceof ApiError) {
      if (error.status === 404) {
        return (
          <Card role="alert" className="p-6 shadow-none">
            <h1 className="text-xl font-bold text-[var(--text-primary)]">
              Asset not found
            </h1>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              This record may no longer be available.
            </p>
            <Button asChild variant="secondary" className="mt-5">
              <Link to="/app/assets" search={registrySearch}>
                Return to assets
              </Link>
            </Button>
          </Card>
        )
      }

      if (error.classification === 'network') {
        return (
          <Card
            role="alert"
            className="border-[var(--error)] p-6 text-[var(--error)] shadow-none"
          >
            <h1 className="text-xl font-bold">Service unavailable</h1>
            <p className="mt-2 text-sm text-[var(--text-secondary)]">
              The service could not be reached. Please check your network
              connection and try again.
            </p>
            <div className="mt-5 flex gap-3">
              <Button type="button" onClick={() => void asset.refetch()}>
                Retry
              </Button>
              <Button asChild variant="secondary">
                <Link to="/app/assets" search={registrySearch}>
                  Return to assets
                </Link>
              </Button>
            </div>
          </Card>
        )
      }
    }

    return (
      <Card
        role="alert"
        className="border-[var(--error)] p-6 text-[var(--error)] shadow-none"
      >
        <h1 className="text-xl font-bold">Asset details unavailable</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          The asset record could not be loaded. Please try again.
        </p>
        <div className="mt-5 flex gap-3">
          <Button type="button" onClick={() => void asset.refetch()}>
            Retry
          </Button>
          <Button asChild variant="secondary">
            <Link to="/app/assets" search={registrySearch}>
              Return to assets
            </Link>
          </Button>
        </div>
      </Card>
    )
  }

  const record = asset.data
  const category = categories.data?.find(
    (item) => item.code === record.assetCategory,
  )

  return (
    <section
      aria-labelledby="asset-detail-title"
      className="max-w-5xl space-y-6"
    >
      <Link
        to="/app/assets"
        search={registrySearch}
        className="text-sm font-semibold text-[var(--primary)] hover:underline"
      >
        Back to assets
      </Link>
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-start">
        <div>
          <p className="text-sm font-semibold tracking-[0.08em] text-[var(--primary)] uppercase">
            Asset record
          </p>
          <h1
            ref={heading}
            id="asset-detail-title"
            tabIndex={-1}
            className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)] outline-none"
          >
            {record.assetCode}
          </h1>
          <p className="mt-2 text-[var(--text-secondary)]">
            {categoryLabel(category, record.assetCategory)}
          </p>
        </div>
        <Badge
          variant={record.status === 'Active' ? 'success' : 'neutral'}
          className="px-3 text-sm font-semibold"
        >
          {record.status}
        </Badge>
      </div>
      <Card className="grid gap-6 shadow-none md:grid-cols-2 lg:grid-cols-3">
        <DetailItem
          label="Category"
          value={categoryLabel(category, record.assetCategory)}
        />
        <DetailItem label="Building" value={record.building} />
        <DetailItem label="Department" value={record.department} />
        <DetailItem label="Location" value={record.location} />
        <DetailItem label="Created" value={formatDate(record.createdAt)} />
        <DetailItem label="Last updated" value={formatDate(record.updatedAt)} />
      </Card>
      <AssetQrLabel
        qrCodeValue={record.qrCodeValue}
        assetCode={record.assetCode}
        assetCategory={categoryLabel(category, record.assetCategory)}
        department={record.department}
        building={record.building}
        location={record.location}
      />
      {currentUser.data?.roles.includes('GSD') && (
        <AssetVerificationLocationEditor key={record.id} asset={record} />
      )}
      <InspectionHistory assetId={record.id} />
    </section>
  )
}
