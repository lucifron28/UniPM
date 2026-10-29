import {
  RecordField as DetailItem,
  RecordHeader,
  RecordMetadata,
  RecordSection,
} from '@/features/shared/record-layout'
import { useEffect, useRef } from 'react'
import { ZodError } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Skeleton } from '@/components/ui/skeleton'
import { useAssetCategories, useAsset } from '@/features/assets/asset-queries'
import { categoryLabel, formatDate } from '@/features/assets/asset-presentation'
import { AssetQrLabel } from '@/features/assets/asset-qr-label'
import type { AssetSearch } from '@/features/assets/asset-registry'
import { InspectionHistory } from '@/features/inspections/inspection-history'
import { DetailBackLink } from '@/features/shared/detail-back-link'
import type {
  DetailReturnContext,
  DetailReturnFallback,
} from '@/features/shared/detail-navigation'

export function AssetDetail({
  assetId,
  registrySearch = {},
  returnContext,
}: {
  assetId: string
  registrySearch?: AssetSearch
  returnContext?: DetailReturnContext | undefined
}) {
  const returnFallback: DetailReturnFallback = {
    kind: 'assetRegistry',
    search: registrySearch,
  }
  const isValidId =
    /^[0-9a-f]{8}-[0-9a-f]{4}-[1-5][0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i.test(
      assetId,
    )
  const asset = useAsset(assetId, isValidId)
  const categories = useAssetCategories()
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
          <DetailBackLink context={returnContext} fallback={returnFallback} />
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
              <DetailBackLink
                context={returnContext}
                fallback={returnFallback}
              />
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
              <DetailBackLink
                context={returnContext}
                fallback={returnFallback}
              />
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
                <DetailBackLink
                  context={returnContext}
                  fallback={returnFallback}
                />
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
            <DetailBackLink context={returnContext} fallback={returnFallback} />
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
    <section aria-labelledby="asset-detail-title" className="record-page">
      <DetailBackLink context={returnContext} fallback={returnFallback} />
      <RecordHeader
        id="asset-detail-title"
        eyebrow="Asset record"
        title={record.assetCode}
        headingRef={heading}
        description={categoryLabel(category, record.assetCategory)}
        status={
          <Badge
            variant={record.status === 'Active' ? 'success' : 'neutral'}
            className="px-3 text-sm"
          >
            {record.status}
          </Badge>
        }
      />
      <div className="record-columns">
        <RecordSection title="Equipment and location">
          <dl className="grid gap-5 sm:grid-cols-2">
            <DetailItem
              label="Category"
              value={categoryLabel(category, record.assetCategory)}
            />
            <DetailItem label="Department" value={record.department} />
            <DetailItem label="Building" value={record.building} />
            <DetailItem label="Location" value={record.location} />
          </dl>
          <p className="record-note mt-5">
            Use the QR label to identify this asset during inspection. Its
            recorded maintenance appears below.
          </p>
        </RecordSection>
        <AssetQrLabel
          qrCodeValue={record.qrCodeValue}
          assetCode={record.assetCode}
          assetCategory={categoryLabel(category, record.assetCategory)}
          department={record.department}
          building={record.building}
          location={record.location}
        />
      </div>
      <RecordMetadata title="Record information">
        <dl className="grid gap-4 sm:grid-cols-2">
          <DetailItem label="Created" value={formatDate(record.createdAt)} />
          <DetailItem
            label="Last updated"
            value={formatDate(record.updatedAt)}
          />
        </dl>
      </RecordMetadata>
      <InspectionHistory
        assetId={record.id}
        assetCode={record.assetCode}
        returnContext={returnContext}
        fallback={returnFallback}
      />
    </section>
  )
}
