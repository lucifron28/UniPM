import {
  useCallback,
  useEffect,
  useMemo,
  useRef,
  useState,
  useLayoutEffect,
  type FormEvent,
  type ReactNode,
} from 'react'
import { useInfiniteQuery, useQuery } from '@tanstack/react-query'
import { Alert } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { PageHeading } from '@/components/layout/page-heading'
import { SearchablePicker } from '@/components/ui/searchable-picker'
import { ApiError } from '@/api/problem-details'
import {
  assetCategoryCodes,
  assetStatusCodes,
} from '@/features/assets/asset-contract'
import {
  schedulePeriodTypeCodes,
  scheduleQuarterCodes,
  scheduleStatusCodes,
} from '@/features/schedules/schedule-contract'
import { useGenerateSchedules } from '@/features/schedules/schedule-queries'
import {
  adminApi,
  type Account,
  type Asset,
  type DeleteResult,
  type Inspection,
  type MaintenanceForm,
  type Page,
  type Schedule,
  type ValidationDatasetResult,
} from './admin-api'

type AdminSection = 'overview' | 'accounts' | 'assets' | 'maintenance' | 'demo'

const sections: { id: AdminSection; label: string }[] = [
  { id: 'overview', label: 'Overview' },
  { id: 'accounts', label: 'Accounts' },
  { id: 'assets', label: 'Assets' },
  { id: 'maintenance', label: 'Maintenance' },
  { id: 'demo', label: 'Demo data' },
]

function messageFor(error: unknown) {
  if (error instanceof ApiError) {
    const details = Object.values(error.problem?.errors ?? {}).flat()
    return details.length > 0
      ? `${error.message} ${details.join(' ')}`
      : error.message
  }
  if (error instanceof Error && error.message) return error.message
  return 'The request could not be completed. Check the data and try again.'
}

function confirmAction(message: string) {
  return window.confirm(message)
}

function fieldClassName() {
  return 'h-10 w-full rounded-md border border-[var(--border-control)] bg-white px-3 text-sm text-[var(--text-primary)] outline-none focus-visible:border-[var(--primary)] focus-visible:ring-2 focus-visible:ring-[color-mix(in_srgb,var(--primary)_25%,transparent)]'
}

function Field({
  label,
  value,
  onChange,
  type = 'text',
  required,
  placeholder,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  type?: string
  required?: boolean
  placeholder?: string
}) {
  return (
    <label className="block space-y-1.5 text-sm font-medium text-[var(--text-secondary)]">
      <span>{label}</span>
      <Input
        type={type}
        value={value}
        onChange={(event) => onChange(event.target.value)}
        required={required}
        placeholder={placeholder}
      />
    </label>
  )
}

function SelectField({
  label,
  value,
  onChange,
  options,
}: {
  label: string
  value: string
  onChange: (value: string) => void
  options: readonly (string | { value: string; label: string })[]
}) {
  return (
    <label className="block space-y-1.5 text-sm font-medium text-[var(--text-secondary)]">
      <span>{label}</span>
      <select
        className={fieldClassName()}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      >
        {options.map((option) =>
          typeof option === 'string' ? (
            <option key={option} value={option}>
              {option || 'All'}
            </option>
          ) : (
            <option key={option.value} value={option.value}>
              {option.label}
            </option>
          ),
        )}
      </select>
    </label>
  )
}

function Modal({
  title,
  description,
  onClose,
  busy = false,
  children,
}: {
  title: string
  description?: string
  onClose: () => void
  busy?: boolean
  children: ReactNode
}) {
  const dialogRef = useRef<HTMLElement>(null)
  const busyRef = useRef(busy)
  const onCloseRef = useRef(onClose)
  useLayoutEffect(() => {
    busyRef.current = busy
    onCloseRef.current = onClose
  }, [busy, onClose])
  useLayoutEffect(() => {
    const previousFocus =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null
    const previousOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    const focusable = () =>
      Array.from(
        dialogRef.current?.querySelectorAll<HTMLElement>(
          'button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href], [tabindex]:not([tabindex="-1"])',
        ) ?? [],
      )
    focusable()[0]?.focus()

    function handleKeys(event: KeyboardEvent) {
      if (event.key === 'Escape') {
        if (!busyRef.current) onCloseRef.current()
        event.preventDefault()
        return
      }
      if (event.key !== 'Tab') return
      const items = focusable()
      if (items.length === 0) {
        event.preventDefault()
        dialogRef.current?.focus()
      } else if (event.shiftKey && document.activeElement === items[0]) {
        event.preventDefault()
        items.at(-1)?.focus()
      } else if (!event.shiftKey && document.activeElement === items.at(-1)) {
        event.preventDefault()
        items[0]?.focus()
      }
    }
    document.addEventListener('keydown', handleKeys)
    return () => {
      document.removeEventListener('keydown', handleKeys)
      document.body.style.overflow = previousOverflow
      if (previousFocus?.isConnected) previousFocus.focus()
    }
  }, [])

  return (
    <div
      className="fixed inset-0 z-[100] grid place-items-center bg-black/45 p-4"
      onMouseDown={(event) => {
        if (!busyRef.current && event.target === event.currentTarget)
          onCloseRef.current()
      }}
    >
      <section
        ref={dialogRef}
        tabIndex={-1}
        role="dialog"
        aria-modal="true"
        aria-labelledby="admin-dialog-title"
        className="max-h-[min(90vh,54rem)] w-full max-w-xl overflow-y-auto rounded-xl border border-[var(--border-soft)] bg-white p-5 shadow-xl outline-none sm:p-6"
      >
        <header className="mb-5 flex items-start justify-between gap-4">
          <div>
            <h2
              id="admin-dialog-title"
              className="text-lg font-bold text-[var(--text-primary)]"
            >
              {title}
            </h2>
            {description && (
              <p className="mt-1 text-sm text-[var(--text-neutral)]">
                {description}
              </p>
            )}
          </div>
          <button
            type="button"
            aria-label="Close dialog"
            disabled={busy}
            onClick={onClose}
            className="grid size-9 shrink-0 place-items-center rounded-md text-xl text-[var(--text-neutral)] hover:bg-[var(--surface-muted)] disabled:opacity-50"
          >
            ×
          </button>
        </header>
        {children}
      </section>
    </div>
  )
}

function ModalActions({
  onClose,
  saving,
  saveLabel = 'Save changes',
  disabled = false,
}: {
  onClose: () => void
  saving: boolean
  saveLabel?: string
  disabled?: boolean
}) {
  return (
    <div className="mt-6 flex justify-end gap-2">
      <Button
        type="button"
        variant="secondary"
        disabled={saving || disabled}
        onClick={onClose}
      >
        Cancel
      </Button>
      <Button type="submit" disabled={saving || disabled}>
        {saving ? 'Saving…' : saveLabel}
      </Button>
    </div>
  )
}

function usePage<T>(
  queryKey: readonly unknown[],
  loader: () => Promise<Page<T>>,
  onError: (message: string) => void,
) {
  const query = useQuery({ queryKey, queryFn: loader, retry: false })
  useEffect(() => {
    if (query.error) onError(messageFor(query.error))
  }, [query.error, onError])
  return { result: query.data ?? null, loading: query.isPending }
}

export function AdminManagement() {
  const [section, setSection] = useState<AdminSection>('overview')
  const [revision, setRevision] = useState(0)
  const [notice, setNotice] = useState('')
  const [error, setError] = useState('')
  const handleError = useCallback((value: string) => {
    setNotice('')
    setError(value)
  }, [])
  const handleMutation = useCallback((value: string) => {
    setError('')
    setNotice(value)
    setRevision((current) => current + 1)
  }, [])

  return (
    <section aria-labelledby="admin-page-title" className="space-y-5">
      <PageHeading
        id="admin-page-title"
        eyebrow="System administration"
        title="Admin management"
        description="Manage accounts and demo maintenance data. Normal maintenance workflows remain the source of operational records."
        actions={
          <Button
            type="button"
            variant="secondary"
            onClick={() => setRevision((current) => current + 1)}
          >
            Refresh
          </Button>
        }
      />
      {error && (
        <Alert className="border-[var(--error)] text-[var(--error)]">
          {error}
        </Alert>
      )}
      {notice && (
        <Alert className="border-[var(--success)] text-[var(--success)]">
          {notice}
        </Alert>
      )}
      <div
        role="tablist"
        aria-label="Admin sections"
        className="flex gap-1 overflow-x-auto border-b border-[var(--border-soft)]"
      >
        {sections.map((item) => (
          <button
            key={item.id}
            id={`admin-tab-${item.id}`}
            role="tab"
            type="button"
            aria-selected={section === item.id}
            aria-controls="admin-tab-panel"
            onClick={() => setSection(item.id)}
            className={`min-h-11 shrink-0 border-b-2 px-4 text-sm font-semibold transition-colors ${section === item.id ? 'border-[var(--primary)] text-[var(--primary)]' : 'border-transparent text-[var(--text-neutral)] hover:text-[var(--text-primary)]'}`}
          >
            {item.label}
          </button>
        ))}
      </div>
      <div
        id="admin-tab-panel"
        role="tabpanel"
        aria-labelledby={`admin-tab-${section}`}
      >
        {section === 'overview' && (
          <OverviewPanel revision={revision} onError={handleError} />
        )}
        {section === 'accounts' && (
          <AccountsPanel
            revision={revision}
            onError={handleError}
            onMutation={handleMutation}
          />
        )}
        {section === 'assets' && (
          <AssetsPanel
            revision={revision}
            onError={handleError}
            onMutation={handleMutation}
          />
        )}
        {section === 'maintenance' && (
          <MaintenancePanel
            revision={revision}
            onError={handleError}
            onMutation={handleMutation}
          />
        )}
        {section === 'demo' && (
          <DemoPanel onError={handleError} onMutation={handleMutation} />
        )}
      </div>
    </section>
  )
}

