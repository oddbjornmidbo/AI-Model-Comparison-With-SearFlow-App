import { useId, useState } from 'react'
import type { ParticipantDto, WorkshopDto } from '../types'

interface Props {
  workshop: Pick<WorkshopDto, 'title' | 'remainingSeats'>
  participants: ParticipantDto[] | undefined
  participantsError?: string
  submitting?: boolean
  onSubmit: (participantId: number) => void
}

export function RegisterForm({ workshop, participants, participantsError, submitting, onSubmit }: Props) {
  const selectId = useId()
  const errId = useId()
  const [value, setValue] = useState('')
  const [validation, setValidation] = useState<string>()
  const full = workshop.remainingSeats <= 0

  return (
    <form
      className="register"
      noValidate
      onSubmit={(e) => {
        e.preventDefault()
        if (!value) {
          setValidation('Choose a participant to register.')
          return
        }
        setValidation(undefined)
        onSubmit(Number(value))
      }}
    >
      <h3 className="register__title">Register a participant</h3>
      <div className="field">
        <label htmlFor={selectId}>Participant</label>
        <select
          id={selectId}
          value={value}
          disabled={submitting || !participants}
          aria-invalid={validation ? true : undefined}
          aria-describedby={validation ? errId : undefined}
          onChange={(e) => {
            setValue(e.target.value)
            setValidation(undefined)
          }}
        >
          <option value="">{participants ? 'Select a participant…' : 'Loading participants…'}</option>
          {participants?.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} ({p.email})
            </option>
          ))}
        </select>
        {validation && (
          <p id={errId} className="field__error">
            {validation}
          </p>
        )}
        {participantsError && <p className="field__error">{participantsError}</p>}
      </div>
      <button type="submit" className="btn btn--primary" disabled={submitting}>
        {submitting ? 'Registering…' : full ? 'Join waitlist' : 'Register'}
      </button>
      {full && !submitting && (
        <p className="muted">This workshop is full. New registrations are added to the waitlist.</p>
      )}
    </form>
  )
}
