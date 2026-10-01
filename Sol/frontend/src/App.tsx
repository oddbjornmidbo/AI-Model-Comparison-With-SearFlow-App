import { useCallback, useEffect, useState } from 'react'

type Status = 'Confirmed' | 'Waitlisted' | 'Cancelled'
type Workshop = { id: number; title: string; startTime: string; endTime: string; capacity: number; confirmedCount: number; remainingCapacity: number }
type Participant = { id: number; name: string; email: string }
type Registration = { id: number; workshopId: number; participantId: number; participantName: string; participantEmail: string; status: Status; createdAt: string }
type ScheduleItem = { id: number; workshopId: number; title: string; startTime: string; endTime: string; status: Status }

async function api<T>(url: string, options?: RequestInit): Promise<T> {
  const response = await fetch(url, options)
  if (!response.ok) { const data = await response.json().catch(() => ({})); throw new Error(data.error || `Request failed (${response.status})`) }
  return response.json()
}
const date = (value: string) => new Date(value).toLocaleString()

export default function App() {
  const [workshops, setWorkshops] = useState<Workshop[]>([])
  const [participants, setParticipants] = useState<Participant[]>([])
  const [selectedWorkshop, setSelectedWorkshop] = useState<number | null>(null)
  const [selectedParticipant, setSelectedParticipant] = useState<number | null>(null)
  const [registrations, setRegistrations] = useState<Registration[]>([])
  const [schedule, setSchedule] = useState<ScheduleItem[]>([])
  const [message, setMessage] = useState('')
  const [busy, setBusy] = useState(false)

  const refresh = useCallback(async () => {
    try {
      const [ws, ps] = await Promise.all([api<Workshop[]>('/api/workshops'), api<Participant[]>('/api/participants')])
      setWorkshops(ws); setParticipants(ps)
      setSelectedWorkshop(current => current ?? ws[0]?.id ?? null)
      setSelectedParticipant(current => current ?? ps[0]?.id ?? null)
    } catch (error) { setMessage((error as Error).message) }
  }, [])
  const refreshDetails = useCallback(async () => {
    try {
      if (selectedWorkshop !== null) setRegistrations(await api<Registration[]>(`/api/workshops/${selectedWorkshop}/registrations`))
      if (selectedParticipant !== null) setSchedule(await api<ScheduleItem[]>(`/api/participants/${selectedParticipant}/schedule`))
    } catch (error) { setMessage((error as Error).message) }
  }, [selectedWorkshop, selectedParticipant])
  useEffect(() => { void refresh() }, [refresh])
  useEffect(() => { void refreshDetails() }, [refreshDetails])
  const update = async (action: () => Promise<unknown>, success: string) => {
    setBusy(true); setMessage('')
    try { await action(); await refresh(); await refreshDetails(); setMessage(success) }
    catch (error) { setMessage((error as Error).message) }
    finally { setBusy(false) }
  }
  const register = () => {
    if (selectedWorkshop === null || selectedParticipant === null) return
    const key = crypto.randomUUID()
    void update(() => api(`/api/workshops/${selectedWorkshop}/registrations`, { method: 'POST', headers: { 'Content-Type': 'application/json', 'Idempotency-Key': key }, body: JSON.stringify({ participantId: selectedParticipant }) }), 'Registration saved')
  }
  const cancel = (id: number) => void update(() => api(`/api/registrations/${id}/cancel`, { method: 'POST' }), 'Registration cancelled')
  const workshop = workshops.find(w => w.id === selectedWorkshop)
  return <main>
    <header><h1>SeatFlow</h1><p>Workshop registrations</p></header>
    {message && <p role="status" className="message">{message}</p>}
    <section><h2>Workshops</h2><div className="workshops">{workshops.map(w => <button className={w.id === selectedWorkshop ? 'selected' : ''} key={w.id} onClick={() => setSelectedWorkshop(w.id)}><strong>{w.title}</strong><span>{date(w.startTime)} – {date(w.endTime)}</span><span>{w.remainingCapacity} of {w.capacity} seats remaining</span></button>)}</div></section>
    {workshop && <section><h2>{workshop.title}</h2><div className="controls"><label>Participant <select value={selectedParticipant ?? ''} onChange={e => setSelectedParticipant(Number(e.target.value))}>{participants.map(p => <option key={p.id} value={p.id}>{p.name} ({p.email})</option>)}</select></label><button disabled={busy || selectedParticipant === null} onClick={register}>Register</button></div>
      <h3>Confirmed</h3><RegistrationList items={registrations.filter(r => r.status === 'Confirmed')} busy={busy} onCancel={cancel}/>
      <h3>Waitlist</h3><RegistrationList items={registrations.filter(r => r.status === 'Waitlisted')} busy={busy} onCancel={cancel}/>
      <h3>Cancelled</h3><RegistrationList items={registrations.filter(r => r.status === 'Cancelled')} busy={busy} onCancel={cancel}/>
    </section>}
    <section><h2>Participant schedule</h2><label>Participant <select value={selectedParticipant ?? ''} onChange={e => setSelectedParticipant(Number(e.target.value))}>{participants.map(p => <option key={p.id} value={p.id}>{p.name}</option>)}</select></label>{schedule.length ? <ul>{schedule.map(item => <li key={item.id}><strong>{item.title}</strong> · {date(item.startTime)} – {date(item.endTime)} <span className="status Confirmed">Confirmed</span></li>)}</ul> : <p>No confirmed workshops.</p>}</section>
  </main>
}
function RegistrationList({ items, busy, onCancel }: { items: Registration[]; busy: boolean; onCancel: (id: number) => void }) {
  return items.length ? <ul>{items.map(r => <li key={r.id}><span><strong>{r.participantName}</strong> · {r.participantEmail} <span className={`status ${r.status}`}>{r.status}</span></span>{r.status !== 'Cancelled' && <button disabled={busy} onClick={() => onCancel(r.id)}>Cancel</button>}</li>)}</ul> : <p>None.</p>
}
