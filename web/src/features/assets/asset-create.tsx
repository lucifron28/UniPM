import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { toast } from 'sonner'
import { ZodError } from 'zod'
import { createAsset, getGetAssetQueryKey } from '@/api/generated/endpoints'
import { ApiError } from '@/api/problem-details'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  createAssetSchema,
  parseAsset,
  toCreateAssetDto,
  type CreateAssetValues,
} from '@/features/assets/asset-contract'
import { useAssetCategories } from '@/features/assets/asset-queries'
import { useCurrentUser } from '@/features/auth/current-user'

function fieldErrorText(meta: {
  errors: unknown[]
  errorMap: Record<string, unknown>
}): string | undefined {
  const allErrors = [
    ...meta.errors,
    ...Object.values(meta.errorMap || {}).filter(Boolean),
  ]
  const first = allErrors[0]
  if (typeof first === 'string') return first
  if (first && typeof first === 'object' && 'message' in first) {
    return String((first as { message: unknown }).message)
  }
  return undefined
}

const ALLOWED_FIELD_KEYS = new Set<keyof CreateAssetValues>([
  'assetCode',
  'assetCategory',
  'building',
  'department',
  'location',
])

function cleanBackendErrorKey(rawKey: string): keyof CreateAssetValues | null {
  const parts = rawKey.split(/[.[]/)
  const lastSegment = (parts[parts.length - 1] ?? '').replace(/\]$/, '')
  const cleaned = (lastSegment.charAt(0).toLowerCase() +
    lastSegment.slice(1)) as keyof CreateAssetValues
  if (ALLOWED_FIELD_KEYS.has(cleaned)) {
    return cleaned
  }
  return null
}

