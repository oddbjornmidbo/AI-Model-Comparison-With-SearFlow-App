export type RegistrationStatus = 'Confirmed' | 'Waitlisted' | 'Cancelled'

export interface WorkshopDto {
  id: number
  title: string
  startTime: string
  endTime: string
  capacity: number
  confirmedCount: number
  waitlistedCount: number
  remainingSeats: number
}

export interface ParticipantDto {
  id: number
  name: string
  email: string
}

export interface RegistrationDto {
  id: number
  workshopId: number
  participantId: number
  participantName: string
  participantEmail: string
  status: RegistrationStatus
  createdAt: string
  waitlistPosition: number | null
}

export interface ScheduleItemDto {
  registrationId: number
  status: RegistrationStatus
  createdAt: string
  waitlistPosition: number | null
  workshop: { id: number; title: string; startTime: string; endTime: string }
}

export interface ScheduleDto {
  participant: ParticipantDto
  registrations: ScheduleItemDto[]
}

export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  detail?: string
  code?: 'duplicate_registration' | 'schedule_conflict' | 'idempotency_key_reused' | 'not_found'
  errors?: Record<string, string[]>
}
