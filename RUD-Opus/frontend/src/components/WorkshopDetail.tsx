import { formatTimeRange, seatsLabel } from '../format'
import type { ParticipantDto, RegistrationDto, WorkshopDto } from '../types'
import { RegisterForm } from './RegisterForm'
import { RegistrationList } from './RegistrationList'

interface Props {
  workshop: WorkshopDto
  registrations: RegistrationDto[] | undefined
  registrationsLoading?: boolean
  registrationsError?: string
  onRetryRegistrations?: () => void
  participants: ParticipantDto[] | undefined
  participantsError?: string
  submitting?: boolean
  cancellingId?: number | null
  onRegister: (participantId: number) => void
  onCancel: (registrationId: number) => void
}

export function WorkshopDetail(p: Props) {
  const w = p.workshop
  return (
    <section aria-labelledby="detail-h" className="card">
      <h2 id="detail-h">{w.title}</h2>
      <dl className="facts">
        <div>
          <dt>When</dt>
          <dd>{formatTimeRange(w.startTime, w.endTime)}</dd>
        </div>
        <div>
          <dt>Seats</dt>
          <dd>{seatsLabel(w)}</dd>
        </div>
        <div>
          <dt>Confirmed</dt>
          <dd>
            {w.confirmedCount} of {w.capacity}
          </dd>
        </div>
        <div>
          <dt>Waitlist</dt>
          <dd>{w.waitlistedCount}</dd>
        </div>
      </dl>
      <RegisterForm
        workshop={w}
        participants={p.participants}
        participantsError={p.participantsError}
        submitting={p.submitting}
        onSubmit={p.onRegister}
      />
      <h3>Registrations</h3>
      <RegistrationList
        registrations={p.registrations}
        loading={p.registrationsLoading}
        error={p.registrationsError}
        onRetry={p.onRetryRegistrations}
        onCancel={p.onCancel}
        cancellingId={p.cancellingId}
      />
    </section>
  )
}
