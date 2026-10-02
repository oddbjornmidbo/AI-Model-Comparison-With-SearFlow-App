import { useState } from 'react'

interface Props {
  /** Describes what is cancelled, for the accessible name, e.g. "Ada Lovelace". */
  subject: string
  busy?: boolean
  onConfirm: () => void
}

export function ConfirmCancelButton({ subject, busy, onConfirm }: Props) {
  const [confirming, setConfirming] = useState(false)

  if (!confirming) {
    return (
      <button
        type="button"
        className="btn btn--danger-outline"
        disabled={busy}
        onClick={() => setConfirming(true)}
        aria-label={`Cancel registration for ${subject}`}
      >
        Cancel
      </button>
    )
  }
  return (
    <span className="confirm" role="group" aria-label={`Confirm cancelling ${subject}`}>
      <button
        type="button"
        className="btn btn--danger"
        disabled={busy}
        autoFocus
        onClick={() => {
          onConfirm()
          setConfirming(false)
        }}
      >
        {busy ? 'Cancelling…' : 'Confirm cancel'}
      </button>
      <button type="button" className="btn" onClick={() => setConfirming(false)}>
        Keep
      </button>
    </span>
  )
}
