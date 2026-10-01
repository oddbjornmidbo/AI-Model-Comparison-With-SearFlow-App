const API_BASE = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5100'

export type Status = 'Confirmed' | 'Waitlisted' | 'Cancelled'

export interface Workshop {
  id: number
  title: string
  startTime: string
  endTime: string
  capacity: number
  confirmedCount: number
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
  status: Status
  createdAt: string
}

export interface ScheduleItem {
  registrationId: number
  workshopId: number
  workshopTitle: string
  startTime: string
  endTime: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    ...init,
    headers: { 'Content-Type': 'application/json', ...init?.headers },
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => null) as { detail?: string; error?: string; errors?: Record<string, string[]> } | null
    const validation = problem?.errors ? Object.values(problem.errors).flat().join(' ') : undefined
    throw new Error(problem?.detail ?? validation ?? problem?.error ?? `Request failed (${response.status})`)
  }
  if (response.status === 204) return undefined as T
  return response.json() as Promise<T>
}

export const api = {
  workshops: () => request<Workshop[]>('/api/workshops'),
  participants: () => request<Participant[]>('/api/participants'),
  registrations: (workshopId: number) => request<Registration[]>(`/api/workshops/${workshopId}/registrations`),
  register: (workshopId: number, participantId: number) => request<Registration>(`/api/workshops/${workshopId}/registrations`, {
    method: 'POST',
    headers: { 'Idempotency-Key': crypto.randomUUID() },
    body: JSON.stringify({ participantId }),
  }),
  cancel: (registrationId: number) => request<{ registration: Registration; alreadyCancelled: boolean }>(`/api/registrations/${registrationId}/cancel`, { method: 'POST' }),
  schedule: (participantId: number) => request<ScheduleItem[]>(`/api/participants/${participantId}/schedule`),
}
