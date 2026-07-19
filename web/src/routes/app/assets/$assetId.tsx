import { createFileRoute } from '@tanstack/react-router'
import { AssetDetail } from '@/features/assets/asset-detail'

export const Route = createFileRoute('/app/assets/$assetId')({
  component: AssetDetailPage,
})

function AssetDetailPage() {
  const { assetId } = Route.useParams()
  return <AssetDetail assetId={assetId} />
}
