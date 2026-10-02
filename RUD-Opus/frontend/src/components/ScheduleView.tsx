import { useId } from 'react'
import { formatTimeRange } from '../format'
import type { ParticipantDto, ScheduleDto } from '../types'
import { StatePanel } from './StatePanel'
import { StatusBadge } from './StatusBadge'

interface Props {
  participants: ParticipantDto[] | undefined
  selectedId: number | null
  onSelect: (id: number | null) => void
  schedule: ScheduleDto | undefined
  loading?: boolean
  error?: string
  onRetry?: () => void
}

export function ScheduleView({ participants, selectedId, onSelect, schedule, loading, error, onRetry }: Props) {
  const selectId = useId()
  let body: React.ReactNode = null
  if (selectedId === null) body = <p className="muted">Select a participant to see their schedule.</p>
  else if (error && !schedule) body = <StatePanel variant="error" message={error} onRetry={onRetry} />
  else if (loading && !schedule) body = <StatePanel variant="loading" message="Loading schedule…" />
  else if (schedule && schedule.registrations.length === 0)
    body = <StatePanel variant="empty" message={`${schedule.participant.name} has no registrations.`} />
  else if (schedule)
    body = (
      <ul className="reg-list">
        {schedule.registrations.map((r) => (
          <li key={r.registrationId} className="reg">
            <div className="reg__main">
              <span className="reg__name">{r.workshop.title}</span>
              <span className="reg__email">{formatTimeRange(r.workshop.startTime, r.workshop.endTime)}</span>
              {r.status === 'Waitlisted' && r.waitlistPosition != null && (
                <span className="reg__meta">Waitlist position #{r.waitlistPosition}</span>
              )}
            </div>
            <StatusBadge status={r.status} />
          </li>
        ))}
      </ul>
    )

  return (
    <section aria-labelledby="schedule-h" className="card">
      <h2 id="schedule-h">Participant schedule</h2>
      <div className="field">
        <label htmlFor={selectId}>Show schedule for</label>
        <select
          id={selectId}
          value={selectedId ?? ''}
          disabled={!participants}
          onChange={(e) => onSelect(e.target.value ? Number(e.target.value) : null)}
        >
          <option value="">{participants ? 'Select a participant…' : 'Loading participants…'}</option>
          {participants?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </div>
      <div aria-busy={loading || undefined}>{body}</div>
    </section>
  )
}
