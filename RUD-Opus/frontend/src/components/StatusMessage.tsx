export interface Message {
  kind: 'success' | 'error'
  text: string
}

interface Props {
  message: Message | null
  onDismiss?: () => void
}

/** The live region is always rendered so screen readers announce updates reliably. */
export function StatusMessage({ message, onDismiss }: Props) {
  return (
    <div aria-live={message?.kind === 'error' ? 'assertive' : 'polite'} aria-atomic="true" className="live">
      {message && (
        <div className={`notice notice--${message.kind}`}>
          <span>
            <strong>{message.kind === 'error' ? 'Error: ' : 'Success: '}</strong>
            {message.text}
          </span>
          {onDismiss && (
            <button type="button" className="btn btn--ghost" onClick={onDismiss}>
              Dismiss
            </button>
          )}
        </div>
      )}
    </div>
  )
}
