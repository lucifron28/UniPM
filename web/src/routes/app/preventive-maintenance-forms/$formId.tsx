import { createFileRoute, Outlet } from '@tanstack/react-router'

export const Route = createFileRoute(
  '/app/preventive-maintenance-forms/$formId',
)({
  component: FormDetailLayout,
})

function FormDetailLayout() {
  return <Outlet />
}
