import { createFileRoute } from '@tanstack/react-router'
import { AdminManagement } from '@/features/admin/admin-management'

export const Route = createFileRoute('/app/admin')({
  component: AdminManagement,
})
