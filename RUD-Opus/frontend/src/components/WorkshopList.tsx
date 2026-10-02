import { formatTimeRange, seatsLabel } from '../format'
import type { WorkshopDto } from '../types'
import { StatePanel } from './StatePanel'

interface Props {
  workshops: WorkshopDto[] | undefined
  selectedId: number | null
  onSelect: (id: number) => void
  loading?: boolean
  error?: string
  onRetry?: () => void
}

export function WorkshopList({ workshops, selectedId, onSelect, loading, error, onRetry }: Props) {
  let body: React.ReactNode
  if (error && !workshops) body = <StatePanel variant="error" message={error} onRetry={onRetry} />
  else if (loading && !workshops) body = <StatePanel variant="loading" message="Loading workshops…" />
  else if (!workshops || workshops.length === 0)
    body = <StatePanel variant="empty" message="No workshops have been scheduled yet." />
  else
    body = (
      <ul className="workshop-list">
        {workshops.map((w) => {
          const full = w.remainingSeats <= 0
          return (
            <li key={w.id}>
              <button
                type="button"
                className="workshop"
                aria-pressed={w.id === selectedId}
                onClick={() => onSelect(w.id)}
              >
                <span className="workshop__title">{w.title}</span>
                <span className="workshop__time">{formatTimeRange(w.startTime, w.endTime)}</span>
                <span className={`workshop__seats${full ? ' workshop__seats--full' : ''}`}>
                  {seatsLabel(w)}
                </span>
              </button>
            </li>
          )
        })}
      </ul>
    )
  return (
    <section aria-labelledby="workshops-h">
      <h2 id="workshops-h">Workshops</h2>
      {body}
    </section>
  )
}
