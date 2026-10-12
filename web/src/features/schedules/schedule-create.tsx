import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { toast } from 'sonner'
import { ZodError } from 'zod'
import {
  createSchedule,
  getGetScheduleQueryKey,
} from '@/api/generated/endpoints'
import { ApiError } from '@/api/problem-details'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useAssetCategories, useAssets } from '@/features/assets/asset-queries'
import { useCurrentUser } from '@/features/auth/current-user'
import {
  createScheduleSchema,
  parseSchedule,
  toCreateScheduleDto,
  type CreateScheduleValues,
} from '@/features/schedules/schedule-contract'
import {
  formatMonthName,
  formatPmCycle,
  formatPmCycleDueDate,
  getCurrentManilaYear,
} from '@/features/schedules/schedule-presentation'

type FieldName = keyof CreateScheduleValues
type FieldErrors = Partial<Record<FieldName, string>>

const fieldNames = new Set<FieldName>(['assetId', 'month', 'year'])

function backendFieldName(raw: string): FieldName | null {
  const segment = raw.split(/[.[]/).at(-1)?.replace(/\]$/, '') ?? ''
  const normalized = segment.charAt(0).toLowerCase() + segment.slice(1)
  if (normalized === 'pmCycle' || normalized === 'scheduleDate') {
    return 'month'
  }
  const key = normalized as FieldName
  return fieldNames.has(key) ? key : null
}

