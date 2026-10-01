import { useCallback, useEffect, useState, type FormEvent } from 'react'
import {
  api,
  type NotificationEntry,
  type Participant,
  type Registration,
  type RegistrationStatus,
  type Schedule,
  type Workshop,
} from './api'

const timeFmt = new Intl.DateTimeFormat(undefined, { dateStyle: 'short', timeStyle: 'short' })
const hourFmt = new Intl.DateTimeFormat(undefined, { timeStyle: 'short' })

function formatRange(start: string, end: string) {
  return `${timeFmt.format(new Date(start))} – ${hourFmt.format(new Date(end))}`
}

function StatusBadge({ status }: { status: RegistrationStatus }) {
  return <span className={`badge badge-${status.toLowerCase()}`}>{status}</span>
}

type Message = { kind: 'ok' | 'error'; text: string }

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([])
  const [participants, setParticipants] = useState<Participant[]>([])
  const [selectedWorkshopId, setSelectedWorkshopId] = useState<number | null>(null)
  const [registrations, setRegistrations] = useState<Registration[]>([])
  const [scheduleParticipantId, setScheduleParticipantId] = useState<number | null>(null)
  const [schedule, setSchedule] = useState<Schedule | null>(null)
  const [notifications, setNotifications] = useState<NotificationEntry[]>([])
  const [message, setMessage] = useState<Message | null>(null)

  const refresh = useCallback(async () => {
    try {
      const [ws, notes] = await Promise.all([api.workshops(), api.notifications()])
      setWorkshops(ws)
      setNotifications(notes)
      setRegistrations(selectedWorkshopId ? await api.registrations(selectedWorkshopId) : [])
      setSchedule(scheduleParticipantId ? await api.schedule(scheduleParticipantId) : null)
    } catch (e) {
      setMessage({ kind: 'error', text: `Could not load data: ${(e as Error).message}` })
    }
  }, [selectedWorkshopId, scheduleParticipantId])

  useEffect(() => {
    api.participants().then(setParticipants, (e: Error) => setMessage({ kind: 'error', text: e.message }))
  }, [])

  useEffect(() => {
    void refresh()
  }, [refresh])

  /** Runs a mutation, shows its outcome, and reloads everything that might have changed. */
  async function act(action: () => Promise<string>) {
    try {
      setMessage({ kind: 'ok', text: await action() })
    } catch (e) {
      setMessage({ kind: 'error', text: (e as Error).message })
    }
    await refresh()
  }

  const selected = workshops.find((w) => w.id === selectedWorkshopId) ?? null

  return (
    <div className="app">
      <header>
        <h1>SeatFlow</h1>
        <span className="muted">Workshop registrations</span>
      </header>

      {message && (
        <div className={`message message-${message.kind}`} onClick={() => setMessage(null)}>
          {message.text} <span className="muted">(click to dismiss)</span>
        </div>
      )}

      <div className="columns">
        <section>
          <h2>Workshops</h2>
          <WorkshopTable workshops={workshops} selectedId={selectedWorkshopId} onSelect={setSelectedWorkshopId} />
          <CreateWorkshopForm
            onCreate={(body) =>
              act(async () => {
                const w = await api.createWorkshop(body)
                setSelectedWorkshopId(w.id)
                return `Created workshop "${w.title}".`
              })
            }
          />
        </section>

        <section>
          {selected ? (
            <WorkshopDetail
              workshop={selected}
              registrations={registrations}
              participants={participants}
              onRegister={(participantId) =>
                act(async () => {
                  // One key per user action; a retried request with the same key cannot create a duplicate.
                  const r = await api.register(selected.id, participantId, crypto.randomUUID())
                  return r.status === 'Confirmed'
                    ? `${r.participantName} is confirmed for "${selected.title}".`
                    : `"${selected.title}" is full — ${r.participantName} is waitlisted (#${r.waitlistPosition}).`
                })
              }
              onCancel={(r) => act(() => cancelRegistration(r.id, r.participantName))}
            />
          ) : (
            <p className="muted">Select a workshop to see its registrations and waitlist.</p>
          )}
        </section>
      </div>

      <div className="columns">
        <section>
          <h2>Participant schedule</h2>
          <select
            value={scheduleParticipantId ?? ''}
            onChange={(e) => setScheduleParticipantId(e.target.value ? Number(e.target.value) : null)}
          >
            <option value="">Choose participant…</option>
            {participants.map((p) => (
              <option key={p.id} value={p.id}>
                {p.name}
              </option>
            ))}
          </select>
          {schedule && (
            <ScheduleView schedule={schedule} onCancel={(id) => act(() => cancelRegistration(id, schedule.participant.name))} />
          )}
        </section>

        <section>
          <h2>Notifications (dev log)</h2>
          {notifications.length === 0 ? (
            <p className="muted">No notifications sent yet.</p>
          ) : (
            <ul className="notifications">
              {notifications.slice(0, 15).map((n, i) => (
                <li key={`${n.createdAt}-${n.registrationId}-${i}`}>
                  <code>{n.type}</code> → {n.participantEmail} · {n.workshopTitle}{' '}
                  <span className="muted">{hourFmt.format(new Date(n.createdAt))}</span>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  )
}

async function cancelRegistration(id: number, name: string): Promise<string> {
  const result = await api.cancel(id)
  if (result.alreadyCancelled) return `Registration #${id} was already cancelled.`
  const promoted = result.promoted.map((p) => p.participantName)
  return `Cancelled ${name}'s registration.` + (promoted.length ? ` Promoted from waitlist: ${promoted.join(', ')}.` : '')
}

function WorkshopTable(props: { workshops: Workshop[]; selectedId: number | null; onSelect: (id: number) => void }) {
  return (
    <table>
      <thead>
        <tr>
          <th>Title</th>
          <th>Time</th>
          <th>Confirmed</th>
          <th>Remaining</th>
          <th>Waitlist</th>
        </tr>
      </thead>
      <tbody>
        {props.workshops.map((w) => (
          <tr
            key={w.id}
            className={`clickable ${w.id === props.selectedId ? 'selected' : ''}`}
            onClick={() => props.onSelect(w.id)}
          >
            <td>{w.title}</td>
            <td>{formatRange(w.startTime, w.endTime)}</td>
            <td>
              {w.confirmedCount}/{w.capacity}
            </td>
            <td className={w.remainingCapacity === 0 ? 'full' : ''}>
              {w.remainingCapacity === 0 ? 'Full' : w.remainingCapacity}
            </td>
            <td>{w.waitlistCount}</td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function WorkshopDetail(props: {
  workshop: Workshop
  registrations: Registration[]
  participants: Participant[]
  onRegister: (participantId: number) => Promise<void>
  onCancel: (r: Registration) => Promise<void>
}) {
  const { workshop, registrations } = props
  const [participantId, setParticipantId] = useState('')
  const [busy, setBusy] = useState(false)

  const byStatus = (s: RegistrationStatus) => registrations.filter((r) => r.status === s)
  const activeIds = new Set(registrations.filter((r) => r.status !== 'Cancelled').map((r) => r.participantId))

  async function submit(e: FormEvent) {
    e.preventDefault()
    if (!participantId) return
    setBusy(true)
    await props.onRegister(Number(participantId))
    setBusy(false)
    setParticipantId('')
  }

  return (
    <>
      <h2>{workshop.title}</h2>
      <p className="muted">
        {formatRange(workshop.startTime, workshop.endTime)} · {workshop.confirmedCount}/{workshop.capacity} confirmed ·{' '}
        {workshop.remainingCapacity} seat(s) left
      </p>

      <form onSubmit={submit} className="inline-form">
        <select value={participantId} onChange={(e) => setParticipantId(e.target.value)}>
          <option value="">Register participant…</option>
          {props.participants.map((p) => (
            <option key={p.id} value={p.id} disabled={activeIds.has(p.id)}>
              {p.name}
              {activeIds.has(p.id) ? ' (already registered)' : ''}
            </option>
          ))}
        </select>
        <button type="submit" disabled={!participantId || busy}>
          {workshop.remainingCapacity > 0 ? 'Register' : 'Join waitlist'}
        </button>
      </form>

      <RegistrationList title="Confirmed" items={byStatus('Confirmed')} onCancel={props.onCancel} />
      <RegistrationList title="Waitlist (first come, first served)" items={byStatus('Waitlisted')} onCancel={props.onCancel} />
      <RegistrationList title="Cancelled" items={byStatus('Cancelled')} />
    </>
  )
}

function RegistrationList(props: { title: string; items: Registration[]; onCancel?: (r: Registration) => Promise<void> }) {
  return (
    <div className="reg-list">
      <h3>
        {props.title} <span className="muted">({props.items.length})</span>
      </h3>
      {props.items.length === 0 ? (
        <p className="muted">None</p>
      ) : (
        <table>
          <tbody>
            {props.items.map((r) => (
              <tr key={r.id} className={`row-${r.status.toLowerCase()}`}>
                <td className="narrow">{r.waitlistPosition ? `#${r.waitlistPosition}` : ''}</td>
                <td>
                  {r.participantName} <span className="muted">{r.participantEmail}</span>
                </td>
                <td>
                  <StatusBadge status={r.status} />
                </td>
                <td className="muted">
                  {r.status === 'Cancelled' && r.cancelledAt
                    ? `cancelled ${timeFmt.format(new Date(r.cancelledAt))}`
                    : `registered ${timeFmt.format(new Date(r.createdAt))}`}
                </td>
                <td className="narrow">
                  {props.onCancel && (
                    <button className="danger" onClick={() => props.onCancel!(r)}>
                      Cancel
                    </button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  )
}

function ScheduleView({ schedule, onCancel }: { schedule: Schedule; onCancel: (registrationId: number) => Promise<void> }) {
  if (schedule.items.length === 0) return <p className="muted">{schedule.participant.name} has no registrations.</p>
  return (
    <table>
      <thead>
        <tr>
          <th>Workshop</th>
          <th>Time</th>
          <th>Status</th>
          <th />
        </tr>
      </thead>
      <tbody>
        {schedule.items.map((item) => (
          <tr key={item.registrationId} className={`row-${item.status.toLowerCase()}`}>
            <td>{item.workshopTitle}</td>
            <td>{formatRange(item.startTime, item.endTime)}</td>
            <td>
              <StatusBadge status={item.status} />
              {item.waitlistPosition && <span className="muted"> #{item.waitlistPosition}</span>}
            </td>
            <td className="narrow">
              {item.status !== 'Cancelled' && (
                <button className="danger" onClick={() => onCancel(item.registrationId)}>
                  Cancel
                </button>
              )}
            </td>
          </tr>
        ))}
      </tbody>
    </table>
  )
}

function CreateWorkshopForm({
  onCreate,
}: {
  onCreate: (body: { title: string; startTime: string; endTime: string; capacity: number }) => Promise<void>
}) {
  const [title, setTitle] = useState('')
  const [start, setStart] = useState('2026-11-12T09:00')
  const [end, setEnd] = useState('2026-11-12T10:00')
  const [capacity, setCapacity] = useState(10)

  async function submit(e: FormEvent) {
    e.preventDefault()
    await onCreate({
      title,
      startTime: new Date(start).toISOString(),
      endTime: new Date(end).toISOString(),
      capacity,
    })
    setTitle('')
  }

  return (
    <details>
      <summary>Add workshop</summary>
      <form onSubmit={submit} className="stack-form">
        <label>
          Title <input value={title} onChange={(e) => setTitle(e.target.value)} required />
        </label>
        <label>
          Start <input type="datetime-local" value={start} onChange={(e) => setStart(e.target.value)} required />
        </label>
        <label>
          End <input type="datetime-local" value={end} onChange={(e) => setEnd(e.target.value)} required />
        </label>
        <label>
          Capacity{' '}
          <input type="number" min={1} value={capacity} onChange={(e) => setCapacity(Number(e.target.value))} required />
        </label>
        <button type="submit">Create</button>
      </form>
    </details>
  )
}
