import { formatDateTime } from '../format'
import type { RegistrationDto } from '../types'
import { ConfirmCancelButton } from './ConfirmCancelButton'
import { StatePanel } from './StatePanel'
import { StatusBadge } from './StatusBadge'

interface Props {
  registrations: RegistrationDto[] | undefined
  loading?: boolean
  error?: string
  onRetry?: () => void
  onCancel: (registrationId: number) => void
  cancellingId?: number | null
}

function Row({
  r,
  onCancel,
  cancelling,
}: {
  r: RegistrationDto
  onCancel?: () => void
  cancelling: boolean
}) {
  return (
    <li className="reg">
      <div className="reg__main">
        <span className="reg__name">
          {r.status === 'Waitlisted' && r.waitlistPosition != null && (
            <span className="reg__pos" aria-label={`Waitlist position ${r.waitlistPosition}`}>
              #{r.waitlistPosition}
            </span>
          )}
          {r.participantName}
        </span>
        <span className="reg__email">{r.participantEmail}</span>
        <span className="reg__meta">Registered {formatDateTime(r.createdAt)}</span>
      </div>
      <StatusBadge status={r.status} />
      {onCancel && (
        <ConfirmCancelButton subject={r.participantName} busy={cancelling} onConfirm={onCancel} />
      )}
    </li>
  )
}

export function RegistrationList({ registrations, loading, error, onRetry, onCancel, cancellingId }: Props) {
  if (error && !registrations) return <StatePanel variant="error" message={error} onRetry={onRetry} />
  if (loading && !registrations) return <StatePanel variant="loading" message="Loading registrations…" />
  if (!registrations) return null

  const confirmed = registrations.filter((r) => r.status === 'Confirmed')
  const waitlist = registrations
    .filter((r) => r.status === 'Waitlisted')
    .sort((a, b) => (a.waitlistPosition ?? 0) - (b.waitlistPosition ?? 0))
  const cancelled = registrations.filter((r) => r.status === 'Cancelled')

  const section = (id: string, title: string, items: RegistrationDto[], empty: string, canCancel: boolean) => (
    <section aria-labelledby={id}>
      <h4 id={id}>
        {title} <span className="count">({items.length})</span>
      </h4>
      {items.length === 0 ? (
        <p className="muted">{empty}</p>
      ) : (
        <ul className="reg-list">
          {items.map((r) => (
            <Row
              key={r.id}
              r={r}
              cancelling={cancellingId === r.id}
              onCancel={canCancel ? () => onCancel(r.id) : undefined}
            />
          ))}
        </ul>
      )}
    </section>
  )

  return (
    <div className="regs" aria-busy={loading || undefined}>
      {section('regs-confirmed', 'Confirmed', confirmed, 'No confirmed registrations yet.', true)}
      {section('regs-waitlist', 'Waitlist (first in line first)', waitlist, 'Nobody is on the waitlist.', true)}
      {cancelled.length > 0 && (
        <details className="cancelled">
          <summary>Cancelled ({cancelled.length})</summary>
          <ul className="reg-list">
            {cancelled.map((r) => (
              <Row key={r.id} r={r} cancelling={false} />
            ))}
          </ul>
        </details>
      )}
    </div>
  )
}