function OverviewPanel({
  revision,
  onError,
}: {
  revision: number
  onError: (message: string) => void
}) {
  const query = useQuery({
    queryKey: ['admin', 'overview', revision],
    queryFn: adminApi.overview,
    retry: false,
  })
  useEffect(() => {
    if (query.error) onError(messageFor(query.error))
  }, [query.error, onError])
  const overview = query.data ?? null
  const loading = query.isPending
  const metrics = useMemo(
    () =>
      overview
        ? ([
            ['Active accounts', overview.activeAccounts],
            ['Inactive accounts', overview.inactiveAccounts],
            ['Assets', overview.assets],
            ['Schedules', overview.schedules],
            ['Inspections', overview.inspections],
            ['Draft forms', overview.draftForms],
            ['Submitted forms', overview.submittedForms],
            ['Acknowledged forms', overview.acknowledgedForms],
            ['Acknowledgements', overview.acknowledgements],
          ] as const)
        : [],
    [overview],
  )

  return (
    <div className="space-y-4">
      <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
        {loading && (
          <p role="status" className="text-sm text-[var(--text-neutral)]">
            Loading admin overview…
          </p>
        )}
        {!loading &&
          metrics.map(([label, value]) => (
            <Card key={label} className="p-4">
              <p className="text-sm font-medium text-[var(--text-neutral)]">
                {label}
              </p>
              <p className="mt-2 text-3xl font-bold tracking-tight text-[var(--text-primary)]">
                {value.toLocaleString()}
              </p>
            </Card>
          ))}
      </div>
      <Card className="border-l-4 border-l-[var(--primary)]">
        <h2 className="font-semibold text-[var(--text-primary)]">
          Administration scope
        </h2>
        <p className="mt-1 text-sm text-[var(--text-neutral)]">
          Admin tools manage users and demonstration records. Create inspection
          and form records through the established maintenance workflow.
        </p>
      </Card>
    </div>
  )
}

