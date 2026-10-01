import { useCallback, useEffect, useState } from 'react'
import { api, type Participant, type Registration, type Status, type Workshop } from './api'

const fmt = (iso: string) =>
  new Date(iso).toLocaleString([], { weekday: 'short', hour: '2-digit', minute: '2-digit', day: 'numeric', month: 'short' })

function Badge({ status }: { status: Status }) {
  return <span className={`badge ${status.toLowerCase()}`}>{status}</span>
}

function RegistrationRow({ r, onCancel, showWorkshop }: { r: Registration; onCancel: (id: number) => void; showWorkshop?: boolean }) {
  return (
    <li className="row">
      <Badge status={r.status} />
      <span className="grow">
        {showWorkshop ? (
          <>
            <b>{r.workshopTitle}</b> <small>{fmt(r.workshopStart)} – {fmt(r.workshopEnd)}</small>
          </>
        ) : (
          <>
            <b>{r.participantName}</b> <small>{r.participantEmail}</small>
          </>
        )}
      </span>
      {r.status !== 'Cancelled' && <button onClick={() => onCancel(r.id)}>Cancel</button>}
    </li>
  )
}

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([])
  const [participants, setParticipants] = useState<Participant[]>([])
  const [selectedWorkshop, setSelectedWorkshop] = useState<number | null>(null)
  const [registrations, setRegistrations] = useState<Registration[]>([])
  const [registerAs, setRegisterAs] = useState<number | ''>('')
  const [schedulePerson, setSchedulePerson] = useState<number | ''>('')
  const [schedule, setSchedule] = useState<Registration[]>([])
  const [error, setError] = useState<string | null>(null)

  const refresh = useCallback(async () => {
    try {
      const [w, p] = await Promise.all([api.workshops(), api.participants()])
      setWorkshops(w)
      setParticipants(p)
      setRegistrations(selectedWorkshop ? await api.registrations(selectedWorkshop) : [])
      setSchedule(schedulePerson ? await api.schedule(schedulePerson) : [])
    } catch (e) {
      setError((e as Error).message)
    }
  }, [selectedWorkshop, schedulePerson])

  useEffect(() => {
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void refresh()
  }, [refresh])

  async function act(action: () => Promise<unknown>) {
    setError(null)
    try {
      await action()
    } catch (e) {
      setError((e as Error).message)
    }
    await refresh()
  }

  const register = () =>
    act(() => api.register(selectedWorkshop!, Number(registerAs), crypto.randomUUID()))
  const cancel = (id: number) => act(() => api.cancel(id))

  const confirmed = registrations.filter((r) => r.status === 'Confirmed')
  const waitlist = registrations.filter((r) => r.status === 'Waitlisted') // API returns FIFO order
  const cancelled = registrations.filter((r) => r.status === 'Cancelled')
  const workshop = workshops.find((w) => w.id === selectedWorkshop)

  return (
    <div className="app">
      <h1>SeatFlow</h1>
      {error && (
        <div className="error" role="alert">
          {error} <button onClick={() => setError(null)}>×</button>
        </div>
      )}

      <section>
        <h2>Workshops</h2>
        <table>
          <thead>
            <tr><th>Title</th><th>Time</th><th>Confirmed</th><th>Remaining</th><th>Waitlist</th></tr>
          </thead>
          <tbody>
            {workshops.map((w) => (
              <tr key={w.id} className={w.id === selectedWorkshop ? 'selected' : ''} onClick={() => setSelectedWorkshop(w.id)}>
                <td><b>{w.title}</b></td>
                <td>{fmt(w.startTime)} – {fmt(w.endTime)}</td>
                <td>{w.confirmedCount}/{w.capacity}</td>
                <td>{w.remainingCapacity === 0 ? <span className="full">Full</span> : w.remainingCapacity}</td>
                <td>{w.waitlistedCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {workshop && (
        <section>
          <h2>{workshop.title}</h2>
          <div className="form">
            <select value={registerAs} onChange={(e) => setRegisterAs(e.target.value ? Number(e.target.value) : '')}>
              <option value="">Select participant…</option>
              {participants.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
            <button disabled={registerAs === ''} onClick={register}>Register</button>
          </div>

          <h3>Confirmed ({confirmed.length}/{workshop.capacity})</h3>
          <ul>{confirmed.map((r) => <RegistrationRow key={r.id} r={r} onCancel={cancel} />)}</ul>
          {confirmed.length === 0 && <p className="empty">None</p>}

          <h3>Waitlist, in promotion order ({waitlist.length})</h3>
          <ol>{waitlist.map((r) => <RegistrationRow key={r.id} r={r} onCancel={cancel} />)}</ol>
          {waitlist.length === 0 && <p className="empty">Empty</p>}

          {cancelled.length > 0 && (
            <>
              <h3>Cancelled ({cancelled.length})</h3>
              <ul>{cancelled.map((r) => <RegistrationRow key={r.id} r={r} onCancel={cancel} />)}</ul>
            </>
          )}
        </section>
      )}

      <section>
        <h2>Participant schedule</h2>
        <select value={schedulePerson} onChange={(e) => setSchedulePerson(e.target.value ? Number(e.target.value) : '')}>
          <option value="">Select participant…</option>
          {participants.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
        </select>
        <ul>{schedule.map((r) => <RegistrationRow key={r.id} r={r} onCancel={cancel} showWorkshop />)}</ul>
        {schedulePerson !== '' && schedule.length === 0 && <p className="empty">No registrations</p>}
      </section>
    </div>
  )
}
