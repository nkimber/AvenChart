import { useEffect, useState } from 'react'

export type ToastItem = {
  id: number
  message: string
  type: 'success' | 'error'
}

let _next = 0
type Listener = (items: ToastItem[]) => void
let _items: ToastItem[] = []
const _listeners = new Set<Listener>()

function notify() {
  _listeners.forEach((fn) => fn([..._items]))
}

export function showToast(message: string, type: ToastItem['type'] = 'success') {
  const id = ++_next
  _items = [..._items, { id, message, type }]
  notify()
  setTimeout(() => {
    _items = _items.filter((t) => t.id !== id)
    notify()
  }, 3400)
}

export function ToastContainer() {
  const [items, setItems] = useState<ToastItem[]>([])

  useEffect(() => {
    _listeners.add(setItems)
    return () => { _listeners.delete(setItems) }
  }, [])

  if (items.length === 0) return null

  return (
    <div className="toast-container" aria-live="polite" aria-atomic="false">
      {items.map((t) => (
        <div
          key={t.id}
          className={`toast toast-${t.type}`}
          role="status"
        >
          {t.message}
        </div>
      ))}
    </div>
  )
}