function Pager({
  page,
  totalCount,
  onPage,
}: {
  page: number
  totalCount: number
  onPage: (page: number) => void
}) {
  const pageCount = Math.max(1, Math.ceil(totalCount / 25))
  return (
    <div className="flex items-center justify-between gap-3 border-t border-[var(--border-soft)] px-4 py-3 text-sm text-[var(--text-neutral)]">
      <span>
        {totalCount.toLocaleString()} records · page {page} of {pageCount}
      </span>
      <div className="flex gap-2">
        <Button
          type="button"
          variant="secondary"
          className="min-h-9 px-3"
          disabled={page <= 1}
          onClick={() => onPage(page - 1)}
        >
          Previous
        </Button>
        <Button
          type="button"
          variant="secondary"
          className="min-h-9 px-3"
          disabled={page >= pageCount}
          onClick={() => onPage(page + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  )
}

function TableFrame({ children }: { children: ReactNode }) {
  return (
    <div className="overflow-x-auto rounded-lg border border-[var(--border-soft)] bg-white">
      <table className="w-full min-w-[680px] border-collapse text-left text-sm">
        {children}
      </table>
    </div>
  )
}

function TableHead({ children }: { children: ReactNode }) {
  return (
    <thead className="bg-[var(--surface-muted)] text-xs font-semibold tracking-wide text-[var(--text-neutral)] uppercase">
      <tr>{children}</tr>
    </thead>
  )
}

function Th({ children }: { children?: ReactNode }) {
  return (
    <th scope="col" className="px-4 py-3">
      {children}
    </th>
  )
}

function Td({
  children,
  className = '',
  colSpan,
}: {
  children?: ReactNode
  className?: string
  colSpan?: number
}) {
  return (
    <td
      colSpan={colSpan}
      className={`border-t border-[var(--border-soft)] px-4 py-3 align-middle ${className}`}
    >
      {children}
    </td>
  )
}

function AccountEditor({
  account,
  onClose,
  onMutation,
  onError,
}: {
  account: Account | null
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [displayName, setDisplayName] = useState(account?.displayName ?? '')
  const [email, setEmail] = useState(account?.email ?? '')
  const [role, setRole] = useState(account?.role ?? 'Inspector')
  const [isActive, setIsActive] = useState(account?.isActive ?? true)
  const [password, setPassword] = useState('')
  const [saving, setSaving] = useState(false)
  const isEdit = account !== null
  const roles = ['Inspector', 'Supervisor'] as const
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      if (account) {
        await adminApi.updateAccount(account.id, {
          displayName,
          email,
          role,
          isActive,
        })
        onMutation('Account updated. Existing sessions were revoked.')
      } else {
        await adminApi.createAccount({
          displayName,
          email,
          role,
          isActive: true,
          initialPassword: password,
        })
        onMutation('Account created.')
      }
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title={isEdit ? 'Edit account' : 'Create account'}
      onClose={onClose}
      busy={saving}
      description="Admin changes revoke the account’s existing sessions."
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="Display name"
          value={displayName}
          onChange={setDisplayName}
          required
        />
        <Field
          label="Login email"
          value={email}
          onChange={setEmail}
          type="email"
          required
        />
        <SelectField
          label="Role"
          value={role}
          onChange={setRole}
          options={roles}
        />
        {!isEdit && (
          <Field
            label="Initial password"
            value={password}
            onChange={setPassword}
            type="password"
            required
          />
        )}
        {isEdit && (
          <label className="flex items-center gap-2 text-sm text-[var(--text-secondary)]">
            <input
              type="checkbox"
              checked={isActive}
              onChange={(event) => setIsActive(event.target.checked)}
            />{' '}
            Active account
          </label>
        )}
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel={isEdit ? 'Save account' : 'Create account'}
        />
      </form>
    </Modal>
  )
}

function PasswordReset({
  account,
  onClose,
  onMutation,
  onError,
}: {
  account: Account
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [password, setPassword] = useState('')
  const [confirmation, setConfirmation] = useState('')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (password !== confirmation) {
      onError('Passwords do not match.')
      return
    }
    setSaving(true)
    try {
      await adminApi.resetPassword(account.id, password)
      onMutation('Password reset. Existing sessions were revoked.')
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title={`Reset password · ${account.displayName}`}
      description="The password is sent once to the authenticated API and is never returned."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="New password"
          value={password}
          onChange={setPassword}
          type="password"
          required
        />
        <Field
          label="Confirm new password"
          value={confirmation}
          onChange={setConfirmation}
          type="password"
          required
        />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel="Reset password"
        />
      </form>
    </Modal>
  )
}

function DemoAccountRestore({
  onClose,
  onMutation,
  onError,
}: {
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [inspectorPassword, setInspectorPassword] = useState('')
  const [supervisorPassword, setSupervisorPassword] = useState('')
  const [saving, setSaving] = useState(false)
  async function restore(role: 'Inspector' | 'Supervisor', password: string) {
    const email =
      role === 'Inspector' ? 'inspector@unipm.local' : 'supervisor@unipm.local'
    const displayName =
      role === 'Inspector' ? 'Maintenance Inspector' : 'Maintenance Supervisor'
    const page = await adminApi.accounts({
      search: email,
      page: 1,
      pageSize: 100,
    })
    const account = page.items.find(
      (candidate) => candidate.email.toLowerCase() === email,
    )
    if (
      account &&
      !account.role
        .split(',')
        .map((value) => value.trim())
        .includes(role)
    ) {
      throw new Error(
        `${email} is already assigned another role; resolve that account first.`,
      )
    }
    if (account) {
      await adminApi.updateAccount(account.id, {
        displayName,
        email,
        role,
        isActive: true,
      })
      await adminApi.resetPassword(account.id, password)
    } else {
      await adminApi.createAccount({
        displayName,
        email,
        role,
        isActive: true,
        initialPassword: password,
      })
    }
  }
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      await restore('Inspector', inspectorPassword)
      await restore('Supervisor', supervisorPassword)
      onMutation(
        'Demo Inspector and Supervisor accounts restored. The active GSD head was left unchanged.',
      )
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title="Restore demo accounts"
      description="Enter fresh passwords. They are sent to the Admin API and stored only as Identity password hashes. The current GSD head is preserved."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="Inspector password"
          value={inspectorPassword}
          onChange={setInspectorPassword}
          type="password"
          required
        />
        <Field
          label="Supervisor password"
          value={supervisorPassword}
          onChange={setSupervisorPassword}
          type="password"
          required
        />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel="Restore accounts"
        />
      </form>
    </Modal>
  )
}

function GsdHeadPanel({
  revision,
  onMutation,
  onError,
}: {
  revision: number
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const headQuery = useQuery({
    queryKey: ['admin', 'gsd-head', revision],
    queryFn: adminApi.gsdHead,
    retry: false,
  })
  const head = headQuery.data ?? null
  const [dialog, setDialog] = useState<'edit' | 'transfer' | null>(null)
  const [replacementSearch, setReplacementSearch] = useState('')
  const [displayName, setDisplayName] = useState('')
  const [email, setEmail] = useState('')
  const [replacementId, setReplacementId] = useState('')
  const [replacementAccount, setReplacementAccount] = useState<Account | null>(
    null,
  )
  const [createReplacement, setCreateReplacement] = useState(false)
  const [password, setPassword] = useState('')
  const [saving, setSaving] = useState(false)
  const replacementQuery = useInfiniteQuery({
    queryKey: ['admin', 'gsd-replacements', replacementSearch, revision],
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const params = {
        search: replacementSearch || undefined,
        active: true,
        page: pageParam,
        pageSize: 25,
      }
      const [inspectors, supervisors] = await Promise.all([
        adminApi.accounts({ ...params, role: 'Inspector' }),
        adminApi.accounts({ ...params, role: 'Supervisor' }),
      ])
      return { inspectors, supervisors }
    },
    getNextPageParam: (lastPage) => {
      const inspectorMore =
        lastPage.inspectors.page * lastPage.inspectors.pageSize <
        lastPage.inspectors.totalCount
      const supervisorMore =
        lastPage.supervisors.page * lastPage.supervisors.pageSize <
        lastPage.supervisors.totalCount
      return inspectorMore || supervisorMore
        ? lastPage.inspectors.page + 1
        : undefined
    },
    enabled: dialog === 'transfer' && !createReplacement,
    retry: false,
  })
  useEffect(() => {
    if (headQuery.error) onError(messageFor(headQuery.error))
  }, [headQuery.error, onError])
  useEffect(() => {
    if (replacementQuery.error) onError(messageFor(replacementQuery.error))
  }, [replacementQuery.error, onError])

  function openEdit() {
    setDisplayName(head?.activeHead?.displayName ?? '')
    setEmail(head?.activeHead?.email ?? '')
    setDialog('edit')
  }
  async function saveHead(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      if (dialog === 'edit') {
        await adminApi.updateGsdHead({ displayName, email })
        onMutation('GSD head profile updated. Existing sessions were revoked.')
      } else if (createReplacement) {
        await adminApi.replaceGsdHead({
          displayName,
          email,
          initialPassword: password,
        })
        onMutation(
          'GSD head transferred to the new account. Affected sessions were revoked.',
        )
      } else {
        if (!replacementId)
          throw new Error('Choose an active Inspector or Supervisor account.')
        await adminApi.replaceGsdHead({ replacementUserId: replacementId })
        onMutation('GSD head transferred. Affected sessions were revoked.')
      }
      setDialog(null)
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }

  const replacementOptions = [
    ...new Map(
      (replacementQuery.data?.pages ?? [])
        .flatMap((page) => [
          ...page.inspectors.items,
          ...page.supervisors.items,
        ])
        .map((account) => [account.id, account]),
    ).values(),
  ]
  const pickerOptions = replacementOptions.map((account) => ({
    value: account.id,
    label: account.displayName,
    description: `${account.role} · ${account.email}`,
    item: account,
  }))
  return (
    <Card className="space-y-4 border-l-4 border-l-[var(--primary)]">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold text-[var(--text-primary)]">GSD head</h2>
          <p className="mt-1 text-sm text-[var(--text-neutral)]">
            Keep one active GSD head. Transfer is explicit and revokes affected
            sessions.
          </p>
        </div>
        <div className="flex gap-2">
          {head?.activeHead && (
            <Button type="button" variant="secondary" onClick={openEdit}>
              Edit details
            </Button>
          )}
          <Button
            type="button"
            variant="secondary"
            onClick={() => {
              setCreateReplacement(false)
              setReplacementId('')
              setReplacementAccount(null)
              setReplacementSearch('')
              setDialog('transfer')
            }}
          >
            Transfer head
          </Button>
        </div>
      </div>
      {head?.activeHead ? (
        <div className="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm">
          <strong>{head.activeHead.displayName}</strong>
          <span className="text-[var(--text-neutral)]">
            {head.activeHead.email}
          </span>
          <Badge variant="success">Active</Badge>
        </div>
      ) : (
        <Alert>
          There is no single active GSD head. Resolve this before loading the
          validation dataset.
        </Alert>
      )}
      {head?.accounts.length ? (
        <p className="text-xs text-[var(--text-neutral)]">
          {head.accounts.length} GSD account
          {head.accounts.length === 1 ? '' : 's'} on record
        </p>
      ) : null}
      {dialog && (
        <Modal
          title={dialog === 'edit' ? 'Edit GSD head' : 'Transfer GSD head'}
          description={
            dialog === 'transfer'
              ? 'Choose one active Inspector or Supervisor account, or create a new GSD account. The current head loses the GSD role.'
              : 'Changes revoke the current GSD head’s active sessions.'
          }
          onClose={() => setDialog(null)}
          busy={saving}
        >
          <form
            onSubmit={(event) => void saveHead(event)}
            className="space-y-4"
          >
            {dialog === 'edit' ? (
              <>
                <Field
                  label="Display name"
                  value={displayName}
                  onChange={setDisplayName}
                  required
                />
                <Field
                  label="Login email"
                  value={email}
                  onChange={setEmail}
                  type="email"
                  required
                />
              </>
            ) : (
              <>
                <label className="flex items-center gap-2 text-sm text-[var(--text-secondary)]">
                  <input
                    type="checkbox"
                    checked={createReplacement}
                    onChange={(event) =>
                      setCreateReplacement(event.target.checked)
                    }
                  />{' '}
                  Create a new GSD account
                </label>
                {createReplacement ? (
                  <>
                    <Field
                      label="Display name"
                      value={displayName}
                      onChange={setDisplayName}
                      required
                    />
                    <Field
                      label="Login email"
                      value={email}
                      onChange={setEmail}
                      type="email"
                      required
                    />
                    <Field
                      label="Initial password"
                      value={password}
                      onChange={setPassword}
                      type="password"
                      required
                    />
                  </>
                ) : (
                  <SearchablePicker
                    label="Active replacement account"
                    value={replacementId || undefined}
                    options={pickerOptions}
                    selectedOption={
                      replacementAccount
                        ? {
                            value: replacementAccount.id,
                            label: replacementAccount.displayName,
                            description: `${replacementAccount.role} · ${replacementAccount.email}`,
                          }
                        : undefined
                    }
                    placeholder="Search active personnel"
                    loading={replacementQuery.isPending}
                    loadingMore={replacementQuery.isFetchingNextPage}
                    hasMore={Boolean(replacementQuery.hasNextPage)}
                    onLoadMore={() => void replacementQuery.fetchNextPage()}
                    onSearchChange={setReplacementSearch}
                    onChange={(value, option) => {
                      setReplacementId(value ?? '')
                      setReplacementAccount(option?.item ?? null)
                    }}
                  />
                )}
                {!createReplacement && (
                  <p className="text-xs text-[var(--text-neutral)]">
                    Select an existing active Inspector or Supervisor account.
                  </p>
                )}
              </>
            )}
            <ModalActions
              onClose={() => setDialog(null)}
              saving={saving}
              saveLabel={
                dialog === 'transfer' ? 'Transfer GSD head' : 'Save details'
              }
              disabled={
                dialog === 'transfer' && !createReplacement && !replacementId
              }
            />
          </form>
        </Modal>
      )}
    </Card>
  )
}

function AccountsPanel({
  revision,
  onError,
  onMutation,
}: {
  revision: number
  onError: (message: string) => void
  onMutation: (message: string) => void
}) {
  const [search, setSearch] = useState('')
  const [role, setRole] = useState('')
  const [page, setPage] = useState(1)
  const [modal, setModal] = useState<
    | { kind: 'edit'; account: Account | null }
    | { kind: 'password'; account: Account }
    | { kind: 'restore' }
    | null
  >(null)
  const loader = useCallback(
    () =>
      adminApi.accounts({
        search,
        role: role || undefined,
        page,
        pageSize: 25,
      }),
    [search, role, page],
  )
  const { result, loading } = usePage(
    ['admin', 'accounts', search, role, page, revision],
    loader,
    onError,
  )
  async function deleteAccount(account: Account) {
    if (
      !confirmAction(
        `Remove access for ${account.displayName}? Historical accounts may be retained inactive.`,
      )
    )
      return
    try {
      const result = await adminApi.deleteAccount(account.id)
      onMutation(
        result.retainedForHistory
          ? 'Account access removed; its history is retained.'
          : 'Account deleted.',
      )
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  async function toggleActive(account: Account) {
    try {
      await adminApi.updateAccount(account.id, {
        displayName: account.displayName,
        email: account.email,
        role: account.role,
        isActive: !account.isActive,
      })
      onMutation(
        account.isActive
          ? 'Account deactivated. Existing sessions were revoked.'
          : 'Account reactivated.',
      )
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  return (
    <div className="space-y-4">
      <GsdHeadPanel
        revision={revision}
        onMutation={onMutation}
        onError={onError}
      />
      <Card className="space-y-4 p-0">
        <div className="flex flex-wrap items-end justify-between gap-3 p-4">
          <div className="grid flex-1 gap-3 sm:grid-cols-[minmax(16rem,1fr)_12rem]">
            <label className="block space-y-1.5 text-sm font-medium text-[var(--text-secondary)]">
              <span>Search accounts</span>
              <Input
                aria-label="Search accounts"
                value={search}
                onChange={(event) => {
                  setSearch(event.target.value)
                  setPage(1)
                }}
                placeholder="Name or email"
              />
            </label>
            <SelectField
              label="Role"
              value={role}
              onChange={(value) => {
                setRole(value)
                setPage(1)
              }}
              options={['', 'Inspector', 'Supervisor', 'GSD']}
            />
          </div>
          <div className="flex flex-wrap gap-2">
            <Button
              type="button"
              variant="secondary"
              onClick={() => setModal({ kind: 'restore' })}
            >
              Restore demo accounts
            </Button>
            <Button
              type="button"
              onClick={() => setModal({ kind: 'edit', account: null })}
            >
              Create account
            </Button>
          </div>
        </div>
        <TableFrame>
          <TableHead>
            <Th>Name</Th>
            <Th>Email</Th>
            <Th>Role</Th>
            <Th>Status</Th>
            <Th>Actions</Th>
          </TableHead>
          <tbody>
            {loading && (
              <tr>
                <Td colSpan={5}>Loading accounts…</Td>
              </tr>
            )}
            {!loading && result?.items.length === 0 && (
              <tr>
                <Td colSpan={5}>No matching accounts.</Td>
              </tr>
            )}
            {result?.items.map((account) => (
              <tr key={account.id}>
                <Td className="font-semibold text-[var(--text-primary)]">
                  {account.displayName}
                </Td>
                <Td>{account.email}</Td>
                <Td>{account.role}</Td>
                <Td>
                  <Badge variant={account.isActive ? 'success' : 'neutral'}>
                    {account.isActive ? 'Active' : 'Inactive'}
                  </Badge>
                </Td>
                <Td>
                  <div className="flex flex-wrap gap-2">
                    {!account.role
                      .split(',')
                      .some((role) => role.trim() === 'GSD') && (
                      <>
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => setModal({ kind: 'edit', account })}
                        >
                          Edit
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() =>
                            setModal({ kind: 'password', account })
                          }
                        >
                          Reset password
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => void toggleActive(account)}
                        >
                          {account.isActive ? 'Deactivate' : 'Activate'}
                        </Button>
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5 text-[var(--error)]"
                          onClick={() => void deleteAccount(account)}
                        >
                          Delete
                        </Button>
                      </>
                    )}
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </TableFrame>
        {result && (
          <Pager page={page} totalCount={result.totalCount} onPage={setPage} />
        )}
      </Card>
      {modal?.kind === 'edit' && (
        <AccountEditor
          account={modal.account}
          onClose={() => setModal(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
      {modal?.kind === 'password' && (
        <PasswordReset
          account={modal.account}
          onClose={() => setModal(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
      {modal?.kind === 'restore' && (
        <DemoAccountRestore
          onClose={() => setModal(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
    </div>
  )
}

function AssetEditor({
  asset,
  onClose,
  onMutation,
  onError,
}: {
  asset: Asset | null
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [assetCode, setAssetCode] = useState(asset?.assetCode ?? '')
  const [category, setCategory] = useState(
    asset?.assetCategory ?? assetCategoryCodes[0],
  )
  const [status, setStatus] = useState(asset?.status ?? 'Active')
  const [building, setBuilding] = useState(asset?.building ?? '')
  const [department, setDepartment] = useState(asset?.department ?? '')
  const [location, setLocation] = useState(asset?.location ?? '')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      const body = {
        assetCode,
        assetCategory: category,
        status,
        building,
        department,
        location,
      }
      if (asset) {
        await adminApi.updateAsset(asset.id, body)
        onMutation('Asset updated.')
      } else {
        await adminApi.createAsset({
          assetCode,
          assetCategory: category,
          building,
          department,
          location,
        })
        onMutation('Asset created with its current maintenance cycles.')
      }
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title={asset ? 'Edit asset' : 'Add asset'}
      description="Asset creation uses the existing registry rules and adds upcoming maintenance cycles."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="Asset code"
          value={assetCode}
          onChange={setAssetCode}
          required
        />
        <SelectField
          label="Category"
          value={category}
          onChange={setCategory}
          options={assetCategoryCodes}
        />
        {asset && (
          <SelectField
            label="Status"
            value={status}
            onChange={setStatus}
            options={assetStatusCodes}
          />
        )}
        <Field label="Building" value={building} onChange={setBuilding} />
        <Field label="Department" value={department} onChange={setDepartment} />
        <Field label="Location" value={location} onChange={setLocation} />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel={asset ? 'Save asset' : 'Create asset'}
        />
      </form>
    </Modal>
  )
}

function AssetsPanel({
  revision,
  onError,
  onMutation,
}: {
  revision: number
  onError: (message: string) => void
  onMutation: (message: string) => void
}) {
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const [editing, setEditing] = useState<Asset | null | undefined>(undefined)
  const loader = useCallback(
    () => adminApi.assets({ search, page, pageSize: 25 }),
    [search, page],
  )
  const { result, loading } = usePage(
    ['admin', 'assets', search, page, revision],
    loader,
    onError,
  )
  async function deleteRows(ids: string[]) {
    if (
      !ids.length ||
      !confirmAction(
        `Delete ${ids.length} selected asset${ids.length === 1 ? '' : 's'} and their linked maintenance records?`,
      )
    )
      return
    try {
      const deleted =
        ids.length === 1
          ? await adminApi.deleteAsset(ids[0]!)
          : await adminApi.deleteAssets(ids)
      setSelected([])
      onMutation(deleteSummary(deleted))
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  return (
    <div className="space-y-4">
      <Card className="space-y-4 p-0">
        <div className="flex flex-wrap items-end justify-between gap-3 p-4">
          <label className="block w-full max-w-md space-y-1.5 text-sm font-medium text-[var(--text-secondary)]">
            <span>Search assets</span>
            <Input
              aria-label="Search assets"
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
                setSelected([])
              }}
              placeholder="Asset, building, department"
            />
          </label>
          <div className="flex gap-2">
            <Button
              type="button"
              variant="secondary"
              disabled={!selected.length}
              onClick={() => void deleteRows(selected)}
            >
              Delete selected ({selected.length})
            </Button>
            <Button type="button" onClick={() => setEditing(null)}>
              Add asset
            </Button>
          </div>
        </div>
        <TableFrame>
          <TableHead>
            <Th>
              <input
                aria-label="Select all assets on this page"
                type="checkbox"
                checked={Boolean(
                  result?.items.length &&
                  selected.length === result.items.length,
                )}
                onChange={(event) =>
                  setSelected(
                    event.target.checked
                      ? (result?.items.map((item) => item.id) ?? [])
                      : [],
                  )
                }
              />
            </Th>
            <Th>Asset</Th>
            <Th>Category</Th>
            <Th>Department / location</Th>
            <Th>Status</Th>
            <Th>Actions</Th>
          </TableHead>
          <tbody>
            {loading && (
              <tr>
                <Td colSpan={6}>Loading assets…</Td>
              </tr>
            )}
            {!loading && result?.items.length === 0 && (
              <tr>
                <Td colSpan={6}>No matching assets.</Td>
              </tr>
            )}
            {result?.items.map((asset) => (
              <tr key={asset.id}>
                <Td>
                  <input
                    aria-label={`Select ${asset.assetCode}`}
                    type="checkbox"
                    checked={selected.includes(asset.id)}
                    onChange={(event) =>
                      setSelected((current) =>
                        event.target.checked
                          ? [...current, asset.id]
                          : current.filter((id) => id !== asset.id),
                      )
                    }
                  />
                </Td>
                <Td>
                  <strong className="text-[var(--text-primary)]">
                    {asset.assetCode}
                  </strong>
                  <div className="text-xs text-[var(--text-neutral)]">
                    {asset.building ?? 'Building not set'}
                  </div>
                </Td>
                <Td>{asset.assetCategory}</Td>
                <Td>
                  {asset.department ?? '—'}
                  <div className="text-xs text-[var(--text-neutral)]">
                    {asset.location ?? '—'}
                  </div>
                </Td>
                <Td>
                  <Badge
                    variant={asset.status === 'Active' ? 'success' : 'neutral'}
                  >
                    {asset.status}
                  </Badge>
                </Td>
                <Td>
                  <div className="flex gap-2">
                    <Button
                      type="button"
                      variant="secondary"
                      className="min-h-8 px-2.5"
                      onClick={() => setEditing(asset)}
                    >
                      Edit
                    </Button>
                    <Button
                      type="button"
                      variant="secondary"
                      className="min-h-8 px-2.5 text-[var(--error)]"
                      onClick={() => void deleteRows([asset.id])}
                    >
                      Delete
                    </Button>
                  </div>
                </Td>
              </tr>
            ))}
          </tbody>
        </TableFrame>
        {result && (
          <Pager
            page={page}
            totalCount={result.totalCount}
            onPage={(next) => {
              setPage(next)
              setSelected([])
            }}
          />
        )}
      </Card>
      {editing !== undefined && (
        <AssetEditor
          asset={editing}
          onClose={() => setEditing(undefined)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
    </div>
  )
}

function deleteSummary(result: DeleteResult) {
  const summary = `Removed ${result.assetsRemoved} assets, ${result.schedulesRemoved} schedules, ${result.inspectionsRemoved} inspections, ${result.formsRemoved} forms, and ${result.acknowledgementsRemoved} acknowledgements.`
  return result.photoFileCleanupFailures > 0
    ? `${summary} ${result.photoFileCleanupFailures} photo file(s) need cleanup.`
    : summary
}

function CurrentYearScheduleGeneration({
  onError,
  onMutation,
}: {
  onError: (message: string) => void
  onMutation: (message: string) => void
}) {
  const generation = useGenerateSchedules()
  const year = Number(
    new Intl.DateTimeFormat('en', {
      timeZone: 'Asia/Manila',
      year: 'numeric',
    }).format(new Date()),
  )
  return (
    <Card className="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 className="font-semibold text-[var(--text-primary)]">
          Schedule generation
        </h2>
        <p className="mt-1 text-sm text-[var(--text-neutral)]">
          Create missing CPMP schedules for the current Manila calendar year (
          {year}).
        </p>
      </div>
      <Button
        type="button"
        disabled={generation.isPending}
        onClick={() =>
          generation.mutate(year, {
            onSuccess: (result) =>
              onMutation(
                `Year ${result.year}: created ${result.createdSchedules} schedules; ${result.existingSchedules} already existed; ${result.deferredSchedules} deferred for GSD review.`,
              ),
            onError: (error) => onError(messageFor(error)),
          })
        }
      >
        {generation.isPending ? 'Generating…' : 'Generate schedules'}
      </Button>
    </Card>
  )
}

function ScheduleEditor({
  row,
  onClose,
  onMutation,
  onError,
}: {
  row: Schedule
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [scheduleDate, setScheduleDate] = useState(
    row.scheduleDate.slice(0, 10),
  )
  const [periodType, setPeriodType] = useState(row.periodType)
  const [quarter, setQuarter] = useState(row.quarter ?? '')
  const [semester, setSemester] = useState(row.semester ?? '')
  const [academicYear, setAcademicYear] = useState(row.academicYear ?? '')
  const [status, setStatus] = useState(row.status)
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      await adminApi.updateSchedule(row.id, {
        assetId: row.assetId,
        scheduleDate: new Date(`${scheduleDate}T00:00:00.000Z`).toISOString(),
        periodType,
        quarter: quarter || null,
        semester: semester || null,
        academicYear: academicYear || null,
        status,
        assignedToUserId: row.inspectorUserId,
        assignedSupervisorUserId: row.supervisorUserId,
      })
      onMutation('Schedule updated.')
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title={`Edit schedule · ${row.assetCode}`}
      description="Schedule changes use existing period and status validation."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="Schedule date"
          type="date"
          value={scheduleDate}
          onChange={setScheduleDate}
          required
        />
        <SelectField
          label="Period type"
          value={periodType}
          onChange={setPeriodType}
          options={schedulePeriodTypeCodes}
        />
        <SelectField
          label="Status"
          value={status}
          onChange={setStatus}
          options={scheduleStatusCodes}
        />
        <SelectField
          label="Quarter"
          value={quarter}
          onChange={setQuarter}
          options={['', ...scheduleQuarterCodes]}
        />
        <Field label="Semester" value={semester} onChange={setSemester} />
        <Field
          label="Academic year"
          value={academicYear}
          onChange={setAcademicYear}
        />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel="Save schedule"
        />
      </form>
    </Modal>
  )
}

function FormEditor({
  row,
  onClose,
  onMutation,
  onError,
}: {
  row: MaintenanceForm
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [fileNumber, setFileNumber] = useState(row.fileNumber ?? '')
  const [assetCategory, setAssetCategory] = useState(row.assetCategory)
  const [building, setBuilding] = useState(row.building ?? '')
  const [department, setDepartment] = useState(row.department ?? '')
  const [pmCycle, setPmCycle] = useState(row.pmCycle ?? '')
  const [periodType, setPeriodType] = useState(row.periodType)
  const [quarter, setQuarter] = useState(row.quarter ?? '')
  const [semester, setSemester] = useState(row.semester ?? '')
  const [year, setYear] = useState(row.year?.toString() ?? '')
  const [academicYear, setAcademicYear] = useState(row.academicYear ?? '')
  const [saving, setSaving] = useState(false)
  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    setSaving(true)
    try {
      await adminApi.updateForm(row.id, {
        fileNumber: fileNumber || null,
        assetCategory,
        building: building || null,
        department: department || null,
        pmCycle: pmCycle || null,
        periodType,
        quarter: quarter || null,
        semester: semester || null,
        year: year ? Number(year) : null,
        academicYear: academicYear || null,
      })
      onMutation('Form details updated; its lifecycle status was preserved.')
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }
  return (
    <Modal
      title={`Edit form · ${row.fileNumber ?? 'Unnumbered'}`}
      description="This edits form metadata and leaves Draft, Submitted, or Acknowledged status unchanged."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <Field
          label="File number"
          value={fileNumber}
          onChange={setFileNumber}
        />
        <SelectField
          label="Asset category"
          value={assetCategory}
          onChange={setAssetCategory}
          options={assetCategoryCodes}
        />
        <Field label="Building" value={building} onChange={setBuilding} />
        <Field label="Department" value={department} onChange={setDepartment} />
        <Field
          label="PM cycle (yyyy-MM)"
          value={pmCycle}
          onChange={setPmCycle}
        />
        <SelectField
          label="Period type"
          value={periodType}
          onChange={setPeriodType}
          options={schedulePeriodTypeCodes}
        />
        <SelectField
          label="Quarter"
          value={quarter}
          onChange={setQuarter}
          options={['', ...scheduleQuarterCodes]}
        />
        <Field label="Semester" value={semester} onChange={setSemester} />
        <Field label="Year" value={year} onChange={setYear} type="number" />
        <Field
          label="Academic year"
          value={academicYear}
          onChange={setAcademicYear}
        />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel="Save form details"
        />
      </form>
    </Modal>
  )
}

function localDateTime(value: string) {
  const date = new Date(value)
  return new Date(date.getTime() - date.getTimezoneOffset() * 60_000)
    .toISOString()
    .slice(0, 16)
}

function InspectionEntryModal({
  form,
  onClose,
  onMutation,
  onError,
}: {
  form: MaintenanceForm | null
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const [scheduleSearch, setScheduleSearch] = useState('')
  const [inspectorSearch, setInspectorSearch] = useState('')
  const [schedule, setSchedule] = useState<Schedule | null>(null)
  const [inspector, setInspector] = useState<Account | null>(null)
  const [department, setDepartment] = useState(form?.department ?? '')
  const [dateInspected, setDateInspected] = useState(
    localDateTime(new Date().toISOString()),
  )
  const [isOperational, setIsOperational] = useState(false)
  const [remarks, setRemarks] = useState('')
  const [actionsRecommendations, setActionsRecommendations] = useState('')
  const [saving, setSaving] = useState(false)
  const scheduleQuery = useInfiniteQuery({
    queryKey: ['admin', 'inspection-entry-schedules', scheduleSearch, form?.id],
    initialPageParam: 1,
    queryFn: async ({ pageParam }) => {
      const statuses = ['Due', 'Overdue', 'Ongoing']
      const pages = await Promise.all(
        statuses.map((status) =>
          adminApi.schedules({
            search: scheduleSearch || undefined,
            status,
            page: pageParam,
            pageSize: 25,
          }),
        ),
      )
      return { pages }
    },
    getNextPageParam: (lastPage, allPages) => {
      const hasMore = lastPage.pages.some(
        (page) => page.page * page.pageSize < page.totalCount,
      )
      return hasMore ? allPages.length + 1 : undefined
    },
    retry: false,
  })
  const inspectorQuery = useInfiniteQuery({
    queryKey: ['admin', 'inspection-entry-inspectors', inspectorSearch],
    initialPageParam: 1,
    queryFn: ({ pageParam }) =>
      adminApi.accounts({
        search: inspectorSearch || undefined,
        role: 'Inspector',
        active: true,
        page: pageParam,
        pageSize: 25,
      }),
    getNextPageParam: (lastPage) =>
      lastPage.page * lastPage.pageSize < lastPage.totalCount
        ? lastPage.page + 1
        : undefined,
    retry: false,
  })
  useEffect(() => {
    if (scheduleQuery.error) onError(messageFor(scheduleQuery.error))
  }, [scheduleQuery.error, onError])
  useEffect(() => {
    if (inspectorQuery.error) onError(messageFor(inspectorQuery.error))
  }, [inspectorQuery.error, onError])
  const scheduleOptions = useMemo(
    () =>
      (scheduleQuery.data?.pages ?? [])
        .flatMap((page) => page.pages.flatMap((statusPage) => statusPage.items))
        .filter(
          (candidate) =>
            !form ||
            (candidate.assetCategory === form.assetCategory &&
              (candidate.department ?? '').trim().toLowerCase() ===
                (form.department ?? '').trim().toLowerCase() &&
              (!form.pmCycle || candidate.pmCycle === form.pmCycle)),
        )
        .map((candidate) => ({
          value: candidate.id,
          label: `${candidate.assetCode} · ${candidate.pmCycle}`,
          description: `${candidate.assetCategory} · ${candidate.department ?? 'No department'} · ${candidate.status}`,
          item: candidate,
        })),
    [form, scheduleQuery.data],
  )
  const inspectorOptions = useMemo(
    () =>
      (inspectorQuery.data?.pages ?? [])
        .flatMap((page) => page.items)
        .map((candidate) => ({
          value: candidate.id,
          label: candidate.displayName,
          description: candidate.email,
          item: candidate,
        })),
    [inspectorQuery.data],
  )
  const selectedScheduleOption = schedule
    ? {
        value: schedule.id,
        label: `${schedule.assetCode} · ${schedule.pmCycle}`,
        item: schedule,
      }
    : undefined
  const selectedInspectorOption = inspector
    ? {
        value: inspector.id,
        label: inspector.displayName,
        description: inspector.email,
        item: inspector,
      }
    : undefined

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!schedule || !inspector) return
    setSaving(true)
    try {
      const targetForm =
        form ??
        (await adminApi.createForm({
          assetCategory: schedule.assetCategory,
          department: department || schedule.department,
          periodType: schedule.periodType,
          quarter: schedule.quarter,
          semester: schedule.semester,
          year: schedule.year,
          academicYear: schedule.academicYear,
        }))
      await adminApi.addInspection(targetForm.id, {
        scheduleId: schedule.id,
        inspectorUserId: inspector.id,
        dateInspected: new Date(dateInspected).toISOString(),
        isOperational,
        remarks: remarks || null,
        actionsRecommendations: actionsRecommendations || null,
      })
      onMutation(
        form
          ? 'Inspection row added to the draft form.'
          : 'Draft form and inspection row created.',
      )
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      title={
        form
          ? `Add inspection · ${form.fileNumber ?? 'Draft form'}`
          : 'New draft form'
      }
      description="Choose an eligible schedule and active Inspector. The existing PM form workflow validates the batch and marks the schedule completed when the row is saved."
      onClose={onClose}
      busy={saving}
    >
      <form onSubmit={(event) => void submit(event)} className="space-y-4">
        <SearchablePicker
          label="Eligible schedule"
          value={schedule?.id}
          options={scheduleOptions}
          selectedOption={selectedScheduleOption}
          placeholder="Search asset or PM cycle"
          loading={scheduleQuery.isPending}
          loadingMore={scheduleQuery.isFetchingNextPage}
          hasMore={Boolean(scheduleQuery.hasNextPage)}
          onLoadMore={() => void scheduleQuery.fetchNextPage()}
          onSearchChange={setScheduleSearch}
          onChange={(_, option) => {
            setSchedule(option?.item ?? null)
            if (!form) setDepartment(option?.item?.department ?? '')
          }}
        />
        {!form && (
          <Field
            label="Department"
            value={department}
            onChange={setDepartment}
            required
          />
        )}
        <SearchablePicker
          label="Active Inspector"
          value={inspector?.id}
          options={inspectorOptions}
          selectedOption={selectedInspectorOption}
          placeholder="Search active Inspectors"
          loading={inspectorQuery.isPending}
          loadingMore={inspectorQuery.isFetchingNextPage}
          hasMore={Boolean(inspectorQuery.hasNextPage)}
          onLoadMore={() => void inspectorQuery.fetchNextPage()}
          onSearchChange={setInspectorSearch}
          onChange={(_, option) => setInspector(option?.item ?? null)}
        />
        <Field
          label="Date inspected"
          value={dateInspected}
          onChange={setDateInspected}
          type="datetime-local"
          required
        />
        <SelectField
          label="Condition"
          value={isOperational ? 'Operational' : 'Needs follow-up'}
          onChange={(value) => setIsOperational(value === 'Operational')}
          options={['Operational', 'Needs follow-up']}
        />
        <Field label="Remarks" value={remarks} onChange={setRemarks} />
        <Field
          label="Actions and recommendations"
          value={actionsRecommendations}
          onChange={setActionsRecommendations}
        />
        <ModalActions
          onClose={onClose}
          saving={saving}
          saveLabel={form ? 'Add inspection row' : 'Create draft form'}
          disabled={!schedule || !inspector || (!form && !department.trim())}
        />
      </form>
    </Modal>
  )
}

function InspectionEditor({
  row,
  onClose,
  onMutation,
  onError,
}: {
  row: Inspection
  onClose: () => void
  onMutation: (message: string) => void
  onError: (message: string) => void
}) {
  const formQuery = useQuery({
    queryKey: ['admin', 'form-detail', row.formId],
    queryFn: () => adminApi.getForm(row.formId!),
    retry: false,
    enabled: Boolean(row.formId),
  })
  const inspection = formQuery.data?.inspections.find(
    (candidate) => candidate.id === row.id,
  )
  const [editedDate, setDateInspected] = useState<string | null>(null)
  const [editedCondition, setIsOperational] = useState<boolean | null>(null)
  const [editedRemarks, setRemarks] = useState<string | null>(null)
  const [editedRecommendations, setActionsRecommendations] = useState<
    string | null
  >(null)
  const dateInspected =
    editedDate ?? localDateTime(inspection?.dateInspected ?? row.dateInspected)
  const isOperational =
    editedCondition ?? inspection?.isOperational ?? row.isOperational
  const remarks = editedRemarks ?? inspection?.remarks ?? ''
  const actionsRecommendations =
    editedRecommendations ?? inspection?.actionsRecommendations ?? ''
  const [saving, setSaving] = useState(false)
  useEffect(() => {
    if (formQuery.error) onError(messageFor(formQuery.error))
  }, [formQuery.error, onError])

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!inspection || !row.formId) return
    setSaving(true)
    try {
      await adminApi.updateInspection(row.formId, row.id, {
        expectedUpdatedAt: inspection.updatedAt,
        dateInspected: new Date(dateInspected).toISOString(),
        isOperational,
        remarks: remarks || null,
        actionsRecommendations: actionsRecommendations || null,
      })
      onMutation('Draft inspection row updated.')
      onClose()
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setSaving(false)
    }
  }

  return (
    <Modal
      title={`Edit draft inspection · ${row.assetCode}`}
      description="Draft rows retain the standard optimistic update and inspection field validation."
      onClose={onClose}
      busy={saving}
    >
      {formQuery.isPending ? (
        <p className="text-sm text-[var(--text-neutral)]">
          Loading inspection…
        </p>
      ) : !inspection ? (
        <Alert>
          This inspection row is no longer available in the draft form.
        </Alert>
      ) : (
        <form onSubmit={(event) => void submit(event)} className="space-y-4">
          <Field
            label="Date inspected"
            value={dateInspected}
            onChange={setDateInspected}
            type="datetime-local"
            required
          />
          <SelectField
            label="Condition"
            value={isOperational ? 'Operational' : 'Needs follow-up'}
            onChange={(value) => setIsOperational(value === 'Operational')}
            options={['Operational', 'Needs follow-up']}
          />
          <Field label="Remarks" value={remarks} onChange={setRemarks} />
          <Field
            label="Actions and recommendations"
            value={actionsRecommendations}
            onChange={setActionsRecommendations}
          />
          <ModalActions
            onClose={onClose}
            saving={saving}
            saveLabel="Save inspection"
          />
        </form>
      )}
    </Modal>
  )
}

function MaintenancePanel({
  revision,
  onError,
  onMutation,
}: {
  revision: number
  onError: (message: string) => void
  onMutation: (message: string) => void
}) {
  const [type, setType] = useState<'schedules' | 'inspections' | 'forms'>(
    'schedules',
  )
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [selected, setSelected] = useState<string[]>([])
  const [scheduleEditor, setScheduleEditor] = useState<Schedule | null>(null)
  const [formEditor, setFormEditor] = useState<MaintenanceForm | null>(null)
  const [entryForm, setEntryForm] = useState<
    MaintenanceForm | null | undefined
  >(undefined)
  const [inspectionEditor, setInspectionEditor] = useState<Inspection | null>(
    null,
  )
  const loader = useCallback(async (): Promise<
    Page<Schedule | Inspection | MaintenanceForm>
  > => {
    const params = { search, page, pageSize: 25 }
    if (type === 'schedules')
      return (await adminApi.schedules(params)) as Page<
        Schedule | Inspection | MaintenanceForm
      >
    if (type === 'inspections')
      return (await adminApi.inspections(params)) as Page<
        Schedule | Inspection | MaintenanceForm
      >
    return (await adminApi.forms(params)) as Page<
      Schedule | Inspection | MaintenanceForm
    >
  }, [type, search, page])
  const { result, loading } = usePage<Schedule | Inspection | MaintenanceForm>(
    ['admin', 'maintenance', type, search, page, revision],
    loader,
    onError,
  )
  const rows = result?.items ?? []
  const scheduleRows = rows.filter(
    (row): row is Schedule => 'scheduleDate' in row,
  )
  const inspectionRows = rows.filter(
    (row): row is Inspection => 'dateInspected' in row,
  )
  const formRows = rows.filter(
    (row): row is MaintenanceForm => 'inspectionCount' in row,
  )
  async function deleteRow(id: string) {
    if (
      !confirmAction(
        'Delete this maintenance record and reconcile any linked forms or schedules?',
      )
    )
      return
    try {
      let deleted: DeleteResult
      if (type === 'schedules') deleted = await adminApi.deleteSchedule(id)
      else if (type === 'inspections')
        deleted = await adminApi.deleteInspection(id)
      else deleted = await adminApi.deleteForm(id)
      onMutation(deleteSummary(deleted))
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  async function deleteSelected() {
    if (
      !selected.length ||
      !confirmAction(
        `Delete ${selected.length} selected ${type}? Linked maintenance data is reconciled.`,
      )
    )
      return
    try {
      const deleted =
        type === 'schedules'
          ? await adminApi.deleteSchedules(selected)
          : await adminApi.deleteInspections(selected)
      setSelected([])
      onMutation(deleteSummary(deleted))
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  async function updateFormState(row: MaintenanceForm) {
    const message =
      row.status === 'Acknowledged'
        ? 'Remove acknowledgement and return this form to Submitted?'
        : 'Return this Submitted form to Draft?'
    if (!confirmAction(message)) return
    try {
      if (row.status === 'Acknowledged') {
        await adminApi.deleteAcknowledgement(row.id)
        onMutation('Acknowledgement removed; the form is now Submitted.')
      } else {
        await adminApi.returnFormToDraft(row.id)
        onMutation('Form returned to Draft.')
      }
    } catch (reason) {
      onError(messageFor(reason))
    }
  }
  function editSchedule(row: Schedule) {
    setScheduleEditor(row)
  }
  function editForm(row: MaintenanceForm) {
    setFormEditor(row)
  }
  return (
    <div className="space-y-4">
      <div
        role="tablist"
        aria-label="Maintenance record types"
        className="flex gap-2"
      >
        {(['schedules', 'inspections', 'forms'] as const).map((value) => (
          <Button
            key={value}
            type="button"
            variant={type === value ? 'primary' : 'secondary'}
            aria-selected={type === value}
            role="tab"
            onClick={() => {
              setType(value)
              setPage(1)
              setSelected([])
            }}
          >
            {value.charAt(0).toUpperCase()}
            {value.slice(1)}
          </Button>
        ))}
      </div>
      {type === 'schedules' && (
        <CurrentYearScheduleGeneration
          onError={onError}
          onMutation={onMutation}
        />
      )}
      <Card className="space-y-4 p-0">
        <div className="flex flex-wrap items-end justify-between gap-3 p-4">
          <label className="block w-full max-w-md space-y-1.5 text-sm font-medium text-[var(--text-secondary)]">
            <span>Search {type}</span>
            <Input
              aria-label={`Search ${type}`}
              value={search}
              onChange={(event) => {
                setSearch(event.target.value)
                setPage(1)
                setSelected([])
              }}
              placeholder="Asset, department, file number"
            />
          </label>
          {type === 'forms' && (
            <Button type="button" onClick={() => setEntryForm(null)}>
              New draft form
            </Button>
          )}
          {type !== 'forms' && (
            <Button
              type="button"
              variant="secondary"
              disabled={!selected.length}
              onClick={() => void deleteSelected()}
            >
              Delete selected ({selected.length})
            </Button>
          )}
        </div>
        <TableFrame>
          <TableHead>
            {type !== 'forms' && (
              <Th>
                <input
                  aria-label={`Select all ${type} on this page`}
                  type="checkbox"
                  checked={Boolean(
                    rows.length && selected.length === rows.length,
                  )}
                  onChange={(event) =>
                    setSelected(
                      event.target.checked ? rows.map((row) => row.id) : [],
                    )
                  }
                />
              </Th>
            )}
            {type === 'schedules' ? (
              <>
                <Th>Asset / cycle</Th>
                <Th>Date</Th>
                <Th>Assignment</Th>
                <Th>Status</Th>
                <Th>Actions</Th>
              </>
            ) : null}
            {type === 'inspections' ? (
              <>
                <Th>Asset</Th>
                <Th>Inspected</Th>
                <Th>Inspector</Th>
                <Th>Form status</Th>
                <Th>Actions</Th>
              </>
            ) : null}
            {type === 'forms' ? (
              <>
                <Th>Form</Th>
                <Th>Category / department</Th>
                <Th>Rows</Th>
                <Th>Status</Th>
                <Th>Acknowledgement</Th>
                <Th>Actions</Th>
              </>
            ) : null}
          </TableHead>
          <tbody>
            {loading && (
              <tr>
                <Td colSpan={type === 'forms' ? 6 : 6}>Loading {type}…</Td>
              </tr>
            )}
            {!loading && rows.length === 0 && (
              <tr>
                <Td colSpan={type === 'forms' ? 6 : 6}>No matching {type}.</Td>
              </tr>
            )}
            {type === 'schedules' &&
              scheduleRows.map((row) => (
                <tr key={row.id}>
                  <Td>
                    <input
                      aria-label={`Select schedule ${row.assetCode} ${row.pmCycle}`}
                      type="checkbox"
                      checked={selected.includes(row.id)}
                      onChange={(event) =>
                        setSelected((current) =>
                          event.target.checked
                            ? [...current, row.id]
                            : current.filter((id) => id !== row.id),
                        )
                      }
                    />
                  </Td>
                  <Td>
                    <strong>{row.assetCode}</strong>
                    <div className="text-xs text-[var(--text-neutral)]">
                      {row.pmCycle} · {row.assetCategory}
                    </div>
                  </Td>
                  <Td>{new Date(row.scheduleDate).toLocaleDateString()}</Td>
                  <Td>
                    {row.inspectorDisplayName ?? 'Unassigned'}
                    <div className="text-xs text-[var(--text-neutral)]">
                      {row.supervisorDisplayName ?? 'No supervisor'}
                    </div>
                  </Td>
                  <Td>
                    <Badge
                      variant={
                        row.status === 'Completed' ? 'success' : 'neutral'
                      }
                    >
                      {row.status}
                    </Badge>
                  </Td>
                  <Td>
                    <div className="flex gap-2">
                      <Button
                        type="button"
                        variant="secondary"
                        className="min-h-8 px-2.5"
                        onClick={() => editSchedule(row)}
                      >
                        Edit
                      </Button>
                      <Button
                        type="button"
                        variant="secondary"
                        className="min-h-8 px-2.5 text-[var(--error)]"
                        onClick={() => void deleteRow(row.id)}
                      >
                        Delete
                      </Button>
                    </div>
                  </Td>
                </tr>
              ))}
            {type === 'inspections' &&
              inspectionRows.map((row) => (
                <tr key={row.id}>
                  <Td>
                    <input
                      aria-label={`Select inspection ${row.assetCode}`}
                      type="checkbox"
                      checked={selected.includes(row.id)}
                      onChange={(event) =>
                        setSelected((current) =>
                          event.target.checked
                            ? [...current, row.id]
                            : current.filter((id) => id !== row.id),
                        )
                      }
                    />
                  </Td>
                  <Td>
                    <strong>{row.assetCode}</strong>
                    <div className="text-xs text-[var(--text-neutral)]">
                      {row.isOperational ? 'Operational' : 'Needs follow-up'}
                    </div>
                  </Td>
                  <Td>{new Date(row.dateInspected).toLocaleDateString()}</Td>
                  <Td>{row.inspectorDisplayName ?? '—'}</Td>
                  <Td>
                    {row.formStatus ?? 'No form'}
                    {row.formFileNumber ? (
                      <div className="text-xs text-[var(--text-neutral)]">
                        {row.formFileNumber}
                      </div>
                    ) : null}
                  </Td>
                  <Td>
                    <div className="flex gap-2">
                      {row.formStatus === 'Draft' && row.formId && (
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => setInspectionEditor(row)}
                        >
                          Edit
                        </Button>
                      )}
                      <Button
                        type="button"
                        variant="secondary"
                        className="min-h-8 px-2.5 text-[var(--error)]"
                        onClick={() => void deleteRow(row.id)}
                      >
                        Delete
                      </Button>
                    </div>
                  </Td>
                </tr>
              ))}
            {type === 'forms' &&
              formRows.map((row) => (
                <tr key={row.id}>
                  <Td>
                    <strong>{row.fileNumber ?? 'Unnumbered'}</strong>
                    <div className="text-xs text-[var(--text-neutral)]">
                      {row.pmCycle ?? 'No cycle'}
                    </div>
                  </Td>
                  <Td>
                    {row.assetCategory}
                    <div className="text-xs text-[var(--text-neutral)]">
                      {row.department ?? '—'}
                    </div>
                  </Td>
                  <Td>{row.inspectionCount}</Td>
                  <Td>
                    <Badge
                      variant={
                        row.status === 'Acknowledged' ? 'success' : 'neutral'
                      }
                    >
                      {row.status}
                    </Badge>
                  </Td>
                  <Td>
                    {row.hasAcknowledgement ? 'Signed acknowledgement' : '—'}
                  </Td>
                  <Td>
                    <div className="flex flex-wrap gap-2">
                      {row.status === 'Draft' && (
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => setEntryForm(row)}
                        >
                          Add inspection
                        </Button>
                      )}
                      {row.status !== 'Acknowledged' && (
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => editForm(row)}
                        >
                          Edit
                        </Button>
                      )}
                      {row.status === 'Acknowledged' && (
                        <span className="self-center text-xs text-[var(--text-neutral)]">
                          Remove acknowledgement before editing
                        </span>
                      )}
                      {row.status !== 'Draft' && (
                        <Button
                          type="button"
                          variant="secondary"
                          className="min-h-8 px-2.5"
                          onClick={() => void updateFormState(row)}
                        >
                          {row.status === 'Acknowledged'
                            ? 'Remove acknowledgement'
                            : 'Return to Draft'}
                        </Button>
                      )}
                      <Button
                        type="button"
                        variant="secondary"
                        className="min-h-8 px-2.5 text-[var(--error)]"
                        onClick={() => void deleteRow(row.id)}
                      >
                        Delete
                      </Button>
                    </div>
                  </Td>
                </tr>
              ))}
          </tbody>
        </TableFrame>
        {result && (
          <Pager
            page={page}
            totalCount={result.totalCount}
            onPage={(next) => {
              setPage(next)
              setSelected([])
            }}
          />
        )}
      </Card>
      {scheduleEditor && (
        <ScheduleEditor
          row={scheduleEditor}
          onClose={() => setScheduleEditor(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
      {formEditor && (
        <FormEditor
          row={formEditor}
          onClose={() => setFormEditor(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
      {entryForm !== undefined && (
        <InspectionEntryModal
          form={entryForm}
          onClose={() => setEntryForm(undefined)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
      {inspectionEditor && (
        <InspectionEditor
          row={inspectionEditor}
          onClose={() => setInspectionEditor(null)}
          onMutation={onMutation}
          onError={onError}
        />
      )}
    </div>
  )
}

function DemoPanel({
  onError,
  onMutation,
}: {
  onError: (message: string) => void
  onMutation: (message: string) => void
}) {
  const [busy, setBusy] = useState(false)
  const [lastResult, setLastResult] = useState<ValidationDatasetResult | null>(
    null,
  )
  async function run(
    action: () => Promise<DeleteResult | ValidationDatasetResult>,
    message: string,
  ) {
    setBusy(true)
    try {
      const result = await action()
      if ('assetsSeeded' in result) setLastResult(result)
      else setLastResult(null)
      onMutation(message)
    } catch (reason) {
      onError(messageFor(reason))
    } finally {
      setBusy(false)
    }
  }
  return (
    <div className="grid gap-4 xl:grid-cols-2">
      <Card className="space-y-4">
        <div>
          <h2 className="font-semibold text-[var(--text-primary)]">
            Validation dataset
          </h2>
          <p className="mt-1 text-sm text-[var(--text-neutral)]">
            Uses the existing synthetic GSD validation fixture. An active GSD
            head, Inspector, and Supervisor account must be ready first.
          </p>
        </div>
        <div className="rounded-md bg-[var(--surface-muted)] p-3 text-sm text-[var(--text-secondary)]">
          <strong>Load</strong> works only when assets and maintenance tables
          are empty. <strong>Reset and reseed</strong> clears all assets and
          maintenance records, then loads the fixture in one transaction.
        </div>
        <div className="flex flex-wrap gap-2">
          <Button
            type="button"
            disabled={busy}
            onClick={() =>
              void run(
                adminApi.loadValidationDataset,
                'Validation dataset loaded.',
              )
            }
          >
            {busy ? 'Working…' : 'Load validation data'}
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={busy}
            onClick={() => {
              if (
                confirmAction(
                  'Delete all assets and maintenance records, then load the validation dataset? This replaces the full demonstration dataset.',
                )
              )
                void run(
                  adminApi.resetValidationDataset,
                  'Existing data replaced with the validation dataset.',
                )
            }}
          >
            {busy ? 'Working…' : 'Reset and reseed'}
          </Button>
        </div>
        {lastResult && (
          <div
            aria-live="polite"
            className="border-t border-[var(--border-soft)] pt-3 text-sm text-[var(--text-secondary)]"
          >
            <p>
              Seeded {lastResult.assetsSeeded} assets,{' '}
              {lastResult.schedulesSeeded.toLocaleString()} schedules,{' '}
              {lastResult.inspectionsSeeded} inspections, and{' '}
              {lastResult.formsSeeded} forms.
            </p>
            {lastResult.deleted.photoFileCleanupFailures > 0 && (
              <p className="mt-1 text-[var(--error)]">
                {lastResult.deleted.photoFileCleanupFailures} photo files could
                not be removed and need cleanup.
              </p>
            )}
          </div>
        )}
      </Card>
      <Card className="space-y-4">
        <div>
          <h2 className="font-semibold text-[var(--text-primary)]">
            Clear demonstration data
          </h2>
          <p className="mt-1 text-sm text-[var(--text-neutral)]">
            These actions remove rows and reconcile related records. The app
            accounts remain.
          </p>
        </div>
        <div className="flex flex-col gap-3">
          <Button
            type="button"
            variant="secondary"
            disabled={busy}
            onClick={() => {
              if (
                confirmAction(
                  'Clear all schedules, inspections, forms, acknowledgements, and linked maintenance data? Assets will remain.',
                )
              )
                void run(adminApi.clearMaintenance, 'Maintenance data cleared.')
            }}
          >
            Clear maintenance records
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={busy}
            className="text-[var(--error)]"
            onClick={() => {
              if (
                confirmAction(
                  'Clear every asset and all linked maintenance data? This cannot be undone.',
                )
              )
                void run(
                  adminApi.clearAssetsAndMaintenance,
                  'Assets and maintenance records cleared.',
                )
            }}
          >
            Clear assets and maintenance
          </Button>
        </div>
        <p className="text-xs text-[var(--text-neutral)]">
          Photo files are deleted after the database transaction. Any cleanup
          failures are reported after the action completes.
        </p>
      </Card>
    </div>
  )
}
