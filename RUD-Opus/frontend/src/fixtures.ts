import type { ParticipantDto, RegistrationDto, ScheduleDto, WorkshopDto } from './types'

export const workshops: WorkshopDto[] = [
  { id: 1, title: 'Intro to Rust', startTime: '2026-10-14T08:00:00Z', endTime: '2026-10-14T10:00:00Z', capacity: 20, confirmedCount: 17, waitlistedCount: 0, remainingSeats: 3 },
  { id: 2, title: 'Designing Accessible Interfaces', startTime: '2026-10-14T10:30:00Z', endTime: '2026-10-14T12:30:00Z', capacity: 2, confirmedCount: 2, waitlistedCount: 2, remainingSeats: 0 },
  { id: 3, title: 'Event Sourcing in Practice', startTime: '2026-10-15T09:00:00Z', endTime: '2026-10-15T12:00:00Z', capacity: 30, confirmedCount: 0, waitlistedCount: 0, remainingSeats: 30 },
]

export const participants: ParticipantDto[] = [
  { id: 1, name: 'Ada Lovelace', email: 'ada@example.com' },
  { id: 2, name: 'Grace Hopper', email: 'grace@example.com' },
  { id: 3, name: 'Alan Turing', email: 'alan@example.com' },
  { id: 4, name: 'Linus Torvalds', email: 'linus@example.com' },
  { id: 5, name: 'Margaret Hamilton', email: 'margaret@example.com' },
]

const base = { workshopId: 2 }
export const registrations: RegistrationDto[] = [
  { ...base, id: 11, participantId: 1, participantName: 'Ada Lovelace', participantEmail: 'ada@example.com', status: 'Confirmed', createdAt: '2026-10-01T08:00:00Z', waitlistPosition: null },
  { ...base, id: 12, participantId: 2, participantName: 'Grace Hopper', participantEmail: 'grace@example.com', status: 'Confirmed', createdAt: '2026-10-01T08:05:00Z', waitlistPosition: null },
  { ...base, id: 13, participantId: 3, participantName: 'Alan Turing', participantEmail: 'alan@example.com', status: 'Waitlisted', createdAt: '2026-10-01T09:00:00Z', waitlistPosition: 1 },
  { ...base, id: 14, participantId: 4, participantName: 'Linus Torvalds', participantEmail: 'linus@example.com', status: 'Waitlisted', createdAt: '2026-10-01T09:30:00Z', waitlistPosition: 2 },
  { ...base, id: 15, participantId: 5, participantName: 'Margaret Hamilton', participantEmail: 'margaret@example.com', status: 'Cancelled', createdAt: '2026-10-01T07:30:00Z', waitlistPosition: null },
]

export const schedule: ScheduleDto = {
  participant: participants[2],
  registrations: [
    { registrationId: 1, status: 'Confirmed', createdAt: '2026-10-01T08:00:00Z', waitlistPosition: null, workshop: { id: 1, title: 'Intro to Rust', startTime: workshops[0].startTime, endTime: workshops[0].endTime } },
    { registrationId: 13, status: 'Waitlisted', createdAt: '2026-10-01T09:00:00Z', waitlistPosition: 1, workshop: { id: 2, title: 'Designing Accessible Interfaces', startTime: workshops[1].startTime, endTime: workshops[1].endTime } },
    { registrationId: 20, status: 'Cancelled', createdAt: '2026-10-01T09:00:00Z', waitlistPosition: null, workshop: { id: 3, title: 'Event Sourcing in Practice', startTime: workshops[2].startTime, endTime: workshops[2].endTime } },
  ],
}
