export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled'

export interface Workshop {
  id: number
  title: string
  startTime: string
  endTime: string
  capacity: number
  confirmedCount: number
  waitlistCount: number
  remainingCapacity: number
}

export interface Participant {
  id: number
  name: string
  email: string
}

export interface Registration {
  id: number
  workshopId: number
  participantId: number
  participantName: string
  participantEmail: string
  status: RegistrationStatus
  createdAt: string
  cancelledAt: string | null
  waitlistPosition: number | null
}

export interface CancelResponse {
  registration: Registration
  alreadyCancelled: boolean
  promoted: Registration[]
}

export interface ScheduleItem {
  registrationId: number
  status: RegistrationStatus
  workshopId: number
  workshopTitle: string
  startTime: string
  endTime: string
  createdAt: string
  waitlistPosition: number | null
}

export interface Schedule {
  participant: Participant
  items: ScheduleItem[]
}

export interface NotificationEntry {
  type: string
  registrationId: number
  workshopTitle: string
  participantEmail: string
  createdAt: string
}

export class ApiError extends Error {}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/api${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    let message = `${response.status} ${response.statusText}`
    try {
      const problem = await response.json()
      const fieldErrors = problem.errors ? Object.values(problem.errors).flat().join(' ') : ''
      message = problem.detail ?? (fieldErrors || problem.title) ?? message
    } catch {
      // non-JSON error body; keep the status text
    }
    throw new ApiError(message)
  }
  return response.json() as Promise<T>
}

export const api = {
  workshops: () => request<Workshop[]>('/workshops'),
  createWorkshop: (body: { title: string; startTime: string; endTime: string; capacity: number }) =>
    request<Workshop>('/workshops', { method: 'POST', body: JSON.stringify(body) }),
  registrations: (workshopId: number) => request<Registration[]>(`/workshops/${workshopId}/registrations`),
  register: (workshopId: number, participantId: number, idempotencyKey: string) =>
    request<Registration>(`/workshops/${workshopId}/registrations`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ participantId }),
    }),
  cancel: (registrationId: number) =>
    request<CancelResponse>(`/registrations/${registrationId}/cancel`, { method: 'POST' }),
  participants: () => request<Participant[]>('/participants'),
  schedule: (participantId: number) => request<Schedule>(`/participants/${participantId}/schedule`),
  notifications: () => request<NotificationEntry[]>('/notifications'),
}
