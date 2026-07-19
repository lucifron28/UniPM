import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { useMutation, useQueryClient } from '@tanstack/react-query'
import { Link, useNavigate } from '@tanstack/react-router'
import { LoaderCircle } from 'lucide-react'
import { toast } from 'sonner'
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

function fieldError(errors: unknown[]) {
  const first = errors[0]
  return typeof first === 'string' ? first : undefined
}

export function AssetCreate() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const categories = useAssetCategories()
  const currentUser = useCurrentUser()
  const [submitError, setSubmitError] = useState<string | null>(null)
  const codeRef = useRef<HTMLInputElement>(null)
  const canCreate = currentUser.data?.roles.includes('GSD') ?? false
  const mutation = useMutation({
    mutationFn: async (values: CreateAssetValues) =>
      parseAsset(await createAsset(toCreateAssetDto(values))),
    onSuccess: async (asset) => {
      queryClient.setQueryData(getGetAssetQueryKey(asset.id), asset)
      await queryClient.invalidateQueries({ queryKey: ['/api/v1/assets'] })
      toast.success('Asset created.')
      void navigate({
        to: '/app/assets/$assetId',
        params: { assetId: asset.id },
      })
    },
    onError: (error) => {
      setSubmitError(
        error instanceof ApiError && error.status === 409
          ? 'That asset code already exists.'
          : 'The asset could not be created. Please review the details and try again.',
      )
      codeRef.current?.focus()
    },
  })
  const form = useForm({
    defaultValues: {
      assetCode: '',
      assetCategory: '' as CreateAssetValues['assetCategory'],
      building: '',
      department: '',
      location: '',
    },
    onSubmit: async ({ value }) => {
      setSubmitError(null)
      const parsed = createAssetSchema.safeParse(value)
      if (!parsed.success) {
        setSubmitError(
          parsed.error.issues[0]?.message ?? 'Review the required fields.',
        )
        return
      }
      await mutation.mutateAsync(value)
    },
  })
  if (currentUser.isPending) return <Card>Checking access...</Card>
  if (!canCreate)
    return (
      <Card role="alert">
        <h1 className="text-xl font-bold">GSD access required</h1>
        <p className="mt-2 text-sm text-[var(--text-secondary)]">
          Only GSD users can create assets in the current workflow.
        </p>
        <Button asChild className="mt-5">
          <Link to="/app/assets">Return to assets</Link>
        </Button>
      </Card>
    )
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
            <Alert
              role="alert"
              className="border-[var(--error)] text-[var(--error)]"
            >
              {submitError}
            </Alert>
          )}
          <form.Field
            name="assetCode"
            validators={{
              onBlur: ({ value }) => {
                const result =
                  createAssetSchema.shape.assetCode.safeParse(value)
                return result.success
                  ? undefined
                  : result.error.issues[0]?.message
              },
            }}
          >
            {(field) => (
              <div className="space-y-2">
                <Label htmlFor="assetCode">Asset code</Label>
                <Input
                  ref={codeRef}
                  id="assetCode"
                  value={field.state.value}
                  onBlur={field.handleBlur}
                  onChange={(event) => field.handleChange(event.target.value)}
                  aria-invalid={Boolean(fieldError(field.state.meta.errors))}
                />
                {fieldError(field.state.meta.errors) && (
                  <p className="text-sm text-[var(--error)]">
                    {fieldError(field.state.meta.errors)}
                  </p>
                )}
              </div>
            )}
          </form.Field>
          <form.Field name="assetCategory">
            {(field) => (
              <div className="space-y-2">
                <Label htmlFor="assetCategory">Category</Label>
                <select
                  id="assetCategory"
                  value={field.state.value}
                  onChange={(event) =>
                    field.handleChange(
                      event.target.value as CreateAssetValues['assetCategory'],
                    )
                  }
                  className="min-h-10 w-full rounded-lg border border-[var(--border-soft)] bg-white px-3 text-sm"
                >
                  <option value="">Choose a category</option>
                  {(categories.data ?? []).map((category) => (
                    <option key={category.code} value={category.code}>
                      {category.displayName}
                    </option>
                  ))}
                </select>
              </div>
            )}
          </form.Field>
          {(['building', 'department', 'location'] as const).map((name) => (
            <form.Field key={name} name={name}>
              {(field) => (
                <div className="space-y-2">
                  <Label htmlFor={name}>
                    {name.charAt(0).toUpperCase() + name.slice(1)}{' '}
                    <span className="font-normal text-[var(--text-neutral)]">
                      (optional)
                    </span>
                  </Label>
                  <Input
                    id={name}
                    value={field.state.value}
                    onChange={(event) => field.handleChange(event.target.value)}
                  />
                </div>
              )}
            </form.Field>
          ))}
          <form.Subscribe selector={(state) => state.isSubmitting}>
            {(isSubmitting) => (
              <div className="flex flex-wrap gap-3">
                <Button
                  type="submit"
                  disabled={isSubmitting || categories.isPending}
                >
                  {isSubmitting && (
                    <LoaderCircle
                      aria-hidden="true"
                      className="mr-2 size-4 animate-spin"
                    />
                  )}
                  {isSubmitting ? 'Creating asset...' : 'Create asset'}
                </Button>
                <Button
                  asChild
                  className="bg-white text-[var(--text-primary)] hover:bg-[var(--page-background)]"
                >
                  <Link to="/app/assets">Cancel</Link>
                </Button>
              </div>
            )}
          </form.Subscribe>
        </form>
      </Card>
    </section>
  )
}
