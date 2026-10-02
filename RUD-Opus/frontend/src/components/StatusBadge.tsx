import type { RegistrationStatus } from '../types'

const icons: Record<RegistrationStatus, React.ReactNode> = {
  // check in circle
  Confirmed: (
    <>
      <circle cx="8" cy="8" r="6.5" />
      <path d="M5 8.3l2 2 4-4.3" />
    </>
  ),
  // clock
  Waitlisted: (
    <>
      <circle cx="8" cy="8" r="6.5" />
      <path d="M8 4.5V8l2.3 1.5" />
    </>
  ),
  // cross
  Cancelled: <path d="M4 4l8 8M12 4l-8 8" />,
}

export function StatusBadge({ status }: { status: RegistrationStatus }) {
  return (
    <span className={`badge badge--${status.toLowerCase()}`}>
      <svg viewBox="0 0 16 16" width="14" height="14" aria-hidden="true" focusable="false">
        {icons[status]}
      </svg>
      {status}
    </span>
  )
}