export function AssetCreate() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const categories = useAssetCategories()
  const currentUser = useCurrentUser()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const summaryRef = useRef<HTMLDivElement>(null)

  const canCreate = currentUser.data?.roles.includes('GSD') ?? false

  const form = useForm({
    defaultValues: {
      assetCode: '',
      assetCategory: '' as CreateAssetValues['assetCategory'],
      building: '',
      department: '',
      location: '',
    },
    onSubmitInvalid: () => {
      setSubmitError('Please review and correct the required fields.')
      const firstInvalidField = [
        'assetCode',
        'assetCategory',
        'building',
        'department',
        'location',
      ].find((key) => {
        const meta = form.getFieldMeta(key as keyof CreateAssetValues)
        return meta && (meta.errors.length > 0 || meta.errorMap.onChange)
      })
      if (firstInvalidField) {
        document.getElementById(firstInvalidField)?.focus()
      } else {
        summaryRef.current?.focus()
      }
    },
    onSubmit: ({ value }) => {
      setSubmitError(null)

      ALLOWED_FIELD_KEYS.forEach((key) => {
        form.setFieldMeta(key, (prev) => ({
          ...prev,
          errorMap: {
            ...prev.errorMap,
            onSubmit: undefined,
          },
        }))
      })

      const parsed = createAssetSchema.safeParse(value)
      if (!parsed.success) {
        let firstInvalidField: string | null = null
        parsed.error.issues.forEach((issue) => {
          const fieldName = issue.path[0] as keyof CreateAssetValues
          if (fieldName && ALLOWED_FIELD_KEYS.has(fieldName)) {
            if (!firstInvalidField) firstInvalidField = fieldName
            form.setFieldMeta(fieldName, (prev) => ({
              ...prev,
              errorMap: {
                ...prev.errorMap,
                onSubmit: issue.message,
              },
            }))
          }
        })

        setSubmitError('Please review and correct the required fields.')

        if (firstInvalidField) {
          const el = document.getElementById(firstInvalidField)
          if (el) {
            el.focus()
            return
          }
        }
        summaryRef.current?.focus()
        return
      }

      mutation.mutate(value)
    },
  })

  const clearBackendFieldError = (key: keyof CreateAssetValues) => {
    setSubmitError(null)
    form.setFieldMeta(key, (prev) => ({
      ...prev,
      errorMap: {
        ...prev.errorMap,
        onSubmit: undefined,
      },
    }))
  }

  const mutation = useMutation({
    mutationFn: async (values: CreateAssetValues) => {
      const rawResponse = await createAsset(toCreateAssetDto(values))
      return parseAsset(rawResponse)
    },
    onSuccess: async (asset) => {
      queryClient.setQueryData(getGetAssetQueryKey(asset.id), asset)
      toast.success('Asset created.')
      await queryClient.invalidateQueries({ queryKey: ['/api/v1/assets'] })
      void navigate({
        to: '/app/assets/$assetId',
        params: { assetId: asset.id },
      })
    },
    onError: (error) => {
      if (error instanceof ZodError) {
        setSubmitError(
          'The server returned an invalid response, so the creation result could not be verified. Check the registry before trying again.',
        )
        summaryRef.current?.focus()
        return
      }

      if (error instanceof ApiError) {
        if (error.status === 403) {
          setSubmitError(
            'Only GSD users can create assets in the current workflow.',
          )
          summaryRef.current?.focus()
          return
        }

        if (error.status === 409) {
          setSubmitError('That asset code already exists.')
          form.setFieldMeta('assetCode', (prev) => ({
            ...prev,
            errorMap: {
              ...prev.errorMap,
              onSubmit: 'That asset code already exists.',
            },
          }))
          summaryRef.current?.focus()
          return
        }

        if (error.status === 400 && error.problem?.errors) {
          let hasMapped = false
          Object.entries(error.problem.errors).forEach(([rawKey, msgs]) => {
            const key = cleanBackendErrorKey(rawKey)
            if (key && Array.isArray(msgs) && msgs.length > 0) {
              const cleanMsg =
                typeof msgs[0] === 'string' ? msgs[0] : 'Invalid value.'
              form.setFieldMeta(key, (prev) => ({
                ...prev,
                errorMap: {
                  ...prev.errorMap,
                  onSubmit: cleanMsg,
                },
              }))
              hasMapped = true
            }
          })
          setSubmitError(
            hasMapped
              ? 'Please correct the highlighted validation errors.'
              : 'The asset details are invalid. Please check your entries.',
          )
          summaryRef.current?.focus()
          return
        }

        if (error.classification === 'network') {
          setSubmitError(
            'The service could not be reached. Please check your network connection and try again.',
          )
          summaryRef.current?.focus()
          return
        }
      }

      setSubmitError(
        'The asset could not be created. Please review the details and try again.',
      )
      summaryRef.current?.focus()
    },
  })

  if (currentUser.isPending)
    return <Card className="p-6">Checking access...</Card>

  if (!canCreate) {
    return (
      <Card role="alert" className="p-6 shadow-none">
        <h1 className="text-xl font-bold">GSD access required</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Only GSD users can create assets in the current workflow.
        </p>
        <Button asChild className="mt-5">
          <Link to="/app/assets">Return to assets</Link>
        </Button>
      </Card>
    )
  }

  if (categories.isError) {
    return (
      <Card
        role="alert"
        className="border-[var(--error)] p-6 text-[var(--error)] shadow-none"
      >
        <h1 className="text-xl font-bold">Asset categories unavailable</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Asset categories could not be loaded. Reference data is required to
          create an asset.
        </p>
        <div className="mt-5 flex gap-3">
          <Button type="button" onClick={() => void categories.refetch()}>
            Retry loading categories
          </Button>
          <Button
            asChild
            className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
          >
            <Link to="/app/assets">Return to assets</Link>
          </Button>
        </div>
      </Card>
    )
  }

  return (
    <section
      aria-labelledby="create-asset-title"
      className="max-w-3xl space-y-6"
    >
      <div>
        <Link
          to="/app/assets"
          className="text-sm font-semibold text-[var(--primary)] hover:underline"
        >
          Back to assets
        </Link>
        <h1
          id="create-asset-title"
          className="mt-4 text-3xl font-bold tracking-tight text-[var(--text-primary)]"
        >
          Add asset
        </h1>
        <p className="mt-2 text-[var(--text-secondary)]">
          Create a registry record using fields supported by the current API
          contract.
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
              <Alert
                role="alert"
                aria-live="polite"
                className="border-[var(--error)] text-[var(--error)]"
              >
                {submitError}
              </Alert>
            </div>
          )}
          <form.Field
            name="assetCode"
            validators={{
              onChange: ({ value }) => {
                const res = createAssetSchema.shape.assetCode.safeParse(value)
                return res.success ? undefined : res.error.issues[0]?.message
              },
            }}
          >
            {(field) => {
              const err = fieldErrorText(field.state.meta)
              return (
                <div className="space-y-2">
                  <Label htmlFor="assetCode">Asset code</Label>
                  <Input
                    id="assetCode"
                    value={field.state.value}
                    onBlur={field.handleBlur}
                    onChange={(event) => {
                      clearBackendFieldError('assetCode')
                      field.handleChange(event.target.value)
                    }}
                    aria-invalid={Boolean(err)}
                    aria-describedby={err ? 'assetCode-error' : undefined}
                  />
                  {err && (
                    <p
                      id="assetCode-error"
                      className="text-sm text-[var(--error)]"
                    >
                      {err}
                    </p>
                  )}
                </div>
              )
            }}
          </form.Field>

          <form.Field
            name="assetCategory"
            validators={{
              onChange: ({ value }) => {
                const res =
                  createAssetSchema.shape.assetCategory.safeParse(value)
                return res.success ? undefined : res.error.issues[0]?.message
              },
            }}
          >
            {(field) => {
              const err = fieldErrorText(field.state.meta)
              return (
                <div className="space-y-2">
                  <Label htmlFor="assetCategory">Category</Label>
                  <select
                    id="assetCategory"
                    value={field.state.value}
                    onBlur={field.handleBlur}
                    onChange={(event) => {
                      clearBackendFieldError('assetCategory')
                      field.handleChange(
                        event.target
                          .value as CreateAssetValues['assetCategory'],
                      )
                    }}
                    aria-invalid={Boolean(err)}
                    aria-describedby={err ? 'assetCategory-error' : undefined}
                    className="min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
                  >
                    <option value="">Choose a category</option>
                    {(categories.data ?? []).map((category) => (
                      <option key={category.code} value={category.code}>
                        {category.displayName}
                      </option>
                    ))}
                  </select>
                  {err && (
                    <p
                      id="assetCategory-error"
                      className="text-sm text-[var(--error)]"
                    >
                      {err}
                    </p>
                  )}
                </div>
              )
            }}
          </form.Field>

          {(['building', 'department', 'location'] as const).map((name) => (
            <form.Field
              key={name}
              name={name}
              validators={{
                onChange: ({ value }) => {
                  const res = createAssetSchema.shape[name].safeParse(value)
                  return res.success ? undefined : res.error.issues[0]?.message
                },
              }}
            >
              {(field) => {
                const err = fieldErrorText(field.state.meta)
                return (
                  <div className="space-y-2">
                    <Label htmlFor={name}>
                      {name.charAt(0).toUpperCase() + name.slice(1)}{' '}
                      <span className="font-normal text-[var(--text-neutral)]">
                        (optional)
                      </span>
                    </Label>
                    <Input
                      id={name}
                      value={field.state.value ?? ''}
                      onBlur={field.handleBlur}
                      onChange={(event) => {
                        clearBackendFieldError(name)
                        field.handleChange(event.target.value)
                      }}
                      aria-invalid={Boolean(err)}
                      aria-describedby={err ? `${name}-error` : undefined}
                    />
                    {err && (
                      <p
                        id={`${name}-error`}
                        className="text-sm text-[var(--error)]"
                      >
                        {err}
                      </p>
                    )}
                  </div>
                )
              }}
            </form.Field>
          ))}

          <form.Subscribe selector={(state) => state.isSubmitting}>
            {(isSubmitting) => {
              const busy = isSubmitting || mutation.isPending
              return (
                <div className="flex flex-wrap gap-3">
                  <Button type="submit" disabled={busy || categories.isPending}>
                    {busy && (
                      <LoaderCircle
                        aria-hidden="true"
                        className="mr-2 size-4 animate-spin"
                      />
                    )}
                    {busy ? 'Creating asset...' : 'Create asset'}
                  </Button>
                  <Button
                    asChild
                    className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
                  >
                    <Link to="/app/assets">Cancel</Link>
                  </Button>
                </div>
              )
            }}
          </form.Subscribe>
        </form>
      </Card>
    </section>
  )
}
