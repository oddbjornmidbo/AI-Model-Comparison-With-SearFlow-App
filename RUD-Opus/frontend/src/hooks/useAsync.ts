import { useCallback, useEffect, useRef, useState } from 'react'

export interface AsyncState<T> {
  data: T | undefined
  loading: boolean
  error: string | undefined
  reload: () => Promise<void>
}

/** Loads data via `fn` when `key` changes (key null = skip). Keeps stale data while reloading. */
export function useAsync<T>(fn: () => Promise<T>, key: unknown): AsyncState<T> {
  const [data, setData] = useState<T>()
  const [loading, setLoading] = useState(key !== null)
  const [error, setError] = useState<string>()
  const fnRef = useRef(fn)
  useEffect(() => {
    fnRef.current = fn
  })
  const seq = useRef(0)

  const run = useCallback(async () => {
    const id = ++seq.current
    setLoading(true)
    try {
      const result = await fnRef.current()
      if (id !== seq.current) return
      setData(result)
      setError(undefined)
    } catch (e) {
      if (id !== seq.current) return
      setError(e instanceof Error ? e.message : 'Something went wrong')
    } finally {
      if (id === seq.current) setLoading(false)
    }
  }, [])

  /* Resetting state when the key changes is the point of this effect. */
  /* oxlint-disable react/set-state-in-effect */
  useEffect(() => {
    if (key === null) {
      seq.current++
      setData(undefined)
      setError(undefined)
      setLoading(false)
      return
    }
    setData(undefined)
    void run()
  }, [key, run])

  return { data, loading, error, reload: run }
}
