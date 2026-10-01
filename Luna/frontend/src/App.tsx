import { useCallback, useEffect, useMemo, useState } from 'react'
import { api, type Participant, type Registration, type ScheduleItem, type Workshop } from './api'

function dateTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { weekday: 'short', month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' }).format(new Date(value))
}

function timeRange(start: string, end: string) {
  return `${dateTime(start)} – ${new Intl.DateTimeFormat(undefined, { hour: 'numeric', minute: '2-digit' }).format(new Date(end))}`
}

function StatusPill({ status }: { status: Registration['status'] }) {
  return <span className={`status-pill ${status.toLowerCase()}`}>{status}</span>
}

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([])
  const [participants, setParticipants] = useState<Participant[]>([])
  const [registrations, setRegistrations] = useState<Registration[]>([])
  const [selectedWorkshop, setSelectedWorkshop] = useState<number | null>(null)
  const [selectedParticipant, setSelectedParticipant] = useState('')
  const [scheduleParticipant, setScheduleParticipant] = useState('')
  const [schedule, setSchedule] = useState<ScheduleItem[]>([])
  const [message, setMessage] = useState('')
  const [error, setError] = useState('')
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)

  const selected = workshops.find((workshop) => workshop.id === selectedWorkshop) ?? null
  const confirmed = useMemo(() => registrations.filter((item) => item.status === 'Confirmed'), [registrations])
  const waitlisted = useMemo(() => registrations.filter((item) => item.status === 'Waitlisted'), [registrations])
  const cancelled = useMemo(() => registrations.filter((item) => item.status === 'Cancelled'), [registrations])

  const loadWorkshops = useCallback(async () => {
    const items = await api.workshops()
    setWorkshops(items)
    setSelectedWorkshop((current) => current && items.some((item) => item.id === current) ? current : items[0]?.id ?? null)
  }, [])

  const loadRegistrations = useCallback(async (workshopId: number) => {
    setRegistrations(await api.registrations(workshopId))
  }, [])

  useEffect(() => {
    let alive = true
    Promise.all([api.workshops(), api.participants()])
      .then(([workshopItems, participantItems]) => {
        if (!alive) return
        setWorkshops(workshopItems)
        setParticipants(participantItems)
        setSelectedWorkshop(workshopItems[0]?.id ?? null)
        setSelectedParticipant(participantItems[0] ? String(participantItems[0].id) : '')
        setScheduleParticipant(participantItems[0] ? String(participantItems[0].id) : '')
        setLoading(false)
      })
      .catch((reason: unknown) => {
        if (!alive) return
        setError(reason instanceof Error ? reason.message : 'Could not connect to SeatFlow.')
        setLoading(false)
      })
    return () => { alive = false }
  }, [])

  useEffect(() => {
    if (selectedWorkshop === null) return
    api.registrations(selectedWorkshop).then(setRegistrations).catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Could not load registrations.'))
  }, [selectedWorkshop])

  useEffect(() => {
    if (!scheduleParticipant) { setSchedule([]); return }
    api.schedule(Number(scheduleParticipant)).then(setSchedule).catch((reason: unknown) => setError(reason instanceof Error ? reason.message : 'Could not load schedule.'))
  }, [scheduleParticipant])

  async function handleRegister(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (selectedWorkshop === null || !selectedParticipant) return
    setBusy(true)
    setError('')
    setMessage('')
    try {
      const registration = await api.register(selectedWorkshop, Number(selectedParticipant))
      setMessage(`${registration.participantName} is ${registration.status.toLowerCase()} for ${selected?.title ?? 'this workshop'}.`)
      await Promise.all([loadRegistrations(selectedWorkshop), loadWorkshops()])
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Registration failed.')
    } finally {
      setBusy(false)
    }
  }

  async function handleCancel(registration: Registration) {
    setBusy(true)
    setError('')
    setMessage('')
    try {
      await api.cancel(registration.id)
      setMessage(`${registration.participantName}'s registration was cancelled.`)
      if (selectedWorkshop !== null) await Promise.all([loadRegistrations(selectedWorkshop), loadWorkshops()])
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : 'Cancellation failed.')
    } finally {
      setBusy(false)
    }
  }

  if (loading) return <main className="loading">Loading SeatFlow…</main>

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand-mark" aria-hidden="true">S</div>
        <div><h1>SeatFlow</h1><p>Workshop registration desk</p></div>
        <span className="live-indicator">Conference workspace</span>
      </header>

      {(error || message) && <div className={`notice ${error ? 'notice-error' : 'notice-success'}`} role="status">
        <span>{error || message}</span><button className="notice-close" onClick={() => { setError(''); setMessage('') }} aria-label="Dismiss message">×</button>
      </div>}

      <main className="layout">
        <aside className="workshop-column">
          <div className="section-heading"><div><p className="eyebrow">CONFERENCE PROGRAM</p><h2>Workshops</h2></div><span className="count-badge">{workshops.length}</span></div>
          <div className="workshop-list">
            {workshops.map((workshop) => (
              <button className={`workshop-card ${selectedWorkshop === workshop.id ? 'selected' : ''}`} key={workshop.id} onClick={() => setSelectedWorkshop(workshop.id)}>
                <span className="workshop-card-top"><span className="workshop-dot" /> <span>{workshop.remainingCapacity === 0 ? 'Full' : `${workshop.remainingCapacity} seat${workshop.remainingCapacity === 1 ? '' : 's'} left`}</span></span>
                <strong>{workshop.title}</strong>
                <span className="workshop-time">{timeRange(workshop.startTime, workshop.endTime)}</span>
                <span className="capacity-track"><span style={{ width: `${Math.min(100, (workshop.confirmedCount / workshop.capacity) * 100)}%` }} /></span>
                <span className="capacity-caption">{workshop.confirmedCount} of {workshop.capacity} confirmed</span>
              </button>
            ))}
            {workshops.length === 0 && <p className="empty-state">No workshops yet.</p>}
          </div>
        </aside>

        <section className="detail-column">
          {selected ? <>
            <section className="panel workshop-summary">
              <div className="summary-label">SELECTED WORKSHOP</div>
              <div className="summary-line"><div><h2>{selected.title}</h2><p>{timeRange(selected.startTime, selected.endTime)}</p></div><span className={`seat-summary ${selected.remainingCapacity === 0 ? 'full' : ''}`}>{selected.remainingCapacity} <small>seats<br />available</small></span></div>
            </section>

            <section className="panel register-panel">
              <div className="panel-heading"><div><p className="eyebrow">ADD A PARTICIPANT</p><h3>New registration</h3></div><span className="step-icon">＋</span></div>
              <form className="register-form" onSubmit={handleRegister}>
                <label htmlFor="participant-select">Participant</label>
                <div className="form-row">
                  <select id="participant-select" value={selectedParticipant} onChange={(event) => setSelectedParticipant(event.target.value)} required>
                    {participants.map((participant) => <option value={participant.id} key={participant.id}>{participant.name} · {participant.email}</option>)}
                  </select>
                  <button className="primary-button" type="submit" disabled={busy || !participants.length}>{busy ? 'Working…' : 'Register'}</button>
                </div>
                {!participants.length && <p className="helper-text">There are no participants available.</p>}
              </form>
            </section>

            <section className="panel roster-panel">
              <div className="panel-heading roster-heading"><div><p className="eyebrow">LIVE ROSTER</p><h3>Registrations</h3></div><span className="roster-total">{registrations.length} total</span></div>
              <Roster title="Confirmed" items={confirmed} busy={busy} onCancel={handleCancel} />
              <Roster title="Waitlist" items={waitlisted} busy={busy} onCancel={handleCancel} ordered />
              {cancelled.length > 0 && <Roster title="Cancelled" items={cancelled} busy={busy} onCancel={handleCancel} />}
              {registrations.length === 0 && <p className="empty-state roster-empty">No registrations for this workshop yet.</p>}
            </section>
          </> : <section className="panel empty-panel">Choose a workshop to see its registrations.</section>}
        </section>

        <aside className="schedule-column">
          <section className="panel schedule-panel">
            <div className="panel-heading"><div><p className="eyebrow">MY VIEW</p><h3>Participant schedule</h3></div><span className="calendar-icon" aria-hidden="true">▦</span></div>
            <label className="schedule-label" htmlFor="schedule-participant">Show schedule for</label>
            <select id="schedule-participant" value={scheduleParticipant} onChange={(event) => setScheduleParticipant(event.target.value)}>
              {participants.map((participant) => <option value={participant.id} key={participant.id}>{participant.name}</option>)}
            </select>
            <div className="schedule-list">
              {schedule.map((item) => <article className="schedule-item" key={item.registrationId}>
                <span className="schedule-time">{dateTime(item.startTime)}</span><strong>{item.workshopTitle}</strong>
                <span>{timeRange(item.startTime, item.endTime)}</span><StatusPill status="Confirmed" />
              </article>)}
              {schedule.length === 0 && <p className="empty-state schedule-empty">No confirmed workshops on this schedule.</p>}
            </div>
          </section>
          <div className="schedule-note"><span aria-hidden="true">ⓘ</span> A participant can only hold confirmed registrations for workshops that do not overlap.</div>
        </aside>
      </main>
      <footer>SeatFlow <span>·</span> Registration management</footer>
    </div>
  )
}

function Roster({ title, items, busy, onCancel, ordered = false }: {
  title: string
  items: Registration[]
  busy: boolean
  onCancel: (registration: Registration) => void
  ordered?: boolean
}) {
  return <div className="roster-group">
    <div className="roster-subheading"><h4>{title}{ordered && <span className="fifo-tag">FIFO</span>}</h4><span>{items.length}</span></div>
    {items.length ? <ul className="registration-list">{items.map((registration, index) => <li className="registration-row" key={registration.id}>
      <span className={`avatar avatar-${index % 5}`}>{registration.participantName.split(' ').map((part) => part[0]).slice(0, 2).join('').toUpperCase()}</span>
      <span className="participant-info"><strong>{registration.participantName}</strong><small>{registration.participantEmail}</small></span>
      <StatusPill status={registration.status} />
      {registration.status !== 'Cancelled' && <button className="cancel-button" disabled={busy} onClick={() => onCancel(registration)}>Cancel</button>}
    </li>)}</ul> : <p className="sub-empty">No {title.toLowerCase()} registrations.</p>}
  </div>
}
