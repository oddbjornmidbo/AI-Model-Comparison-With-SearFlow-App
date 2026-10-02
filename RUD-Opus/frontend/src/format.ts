import type { WorkshopDto } from './types'

// UI copy is English, so dates use an English locale (in the viewer's local time zone).
const LOCALE = 'en-GB'

const dateFmt = new Intl.DateTimeFormat(LOCALE, {
  weekday: 'short',
  day: 'numeric',
  month: 'short',
  year: 'numeric',
})
const timeFmt = new Intl.DateTimeFormat(LOCALE, { hour: '2-digit', minute: '2-digit' })
const dateTimeFmt = new Intl.DateTimeFormat(LOCALE, {
  day: 'numeric',
  month: 'short',
  hour: '2-digit',
  minute: '2-digit',
})

/** e.g. "Tue, 14 Oct 2026, 09:00–11:00" (ends with date for multi-day spans). */
export function formatTimeRange(startIso: string, endIso: string): string {
  const s = new Date(startIso)
  const e = new Date(endIso)
  const sameDay = s.toDateString() === e.toDateString()
  return sameDay
    ? `${dateFmt.format(s)}, ${timeFmt.format(s)}–${timeFmt.format(e)}`
    : `${dateFmt.format(s)} ${timeFmt.format(s)} – ${dateFmt.format(e)} ${timeFmt.format(e)}`
}

export const formatDateTime = (iso: string) => dateTimeFmt.format(new Date(iso))

export function seatsLabel(w: Pick<WorkshopDto, 'capacity' | 'remainingSeats' | 'waitlistedCount'>): string {
  if (w.remainingSeats <= 0) {
    return `Full – ${w.waitlistedCount} on waitlist`
  }
  return `${w.remainingSeats} of ${w.capacity} seats left`
}
