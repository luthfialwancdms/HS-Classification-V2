import { useState } from 'react'
import HistoryPage from './HistoryPage'
import { approveCase, lookupCode, rejectCase } from '../services/apiClient'

// Case Expert Review — the history list filtered to NeedExpertReview, plus a decision panel.

const CODE_FORMAT = /^\d{4}\.\d{2}\.\d{2}$/

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
    <section aria-labelledby="review-title" className="bg-white rounded-2xl border-2 border-blue-200 shadow-sm p-5">
      <h3 id="review-title" className="text-sm font-semibold text-gray-800 mb-3">Keputusan ahli</h3>

      <fieldset className="flex flex-col gap-1.5 mb-3">
        <legend className="text-xs text-gray-600 mb-1">Kode HS final</legend>
        {candidates.map(c => (
          <label key={c} className="flex items-center gap-2 text-sm">
            <input type="radio" name="review-code" checked={!override && code === c}
              onChange={() => { setOverride(false); setCode(c); setPreview(null) }} />
            <span className="font-mono">{c}</span> <span className="text-xs text-gray-500">(kandidat sistem)</span>
          </label>
        ))}
        <label className="flex items-center gap-2 text-sm">
          <input type="radio" name="review-code" checked={override}
            onChange={() => { setOverride(true); setCode(''); setPreview(null) }} />
          Kode lain (override)
        </label>
        {override && (
          <div className="flex gap-2 ml-6">
            <label htmlFor="review-override" className="sr-only">Kode HS override</label>
            <input id="review-override" value={code} onChange={e => { setCode(e.target.value); setPreview(null) }}
              placeholder="XXXX.XX.XX" className="text-sm font-mono border border-gray-300 rounded p-1.5 w-36" />
            <button type="button" onClick={checkCode} className="text-sm px-2 rounded border border-gray-300">Cek di BTKI</button>
          </div>
        )}
        {preview && (
          <p className={`text-xs ml-6 ${preview.missing ? 'text-red-600' : 'text-green-700'}`} role="status">
            {preview.missing ? `${preview.code} tidak ditemukan di BTKI aktif` : `${preview.code} — ${preview.text}`}
          </p>
        )}
        {/* Headings (4-digit) from the engine are hints: a code under them still counts as a candidate */}
        {(detail.candidateCodes || []).some(c => !CODE_FORMAT.test(c)) && (
          <p className="text-xs text-gray-500 ml-6">
            Heading kandidat: <span className="font-mono">{detail.candidateCodes.filter(c => !CODE_FORMAT.test(c)).join(', ')}</span>
          </p>
        )}
        {override && <p className="text-xs text-purple-700 ml-6">Kode di luar heading kandidat dicatat sebagai EXPERT_OVERRIDE.</p>}
      </fieldset>

      <label htmlFor="review-reason" className="block text-xs text-gray-600 mb-1">Alasan keputusan (wajib)</label>
      <textarea id="review-reason" rows={3} value={reason} onChange={e => setReason(e.target.value)}
        placeholder="Dasar hukum / pertimbangan, mis. Catatan Bab 85, uraian subpos 8517.13"
        className="w-full text-sm border border-gray-300 rounded p-2 mb-3" />

      {error && <p role="alert" className="text-sm text-red-600 mb-2">{error}</p>}
      {notice && <p role="status" className="text-sm text-green-700 mb-2">{notice}</p>}

      <div className="flex gap-2">
        <button disabled={busy || !reasonOk || !codeOk}
          onClick={() => act(() => approveCase(detail.caseId, code.trim(), reason), 'Case disetujui.')}
          className="px-3 py-1.5 text-sm rounded-lg font-medium bg-green-600 text-white disabled:opacity-50">
          Setujui
        </button>
        <button disabled={busy || !reasonOk}
          onClick={() => act(() => rejectCase(detail.caseId, reason), 'Case ditolak.')}
          className="px-3 py-1.5 text-sm rounded-lg font-medium bg-red-600 text-white disabled:opacity-50">
          Tolak
        </button>
      </div>
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
      renderDetail={(detail, afterChange) => (
        <ReviewActions key={detail.caseId} detail={detail} onDone={afterChange} />
      )}
    />
  )
}
