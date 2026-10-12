import { createFileRoute } from '@tanstack/react-router'
import { z } from 'zod'
import { assetCategoryCodes } from '@/features/assets/asset-contract'
import {
  FormRegistry,
  type FormSearch,
} from '@/features/preventive-maintenance-forms/form-registry'
import { preventiveMaintenanceFormStatusCodes } from '@/features/preventive-maintenance-forms/form-contract'

const searchSchema = z.object({
  status: z.enum(preventiveMaintenanceFormStatusCodes).optional(),
  assetCategory: z.enum(assetCategoryCodes).optional(),
  department: z.string().trim().max(256).optional(),
  pmCycle: z
    .string()
    .regex(/^(?!0000)\d{4}-(0[1-9]|1[0-2])$/)
    .optional(),
  search: z.string().trim().max(256).optional(),
  page: z.coerce.number().int().positive().max(10000).optional(),
})

export const Route = createFileRoute('/app/preventive-maintenance-forms/')({
  validateSearch: (search): FormSearch => {
    const parsed = searchSchema.safeParse(search)
    return parsed.success ? parsed.data : {}
  },
  component: FormsPage,
})

function FormsPage() {
  const search = Route.useSearch()
  const navigate = Route.useNavigate()
  return (
    <FormRegistry
      search={search}
      onSearchChange={(next, options) =>
        void navigate({
          search: next,
          ...(options?.replace ? { replace: true } : {}),
          ...(options?.preserveScroll ? { resetScroll: false } : {}),
        })
      }
    />
  )
}
