import { useState } from 'react'
import { Check, ShieldCheck } from 'lucide-react'
import HistoryPage from './HistoryPage'
import { approveCase, lookupCode, rejectCase } from '../services/apiClient'

// Case Expert Review — the history list filtered to NeedExpertReview, plus a decision panel.

const CODE_FORMAT = /^\d{4}\.\d{2}\.\d{2}$/
const cx = (...parts) => parts.filter(Boolean).join(' ')

function ReviewActions({ detail, onDone }) {
  const candidates = (detail.candidateCodes || []).filter(c => CODE_FORMAT.test(c))
  const [code, setCode] = useState(candidates[0] || '')
  const [override, setOverride] = useState(candidates.length === 0)
  const [reason, setReason] = useState('')
  const [preview, setPreview] = useState(null) // { code, text } | { code, missing: true }
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [notice, setNotice] = useState(null)

  if (detail.status !== 'NEED_EXPERT_REVIEW') return null

  const checkCode = async () => {
    const c = code.trim()
    if (!CODE_FORMAT.test(c)) { setPreview({ code: c, missing: true }); return }
    const line = await lookupCode(c)
    setPreview(line ? { code: c, text: line.descriptionId } : { code: c, missing: true })
  }

  const act = async (fn, label) => {
    setBusy(true); setError(null); setNotice(null)
    try { await fn(); setNotice(label); await onDone() }
    catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  const reasonOk = reason.trim().length >= 5
  const codeOk = CODE_FORMAT.test(code.trim())

  return (
    <section aria-labelledby="review-title" className="expert-panel">
      <div className="subheading">
        <div><ShieldCheck size={17} aria-hidden="true" /><span id="review-title">Keputusan ahli</span></div>
        <span className="pending-pill">PENDING</span>
      </div>
      <p className="panel-help">Pilih kandidat yang paling sesuai atau masukkan kode HS manual.</p>

      <fieldset className="field">
        <legend className="field-label">Kode HS final</legend>
        {candidates.map(c => (
          <label key={c} className={cx('candidate', !override && code === c && 'selected')}>
            <input type="radio" name="review-code" checked={!override && code === c}
              onChange={() => { setOverride(false); setCode(c); setPreview(null) }} />
            <div><code>{c}</code><span>kandidat sistem</span></div>
          </label>
        ))}
        <label className={cx('candidate', override && 'selected')}>
          <input type="radio" name="review-code" checked={override}
            onChange={() => { setOverride(true); setCode(''); setPreview(null) }} />
          <div><span>Kode lain (override)</span></div>
        </label>

        {override && (
          <div className="inline-input">
            <label htmlFor="review-override" className="sr-only">Kode HS override</label>
            <input id="review-override" value={code} onChange={e => { setCode(e.target.value); setPreview(null) }}
              placeholder="XXXX.XX.XX" />
            <button type="button" onClick={checkCode} className="ghost-button">Cek di BTKI</button>
          </div>
        )}

        {preview && (preview.missing
          ? <p role="status" className="notice danger"><span>{preview.code} tidak ditemukan di BTKI aktif</span></p>
          : (
            <div role="status" className="btki-check">
              <Check size={15} aria-hidden="true" />
              <div><strong>Kode aktif di BTKI</strong><span>{preview.code} — {preview.text}</span></div>
            </div>
          ))}

        {/* Headings (4-digit) from the engine are hints: a code under them still counts as a candidate */}
        {(detail.candidateCodes || []).some(c => !CODE_FORMAT.test(c)) && (
          <p className="hint">
            Heading kandidat: <span className="mono">{detail.candidateCodes.filter(c => !CODE_FORMAT.test(c)).join(', ')}</span>
          </p>
        )}
        {override && <p className="hint">Kode di luar heading kandidat dicatat sebagai EXPERT_OVERRIDE.</p>}
      </fieldset>

      <label className="field">
        <span>Alasan keputusan <em>*</em></span>
        <textarea id="review-reason" rows={3} value={reason} onChange={e => setReason(e.target.value)}
          placeholder="Dasar hukum / pertimbangan, mis. Catatan Bab 85, uraian subpos 8517.13" />
      </label>

      {error && <p role="alert" className="notice danger"><span>{error}</span></p>}
      {notice && <p role="status" className="notice success"><span>{notice}</span></p>}

      <button disabled={busy || !reasonOk || !codeOk}
        onClick={() => act(() => approveCase(detail.caseId, code.trim(), reason), 'Case disetujui.')}
        className="primary-button full">
        Setujui
      </button>
      <button disabled={busy || !reasonOk}
        onClick={() => act(() => rejectCase(detail.caseId, reason), 'Case ditolak.')}
        className="danger-button full">
        Tolak
      </button>
    </section>
  )
}

export default function ReviewPage() {
  return (
    <HistoryPage
      showUser
      initialStatus="NeedExpertReview"
      title="Case Expert Review"
      subtitle="Antrian case yang menunggu keputusan ahli kepabeanan"
      eyebrow="EXPERT WORKFLOW"
      renderDetail={(detail, afterChange) => (
        <ReviewActions key={detail.caseId} detail={detail} onDone={afterChange} />
      )}
    />
  )
}
