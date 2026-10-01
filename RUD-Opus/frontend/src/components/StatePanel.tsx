interface Props {
  variant: 'loading' | 'error' | 'empty'
  message?: string
  onRetry?: () => void
}

export function StatePanel({ variant, message, onRetry }: Props) {
  if (variant === 'loading') {
    return (
      <div className="state" role="status">
        <span className="spinner" aria-hidden="true" />
        {message ?? 'Loading…'}
      </div>
    )
  }
  if (variant === 'error') {
    return (
      <div className="state state--error" role="alert">
        <p>{message ?? 'Something went wrong.'}</p>
        {onRetry && (
          <button type="button" className="btn" onClick={onRetry}>
            Try again
          </button>
        )}
      </div>
    )
  }
  return <div className="state">{message ?? 'Nothing here yet.'}</div>
}
