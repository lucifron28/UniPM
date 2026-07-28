import { createFileRoute } from '@tanstack/react-router'
import { FormRegistry } from '@/features/preventive-maintenance-forms/form-registry'

export const Route = createFileRoute('/app/preventive-maintenance-forms/')({
  component: FormRegistry,
})
