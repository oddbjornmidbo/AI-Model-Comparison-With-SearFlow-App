export type Status = 'Confirmed' | 'Waitlisted' | 'Cancelled'

export interface Workshop {
  id: number
  title: string
  startTime: string
  endTime: string
  capacity: number
  confirmedCount: number
  waitlistedCount: number
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
  workshopTitle: string
  workshopStart: string
  workshopEnd: string
  participantId: number
  participantName: string
  participantEmail: string
  status: Status
  createdAt: string
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const res = await fetch(path, init)
  if (!res.ok) {
    let message = `${res.status} ${res.statusText}`
    try {
      const body = await res.json()
      if (body.errors) message = Object.values<string[]>(body.errors).flat().join(' ')
      else if (body.detail) message = body.detail
      else if (body.title) message = body.title
    } catch {
      /* non-JSON error body */
    }
    throw new Error(message)
  }
  return res.json() as Promise<T>
}

export const api = {
  workshops: () => request<Workshop[]>('/api/workshops'),
  participants: () => request<Participant[]>('/api/participants'),
  registrations: (workshopId: number) => request<Registration[]>(`/api/workshops/${workshopId}/registrations`),
  schedule: (participantId: number) => request<Registration[]>(`/api/participants/${participantId}/schedule`),
  register: (workshopId: number, participantId: number, idempotencyKey: string) =>
    request<Registration>(`/api/workshops/${workshopId}/registrations`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
      body: JSON.stringify({ participantId }),
    }),
  cancel: (registrationId: number) =>
    request<Registration>(`/api/registrations/${registrationId}/cancel`, { method: 'POST' }),
}
