import type {
  ParticipantDto,
  ProblemDetails,
  RegistrationDto,
  ScheduleDto,
  WorkshopDto,
} from './types'

export class ApiError extends Error {
  status: number
  code?: string
  constructor(message: string, status: number, code?: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
    this.code = code
  }
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let res: Response
  try {
    res = await fetch(path, init)
  } catch {
    throw new ApiError('Could not reach the server. Check your connection and try again.', 0)
  }
  if (!res.ok) {
    let problem: ProblemDetails = {}
    try {
      problem = (await res.json()) as ProblemDetails
    } catch {
      /* non-JSON error body */
    }
    const firstFieldError = problem.errors ? Object.values(problem.errors).flat()[0] : undefined
    throw new ApiError(
      problem.detail ?? firstFieldError ?? problem.title ?? `Request failed (${res.status})`,
      res.status,
      problem.code,
    )
  }
  return (await res.json()) as T
}

const json = (body: unknown): RequestInit => ({
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify(body),
})

export const api = {
  getWorkshops: () => request<WorkshopDto[]>('/api/workshops'),
  getRegistrations: (workshopId: number) =>
    request<RegistrationDto[]>(`/api/workshops/${workshopId}/registrations`),
  register: (workshopId: number, participantId: number, idempotencyKey: string) =>
    request<RegistrationDto>(`/api/workshops/${workshopId}/registrations`, {
      ...json({ participantId }),
      headers: { 'Content-Type': 'application/json', 'Idempotency-Key': idempotencyKey },
    }),
  cancel: (registrationId: number) =>
    request<RegistrationDto>(`/api/registrations/${registrationId}/cancel`, { method: 'POST' }),
  getParticipants: () => request<ParticipantDto[]>('/api/participants'),
  getSchedule: (participantId: number) =>
    request<ScheduleDto>(`/api/participants/${participantId}/schedule`),
}
