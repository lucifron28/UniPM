import { useRef, useState } from 'react'
import { useForm } from '@tanstack/react-form'
import { LoaderCircle, LockKeyhole, LogIn, Mail } from 'lucide-react'
import { z } from 'zod'
import { ApiError } from '@/api/problem-details'
import { Alert } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import {
  authenticate,
  AuthSessionResponseError,
} from '@/features/auth/auth-session-service'
import { useAuthStore } from '@/stores/auth-store'

const emailSchema = z.string().trim().email('Enter a valid email address.')
const passwordSchema = z.string().min(1, 'Enter your password.')

const loginFormSchema = z.object({
  email: emailSchema,
  password: passwordSchema,
})

type LoginValues = z.infer<typeof loginFormSchema>
type FieldErrors = Partial<Record<keyof LoginValues, string | undefined>>

function firstProblemError(error: ApiError, field: keyof LoginValues) {
  const entries = Object.entries(error.problem?.errors ?? {})
  return entries.find(([key]) => key.toLowerCase() === field)?.[1]?.[0]
}

function safeLoginError(error: unknown) {
  if (error instanceof AuthSessionResponseError) return error.message
  if (!(error instanceof ApiError))
    return 'Sign in could not be completed. Please try again.'
  if (error.status === 401) return 'Invalid email or password.'
  if (error.status === 403)
    return 'Sign in is unavailable because this web origin is not allowed.'
  if (error.classification === 'network')
    return 'The service could not be reached. Check your connection and try again.'
  return 'Sign in could not be completed. Please try again.'
}

function fieldMessage(errors: unknown[]) {
  const first = errors[0]
  if (typeof first === 'string') return first
  if (first && typeof first === 'object' && 'message' in first) {
    const message = (first as { message?: unknown }).message
    return typeof message === 'string' ? message : undefined
  }
  return undefined
}

function schemaFieldErrors(values: LoginValues): FieldErrors {
  const result = loginFormSchema.safeParse(values)
  if (result.success) return {}

  return Object.fromEntries(
    result.error.issues.flatMap((issue) => {
      const field = issue.path[0]
      return field === 'email' || field === 'password'
        ? [[field, issue.message]]
        : []
    }),
  )
}

export function LoginForm({
  onAuthenticated,
}: {
  onAuthenticated: () => void | Promise<void>
}) {
  const [submitError, setSubmitError] = useState<string | null>(null)
  const [serverFieldErrors, setServerFieldErrors] = useState<FieldErrors>({})
  const passwordRef = useRef<HTMLInputElement>(null)
  const initializationError = useAuthStore((state) => state.initializationError)
  const clearInitializationError = useAuthStore(
    (state) => state.clearInitializationError,
  )

  const form = useForm({
    defaultValues: { email: '', password: '' },
    validators: { onSubmit: loginFormSchema },
    onSubmit: async ({ value, formApi }) => {
      setSubmitError(null)
      setServerFieldErrors({})
      clearInitializationError()
      try {
        await authenticate({
          email: value.email.trim(),
          password: value.password,
        })
        await onAuthenticated()
      } catch (error) {
        if (error instanceof ApiError && error.status === 400) {
          setServerFieldErrors({
            email: firstProblemError(error, 'email'),
            password: firstProblemError(error, 'password'),
          })
        }
        setSubmitError(safeLoginError(error))
        formApi.setFieldValue('password', '')
        passwordRef.current?.focus()
      }
    },
  })

  return (
    <form
      noValidate
      onSubmit={(event) => {
        event.preventDefault()
        event.stopPropagation()
        setServerFieldErrors(schemaFieldErrors(form.state.values))
        void form.handleSubmit()
      }}
      className="mt-8 space-y-6"
    >
      {(initializationError || submitError) && (
        <Alert
          className="border-[color-mix(in_srgb,var(--error)_35%,var(--border-soft))] bg-white text-[var(--error)]"
          aria-live="assertive"
        >
          {submitError ?? initializationError}
        </Alert>
      )}

      <form.Field
        name="email"
        validators={{
          onSubmit: ({ value }) => {
            const result = emailSchema.safeParse(value)
            return result.success ? undefined : result.error.issues[0]?.message
          },
          onBlur: ({ value }) => {
            const result = emailSchema.safeParse(value)
            return result.success ? undefined : result.error.issues[0]?.message
          },
        }}
      >
        {(field) => {
          const message =
            serverFieldErrors.email ?? fieldMessage(field.state.meta.errors)
          return (
            <div className="space-y-2">
              <Label htmlFor={field.name}>Institutional email</Label>
              <div className="relative">
                <Mail
                  aria-hidden="true"
                  className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-[var(--text-secondary)]"
                />
                <Input
                  id={field.name}
                  name={field.name}
                  type="email"
                  autoComplete="email"
                  placeholder="name@university.edu"
                  value={field.state.value}
                  onBlur={field.handleBlur}
                  onChange={(event) => {
                    setServerFieldErrors((current) => ({
                      ...current,
                      email: undefined,
                    }))
                    field.handleChange(event.target.value)
                  }}
                  aria-invalid={Boolean(message)}
                  aria-describedby={message ? 'email-error' : undefined}
                  className="pl-11"
                />
              </div>
              {message && (
                <p id="email-error" className="text-sm text-[var(--error)]">
                  {message}
                </p>
              )}
            </div>
          )
        }}
      </form.Field>

      <form.Field
        name="password"
        validators={{
          onSubmit: ({ value }) => {
            const result = passwordSchema.safeParse(value)
            return result.success ? undefined : result.error.issues[0]?.message
          },
          onBlur: ({ value }) => {
            const result = passwordSchema.safeParse(value)
            return result.success ? undefined : result.error.issues[0]?.message
          },
        }}
      >
        {(field) => {
          const message =
            serverFieldErrors.password ?? fieldMessage(field.state.meta.errors)
          return (
            <div className="space-y-2">
              <Label htmlFor={field.name}>Password</Label>
              <div className="relative">
                <LockKeyhole
                  aria-hidden="true"
                  className="pointer-events-none absolute top-1/2 left-4 size-4 -translate-y-1/2 text-[var(--text-secondary)]"
                />
                <Input
                  ref={passwordRef}
                  id={field.name}
                  name={field.name}
                  type="password"
                  autoComplete="current-password"
                  value={field.state.value}
                  onBlur={field.handleBlur}
                  onChange={(event) => {
                    setServerFieldErrors((current) => ({
                      ...current,
                      password: undefined,
                    }))
                    field.handleChange(event.target.value)
                  }}
                  aria-invalid={Boolean(message)}
                  aria-describedby={message ? 'password-error' : undefined}
                  className="pl-11"
                />
              </div>
              {message && (
                <p id="password-error" className="text-sm text-[var(--error)]">
                  {message}
                </p>
              )}
            </div>
          )
        }}
      </form.Field>

      <form.Subscribe selector={(state) => state.isSubmitting}>
        {(isSubmitting) => (
          <Button
            type="submit"
            disabled={isSubmitting}
            className="h-14 w-full text-base shadow-[0_10px_22px_color-mix(in_srgb,var(--primary)_22%,transparent)]"
          >
            {isSubmitting ? (
              <>
                <LoaderCircle
                  aria-hidden="true"
                  className="mr-2 size-4 animate-spin"
                />
                Signing in...
              </>
            ) : (
              <>
                Sign in <LogIn aria-hidden="true" className="ml-2 size-4" />
              </>
            )}
          </Button>
        )}
      </form.Subscribe>
    </form>
  )
}
