import { useCallback, useState } from 'react'
import { api } from './api'
import { ScheduleView } from './components/ScheduleView'
import { StatePanel } from './components/StatePanel'
import { StatusMessage, type Message } from './components/StatusMessage'
import { WorkshopDetail } from './components/WorkshopDetail'
import { WorkshopList } from './components/WorkshopList'
import { useAsync } from './hooks/useAsync'

export default function App() {
  const [workshopId, setWorkshopId] = useState<number | null>(null)
  const [scheduleParticipantId, setScheduleParticipantId] = useState<number | null>(null)
  const [message, setMessage] = useState<Message | null>(null)
  const [submitting, setSubmitting] = useState(false)
  const [cancellingId, setCancellingId] = useState<number | null>(null)

  const workshops = useAsync(() => api.getWorkshops(), 'workshops')
  const participants = useAsync(() => api.getParticipants(), 'participants')
  const registrations = useAsync(
    () => api.getRegistrations(workshopId!),
    workshopId,
  )
  const schedule = useAsync(
    () => api.getSchedule(scheduleParticipantId!),
    scheduleParticipantId,
  )

  const selected = workshops.data?.find((w) => w.id === workshopId)

  // Capacity, lists and schedules can all change (e.g. waitlist promotion), so refetch everything open.
  const refreshAll = useCallback(async () => {
    await Promise.all([
      workshops.reload(),
      workshopId !== null ? registrations.reload() : undefined,
      scheduleParticipantId !== null ? schedule.reload() : undefined,
    ])
  }, [workshops, registrations, schedule, workshopId, scheduleParticipantId])

  const handleRegister = async (participantId: number) => {
    if (workshopId === null) return
    setSubmitting(true)
    setMessage(null)
    try {
      const reg = await api.register(workshopId, participantId, crypto.randomUUID())
      setMessage({
        kind: 'success',
        text:
          reg.status === 'Waitlisted'
            ? `${reg.participantName} was added to the waitlist at position ${reg.waitlistPosition ?? '?'}.`
            : `${reg.participantName} is confirmed for this workshop.`,
      })
      await refreshAll()
    } catch (e) {
      setMessage({ kind: 'error', text: e instanceof Error ? e.message : 'Registration failed.' })
    } finally {
      setSubmitting(false)
    }
  }

  const handleCancel = async (registrationId: number) => {
    setCancellingId(registrationId)
    setMessage(null)
    try {
      const reg = await api.cancel(registrationId)
      setMessage({ kind: 'success', text: `Registration for ${reg.participantName} was cancelled.` })
      await refreshAll()
    } catch (e) {
      setMessage({ kind: 'error', text: e instanceof Error ? e.message : 'Cancellation failed.' })
    } finally {
      setCancellingId(null)
    }
  }

  return (
    <>
      <header className="app-header">
        <h1>SeatFlow</h1>
        <p>Workshop registration</p>
      </header>
      <main className="layout">
        <StatusMessage message={message} onDismiss={() => setMessage(null)} />
        <div className="layout__list">
          <WorkshopList
            workshops={workshops.data}
            selectedId={workshopId}
            onSelect={(id) => {
              setWorkshopId(id)
              setMessage(null)
            }}
            loading={workshops.loading}
            error={workshops.error}
            onRetry={() => void workshops.reload()}
          />
        </div>
        <div className="layout__detail">
          {selected ? (
            <WorkshopDetail
              workshop={selected}
              registrations={registrations.data}
              registrationsLoading={registrations.loading}
              registrationsError={registrations.error}
              onRetryRegistrations={() => void registrations.reload()}
              participants={participants.data}
              participantsError={participants.error}
              submitting={submitting}
              cancellingId={cancellingId}
              onRegister={(id) => void handleRegister(id)}
              onCancel={(id) => void handleCancel(id)}
            />
          ) : (
            <StatePanel variant="empty" message="Select a workshop to see its registrations." />
          )}
          <ScheduleView
            participants={participants.data}
            selectedId={scheduleParticipantId}
            onSelect={setScheduleParticipantId}
            schedule={schedule.data}
            loading={schedule.loading}
            error={schedule.error}
            onRetry={() => void schedule.reload()}
          />
        </div>
      </main>
    </>
  )
}