export function ScheduleCreate() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const currentUser = useCurrentUser()
  const assets = useAssets()
  const categories = useAssetCategories()
  const eligibleAssets = (assets.data ?? []).filter(
    (asset) => asset.status === 'Active' && Boolean(asset.department?.trim()),
  )
  const assetCategories = categories.data ?? []
  const currentManilaYear = getCurrentManilaYear()
  const summaryRef = useRef<HTMLDivElement>(null)
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({})
  const [submitError, setSubmitError] = useState<string | null>(null)

  const canCreate =
    currentUser.data?.roles.some(
      (role) => role === 'GSD' || role === 'Supervisor',
    ) ?? false

  const clearFieldError = (key: FieldName) => {
    setFieldErrors((current) => {
      const next = { ...current }
      delete next[key]
      return next
    })
    setSubmitError(null)
  }

  const mutation = useMutation({
    mutationFn: async (formValues: CreateScheduleValues) =>
      createSchedule(toCreateScheduleDto(formValues)).then(parseSchedule),
    onSuccess: async (schedule) => {
      queryClient.setQueryData(getGetScheduleQueryKey(schedule.id), schedule)
      await queryClient.invalidateQueries({ queryKey: ['/api/v1/schedules'] })
      toast.success('Schedule created.')
      void navigate({
        to: '/app/schedules/$scheduleId',
        params: { scheduleId: schedule.id },
      })
    },
    onError: (error) => {
      if (error instanceof ZodError) {
        setSubmitError(
          'The server returned an invalid response, so the creation result could not be verified. Check the schedule registry before trying again.',
        )
      } else if (error instanceof ApiError) {
        if (error.status === 403) {
          setSubmitError(
            'Only GSD or Supervisor users can create schedules in the current workflow.',
          )
        } else if (error.status === 404) {
          setSubmitError('The selected asset no longer exists.')
          setFieldErrors((current) => ({
            ...current,
            assetId: 'Choose an existing asset.',
          }))
        } else if (error.status === 400 && error.problem?.errors) {
          const mapped: FieldErrors = {}
          Object.entries(error.problem.errors).forEach(([rawKey, messages]) => {
            const key = backendFieldName(rawKey)
            if (
              key &&
              Array.isArray(messages) &&
              typeof messages[0] === 'string'
            ) {
              mapped[key] = messages[0]
            }
          })
          setFieldErrors(mapped)
          setSubmitError(
            Object.keys(mapped).length > 0
              ? 'Please correct the highlighted validation errors.'
              : 'The schedule details are invalid. Please check your entries.',
          )
        } else if (error.classification === 'network') {
          setSubmitError(
            'The service could not be reached. Check your connection and try again.',
          )
        } else {
          setSubmitError('The schedule could not be created. Please try again.')
        }
      } else {
        setSubmitError('The schedule could not be created. Please try again.')
      }
      queueMicrotask(() => summaryRef.current?.focus())
    },
  })

  const form = useForm({
    defaultValues: {
      assetId: '',
      year: undefined,
      month: undefined,
      allowedMonths: [],
    } as CreateScheduleValues,
    onSubmit: ({ value }) => {
      setFieldErrors({})
      setSubmitError(null)
      const parsed = createScheduleSchema.safeParse(value)
      if (!parsed.success) {
        const errors: FieldErrors = {}
        parsed.error.issues.forEach((issue) => {
          const field = issue.path[0] as FieldName
          if (field && fieldNames.has(field) && !errors[field]) {
            errors[field] = issue.message
          }
        })
        setFieldErrors(errors)
        setSubmitError('Please review and correct the required fields.')
        queueMicrotask(() => {
          const firstField = Object.keys(errors)[0]
          if (firstField) document.getElementById(firstField)?.focus()
          else summaryRef.current?.focus()
        })
        return
      }
      mutation.mutate(value)
    },
  })

  if (currentUser.isPending)
    return <Card className="p-6">Checking access...</Card>

  if (!canCreate) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <h1 className="text-xl font-bold">Schedule manager access required</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Only GSD or Supervisor users can create schedules in the current
          workflow.
        </p>
        <Button asChild className="mt-5">
          <Link to="/app/schedules">Return to schedules</Link>
        </Button>
      </Card>
    )
  }

  if (assets.isError || categories.isError) {
    return (
      <Card role="alert" className="border-[var(--error)] p-6 shadow-none">
        <h1 className="text-xl font-bold">
          Schedule reference data unavailable
        </h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Assets and CPMP month reference data are required before a schedule
          can be created.
        </p>
        <Button
          type="button"
          className="mt-5"
          onClick={() => {
            void assets.refetch()
            void categories.refetch()
          }}
        >
          Retry reference data
        </Button>
      </Card>
    )
  }

  return (
    <section
      aria-labelledby="create-schedule-title"
      className="max-w-3xl space-y-6"
    >
      <div>
        <Link
          to="/app/schedules"
          className="text-sm font-semibold text-[var(--primary)] hover:underline"
        >
          Back to schedules
        </Link>
        <h1
          id="create-schedule-title"
          className="mt-4 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
        >
          Add schedule
        </h1>
        <p className="mt-2 text-[var(--text-secondary)]">
          Choose an asset, scheduled year, and allowed PM month. The due date is
          calculated from the selected month.
        </p>
      </div>

      <Card className="shadow-none">
        <form
          noValidate
          className="space-y-5"
          onSubmit={(event) => {
            event.preventDefault()
            void form.handleSubmit()
          }}
        >
          {submitError && (
            <div ref={summaryRef} tabIndex={-1} className="outline-none">
              <Alert>{submitError}</Alert>
            </div>
          )}

          <form.Field name="assetId">
            {(field) => (
              <div className="space-y-2">
                <Label htmlFor="assetId">Asset</Label>
                <select
                  id="assetId"
                  value={field.state.value}
                  aria-invalid={fieldErrors.assetId ? true : undefined}
                  aria-describedby={
                    fieldErrors.assetId ? 'assetId-error' : undefined
                  }
                  onChange={(event) => {
                    const assetId = event.target.value
                    field.handleChange(assetId)
                    const selectedAsset = eligibleAssets.find(
                      (asset) => asset.id === assetId,
                    )
                    const allowedMonths =
                      assetCategories.find(
                        (category) =>
                          category.code === selectedAsset?.assetCategory,
                      )?.scheduledMonths ?? []
                    form.setFieldValue('month', undefined)
                    form.setFieldValue('allowedMonths', allowedMonths)
                    clearFieldError('assetId')
                    clearFieldError('month')
                  }}
                  className="min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
                >
                  <option value="">Choose an asset</option>
                  {eligibleAssets.map((asset) => (
                    <option key={asset.id} value={asset.id}>
                      {asset.assetCode} - {asset.assetCategory}
                    </option>
                  ))}
                </select>
                <p className="text-xs text-[var(--text-secondary)]">
                  Only active assets with a department can be scheduled.
                </p>
                {fieldErrors.assetId && (
                  <p id="assetId-error" className="text-sm text-[var(--error)]">
                    {fieldErrors.assetId}
                  </p>
                )}
              </div>
            )}
          </form.Field>

          <form.Subscribe selector={(state) => state.values}>
            {(values) => {
              const selectedAsset = eligibleAssets.find(
                (asset) => asset.id === values.assetId,
              )
              const allowedMonths =
                assetCategories.find(
                  (category) => category.code === selectedAsset?.assetCategory,
                )?.scheduledMonths ?? []
              const year = Number(values.year)
              const month = Number(values.month)
              const pmCycle =
                Number.isInteger(year) &&
                year >= 2000 &&
                Number.isInteger(month) &&
                month >= 1 &&
                month <= 12
                  ? `${year}-${String(month).padStart(2, '0')}`
                  : null

              return (
                <div className="grid gap-5 sm:grid-cols-2">
                  <form.Field name="year">
                    {(field) => (
                      <div className="space-y-2">
                        <Label htmlFor="year">Scheduled year</Label>
                        <Input
                          id="year"
                          type="number"
                          min={currentManilaYear}
                          max={currentManilaYear}
                          value={field.state.value ?? ''}
                          aria-invalid={fieldErrors.year ? true : undefined}
                          aria-describedby={
                            fieldErrors.year ? 'year-error' : undefined
                          }
                          onChange={(event) => {
                            field.handleChange(event.target.value)
                            clearFieldError('year')
                          }}
                        />
                        {fieldErrors.year && (
                          <p
                            id="year-error"
                            className="text-sm text-[var(--error)]"
                          >
                            {fieldErrors.year}
                          </p>
                        )}
                      </div>
                    )}
                  </form.Field>

                  <form.Field name="month">
                    {(field) => (
                      <div className="space-y-2">
                        <Label htmlFor="month">Scheduled month</Label>
                        <select
                          id="month"
                          value={field.state.value ?? ''}
                          disabled={
                            !selectedAsset ||
                            categories.isPending ||
                            allowedMonths.length === 0
                          }
                          aria-invalid={fieldErrors.month ? true : undefined}
                          aria-describedby={
                            fieldErrors.month ? 'month-error' : undefined
                          }
                          onChange={(event) => {
                            field.handleChange(
                              event.target.value
                                ? Number(event.target.value)
                                : undefined,
                            )
                            clearFieldError('month')
                          }}
                          className="min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
                        >
                          <option value="">
                            {selectedAsset
                              ? 'Choose an allowed PM month'
                              : 'Choose an asset first'}
                          </option>
                          {[...allowedMonths]
                            .sort((left, right) => left - right)
                            .map((allowedMonth) => (
                              <option key={allowedMonth} value={allowedMonth}>
                                {formatMonthName(allowedMonth)}
                              </option>
                            ))}
                        </select>
                        {selectedAsset && allowedMonths.length === 0 && (
                          <p className="text-sm text-[var(--error)]">
                            Allowed PM months are unavailable for this asset
                            category. A schedule cannot be created.
                          </p>
                        )}
                        {fieldErrors.month && (
                          <p
                            id="month-error"
                            className="text-sm text-[var(--error)]"
                          >
                            {fieldErrors.month}
                          </p>
                        )}
                      </div>
                    )}
                  </form.Field>

                  <div className="space-y-2 sm:col-span-2">
                    <Label htmlFor="dueDate">Due date</Label>
                    <Input
                      id="dueDate"
                      value={
                        pmCycle
                          ? formatPmCycleDueDate(pmCycle)
                          : 'Choose a scheduled year and month'
                      }
                      readOnly
                    />
                    {pmCycle && (
                      <p className="text-xs text-[var(--text-secondary)]">
                        Scheduled month: {formatPmCycle(pmCycle)}
                      </p>
                    )}
                  </div>
                </div>
              )
            }}
          </form.Subscribe>

          <div className="flex flex-wrap gap-3">
            <Button
              type="submit"
              disabled={
                mutation.isPending || assets.isPending || categories.isPending
              }
            >
              {mutation.isPending && (
                <LoaderCircle
                  aria-hidden="true"
                  className="mr-2 size-4 animate-spin"
                />
              )}
              {mutation.isPending ? 'Creating schedule...' : 'Create schedule'}
            </Button>
            <Button
              asChild
              className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
            >
              <Link to="/app/schedules">Cancel</Link>
            </Button>
          </div>
        </form>
      </Card>
    </section>
  )
}
