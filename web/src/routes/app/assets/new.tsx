import { createFileRoute } from '@tanstack/react-router'
import { AssetCreate } from '@/features/assets/asset-create'

export const Route = createFileRoute('/app/assets/new')({
  component: AssetCreate,
})
