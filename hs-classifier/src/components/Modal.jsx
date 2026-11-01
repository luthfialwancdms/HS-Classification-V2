import { useEffect, useRef } from 'react'
import { createPortal } from 'react-dom'
import { X } from 'lucide-react'

// Cangkang dialog modal (presentasi saja). Tetap ter-mount saat tertutup (atribut `hidden`)
// supaya state dan nilai kontrol di dalamnya tidak hilang; menutup hanya menyembunyikan.
const FOCUSABLE = 'a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not([tabindex="-1"])'

export default function Modal({ open, onClose, titleId, title, footer, children, returnFocusRef, closeLabel = 'Tutup' }) {
  const dialogRef = useRef(null)
  const wasOpen = useRef(false)
  const onCloseRef = useRef(onClose)
  useEffect(() => { onCloseRef.current = onClose })

  // Saat terbuka: kunci scroll halaman, pindahkan fokus ke dialog, tahan fokus (trap) dan tangani Esc/Tab.
  useEffect(() => {
    if (!open) return
    const dialog = dialogRef.current
    document.body.classList.add('modal-open')
    dialog?.focus()
    const onFocusIn = (e) => { if (dialog && !dialog.contains(e.target)) dialog.focus() }
    const onKey = (e) => {
      if (e.key === 'Escape') { e.preventDefault(); onCloseRef.current(); return }
      if (e.key !== 'Tab' || !dialog) return
      const items = [...dialog.querySelectorAll(FOCUSABLE)].filter(el => el.offsetParent !== null)
      if (items.length === 0) { e.preventDefault(); dialog.focus(); return }
      const first = items[0], last = items[items.length - 1], active = document.activeElement
      if (e.shiftKey && (active === first || active === dialog)) { e.preventDefault(); last.focus() }
      else if (!e.shiftKey && (active === last || !dialog.contains(active))) { e.preventDefault(); first.focus() }
    }
    document.addEventListener('focusin', onFocusIn)
    document.addEventListener('keydown', onKey)
    return () => {
      document.body.classList.remove('modal-open')
      document.removeEventListener('focusin', onFocusIn)
      document.removeEventListener('keydown', onKey)
    }
  }, [open])

  // Setelah ditutup (bukan dilepas), kembalikan fokus ke pemicunya.
  useEffect(() => {
    if (wasOpen.current && !open) returnFocusRef?.current?.focus()
    wasOpen.current = open
  }, [open, returnFocusRef])

  return createPortal(
    <div className="modal-overlay" hidden={!open}
      onMouseDown={(e) => { if (e.target === e.currentTarget) { e.preventDefault(); onClose() } }}>
      <section ref={dialogRef} role="dialog" aria-modal="true" aria-labelledby={titleId} tabIndex={-1} className="modal">
        <header className="modal-header">
          <h2 id={titleId} className="modal-title">{title}</h2>
          <button type="button" className="modal-close" aria-label={closeLabel} onClick={onClose}>
            <X size={18} aria-hidden="true" />
          </button>
        </header>
        <div className="modal-body">{children}</div>
        {footer && <footer className="modal-footer">{footer}</footer>}
      </section>
    </div>,
    document.body,
  )
}
