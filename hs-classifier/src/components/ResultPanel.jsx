import { useState } from 'react'
import { getRole, requestReview } from '../services/apiClient'

const CONFIDENCE_COLOR = {
  TINGGI: 'bg-green-100 text-green-800 border-green-300',
  SEDANG: 'bg-yellow-100 text-yellow-800 border-yellow-300',
  RENDAH: 'bg-red-100 text-red-800 border-red-300',
}

const DECISION_COLOR = {
  RETAINED:   'text-green-700 bg-green-50',
  ELIMINATED: 'text-red-700 bg-red-50',
  UNRESOLVED: 'text-amber-700 bg-amber-50',
}

const fmtRate = r => (r.unit === 'PERCENT' ? `${r.value}%` : `${r.value} (${r.unit})`)

const EXPERT_BADGE = {
  EXPERT_APPROVED: ['Disetujui ahli', 'bg-green-100 text-green-800'],
  EXPERT_OVERRIDE: ['Dikoreksi ahli (override)', 'bg-purple-100 text-purple-800'],
}

/** "Minta Expert Review" — only for the case owner's writer roles; the API enforces it too. */
function RequestReviewBox({ caseId, onChanged }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [done, setDone] = useState(false)

  if (!['Admin', 'Classifier'].includes(getRole())) return null
  if (done) return <p role="status" className="text-sm text-green-700 bg-green-50 rounded-xl p-3">Case dikirim ke antrian review ahli.</p>

  const submit = async () => {
    setBusy(true); setError(null)
    try { await requestReview(caseId, reason); setDone(true); onChanged?.() }
    catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-4">
      {!open ? (
        <button onClick={() => setOpen(true)} className="text-sm font-medium text-blue-700 hover:underline">
          👨‍⚖️ Minta Expert Review
        </button>
      ) : (
        <div className="flex flex-col gap-2">
          <label htmlFor={`rr-${caseId}`} className="text-sm font-medium text-gray-700">Alasan meminta review ahli</label>
          <textarea id={`rr-${caseId}`} rows={2} value={reason} onChange={e => setReason(e.target.value)}
            placeholder="mis. ragu antara subpos smartphone dan telepon lainnya"
            className="w-full text-sm border border-gray-300 rounded p-2" />
          {error && <p role="alert" className="text-xs text-red-600">{error}</p>}
          <div className="flex gap-2">
            <button onClick={submit} disabled={busy || reason.trim().length < 5}
              className="px-3 py-1.5 text-sm rounded-lg font-medium bg-blue-600 text-white disabled:opacity-50">
              {busy ? 'Mengirim…' : 'Kirim ke ahli'}
            </button>
            <button onClick={() => setOpen(false)} className="px-3 py-1.5 text-sm rounded-lg border border-gray-300">Batal</button>
          </div>
        </div>
      )}
    </div>
  )
}

export default function ResultPanel({ result, isLoading, onChanged }) {
  if (isLoading) {
    return (
      <div role="status" aria-live="polite" className="bg-white rounded-2xl border border-gray-200 shadow-sm p-8 flex flex-col items-center justify-center gap-3 min-h-64">
        <div className="w-10 h-10 border-4 border-blue-600 border-t-transparent rounded-full animate-spin" aria-hidden="true" />
        <p className="text-sm text-gray-500">Menjalankan KUM HS 1–6 terhadap database BTKI...</p>
        <p className="text-xs text-gray-400">Ekstraksi fakta → kandidat → aturan → validasi → tarif</p>
      </div>
    )
  }

  if (!result) {
    return (
      <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-8 flex flex-col items-center justify-center gap-3 min-h-64 text-center">
        <div className="w-16 h-16 bg-gray-100 rounded-full flex items-center justify-center text-3xl" aria-hidden="true">🔍</div>
        <p className="text-gray-500 text-sm">Isi form di sebelah kiri lalu klik <strong>Analisis HS Code</strong></p>
        <p className="text-gray-400 text-xs">Sistem akan menjalankan reasoning berdasarkan KUM HS 1–6</p>
      </div>
    )
  }

  if (result.error) {
    return (
      <div role="alert" className="bg-white rounded-2xl border border-red-200 shadow-sm p-6">
        <div className="flex items-center gap-2 text-red-600 font-semibold mb-2">
          <span aria-hidden="true">⚠️</span> Terjadi Kesalahan
        </div>
        <p className="text-sm text-red-500">{result.error}</p>
      </div>
    )
  }

  const confidenceClass = CONFIDENCE_COLOR[result.confidence] || CONFIDENCE_COLOR.SEDANG

  return (
    <div className="flex flex-col gap-4">
      {/* No final code: the engine refused to guess (Req 16.7), or an expert rejected it */}
      {!result.hs_code && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
          <p className="text-xs text-gray-500 uppercase tracking-wide mb-1">Rekomendasi HS Code</p>
          <p className="text-lg font-semibold text-gray-700">
            {result.status === 'REJECTED' ? 'Ditolak oleh ahli kepabeanan' : 'Belum dapat ditentukan otomatis'}
          </p>
          {result.status === 'REJECTED' && result.corrections?.length > 0 && (
            <p className="text-sm text-gray-600 mt-2">Alasan: {result.corrections.at(-1).reason}</p>
          )}
          {result.candidateCodes?.length > 0 && (
            <p className="text-sm text-gray-600 mt-2">
              Kandidat untuk ditinjau ahli: <span className="font-mono">{result.candidateCodes.join(', ')}</span>
            </p>
          )}
        </div>
      )}

      {/* Primary Result */}
      {result.hs_code && <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
        <div className="flex items-start justify-between gap-4 mb-4">
          <div>
            <p className="text-xs text-gray-500 uppercase tracking-wide mb-1">Rekomendasi HS Code</p>
            <p className="text-3xl font-bold text-blue-700 tracking-wider">{result.hs_code}</p>
          </div>
          <span className={`px-3 py-1 rounded-full text-xs font-semibold border ${confidenceClass}`}>
            Confidence: {result.confidence}{result.confidenceScore != null && ` · ${result.confidenceScore}/100`}
          </span>
        </div>
        <p className="text-xs text-gray-500 mb-1">Uraian resmi BTKI</p>
        <p className="text-sm font-medium text-gray-800">{result.uraian}</p>
        {EXPERT_BADGE[result.corrections?.at(-1)?.action] && (
          <span className={`inline-block mt-3 text-xs px-2 py-0.5 rounded-full font-medium ${EXPERT_BADGE[result.corrections.at(-1).action][1]}`}>
            {EXPERT_BADGE[result.corrections.at(-1).action][0]}
          </span>
        )}
        {result.reasoningChain?.length > 0 && (
          <p className="text-xs text-gray-500 mt-3">
            {result.reasoningChain.length} langkah reasoning · {result.eliminatedCount ?? 0} kandidat dieliminasi
          </p>
        )}
      </div>}

      {result.confidenceDetail && <ConfidenceSection detail={result.confidenceDetail} score={result.confidenceScore} />}

      {result.status === 'DETERMINED' && result.caseId && (
        <RequestReviewBox key={result.caseId} caseId={result.caseId} onChanged={onChanged} />
      )}

      {/* Expert Review Flag */}
      {result.perlu_expert_review && (
        <div role="alert" className="bg-red-50 rounded-2xl border border-red-200 p-4 flex items-center gap-3">
          <span className="text-2xl" aria-hidden="true">👨‍⚖️</span>
          <div>
            <p className="text-sm font-semibold text-red-700">Perlu Expert Review</p>
            <p className="text-xs text-red-500">{result.alasan_expert_review}</p>
          </div>
        </div>
      )}

      {result.tariffs && <TariffSection tariffs={result.tariffs} />}

      {/* Reasoning chain viewer (Req 17.3 traceability) */}
      {result.reasoningChain?.length > 0 && (
        <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
          <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
            <span aria-hidden="true">⚖️</span> Reasoning KUM HS
          </h3>
          <ol className="flex flex-col gap-2">
            {result.reasoningChain.map(step => (
              <li key={step.eventOrder}>
                <details className="group border border-gray-100 rounded-lg">
                  <summary className="flex items-center gap-3 p-3 cursor-pointer list-none">
                    <span className="text-xs text-gray-400 w-5">{step.eventOrder}.</span>
                    <span className="text-xs font-bold text-blue-600 bg-blue-50 px-2 py-1 rounded">{step.ruleApplied}</span>
                    <span className={`text-xs font-medium px-2 py-0.5 rounded ${DECISION_COLOR[step.decision?.toUpperCase()] || 'text-gray-600 bg-gray-50'}`}>
                      {step.decision}
                    </span>
                    <span className="ml-auto text-xs text-gray-400 group-open:rotate-90 transition-transform" aria-hidden="true">▶</span>
                  </summary>
                  <div className="px-3 pb-3 pl-11">
                    <p className="text-sm text-gray-600 leading-relaxed">{step.reasoning}</p>
                    {step.sourceIds?.length > 0 && (
                      <ul className="mt-2 flex flex-wrap gap-1" aria-label="Sumber hukum">
                        {step.sourceIds.map(s => (
                          <li key={s} className="text-[11px] font-mono text-gray-500 bg-gray-100 px-1.5 py-0.5 rounded">{s}</li>
                        ))}
                      </ul>
                    )}
                  </div>
                </details>
              </li>
            ))}
          </ol>
        </div>
      )}
    </div>
  )
}

// ─── Confidence breakdown + risk flags (V2 doc: weighted model with caps) ─────

const FLAG_LABEL = {
  MISSING_DECISIVE_FACT:          'Fakta penentu belum lengkap',
  LEGAL_NOTES_NOT_REVIEWED:       'Catatan Bagian/Bab belum diuji (data belum tersedia)',
  EXPLANATORY_NOTES_NOT_REVIEWED: 'Explanatory Notes belum diuji (data belum tersedia)',
  MULTIPLE_EQUAL_CANDIDATES:      'Beberapa kandidat setara (KUM HS 3c)',
  GRI_4_USED:                     'Memakai KUM HS 4 (barang paling mirip)',
  AI_TERM_SUGGESTION:             'Istilah barang diterjemahkan AI',
  EXPERT_OVERRIDE:                'Kode dikoreksi ahli',
}

function ConfidenceSection({ detail, score }) {
  const flags = detail.riskFlags || []
  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
      <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
        <span aria-hidden="true">📊</span> Rincian Confidence {score != null && <span className="font-mono">{score}/100</span>}
      </h3>
      {detail.components?.length > 0 && (
        <table className="w-full text-xs mb-3">
          <thead><tr className="text-left text-gray-500">
            <th scope="col" className="py-1 font-medium">Komponen</th>
            <th scope="col" className="py-1 font-medium text-right">Poin / Bobot</th>
          </tr></thead>
          <tbody>
            {detail.components.map(c => (
              <tr key={c.name} className="border-t border-gray-50">
                <td className="py-1">{c.name}</td>
                <td className="py-1 text-right font-mono">{c.points} / {c.weight}</td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
      {detail.caps?.length > 0 && (
        <ul className="text-xs text-gray-600 mb-2 list-disc pl-4">
          {detail.caps.map(c => <li key={c}>Batas: {c}</li>)}
        </ul>
      )}
      {flags.length > 0 && (
        <ul className="flex flex-wrap gap-1" aria-label="Risk flags">
          {flags.map(f => (
            <li key={f} title={f} className="text-[11px] px-2 py-0.5 rounded-full bg-amber-50 text-amber-800 border border-amber-200">
              ⚠ {FLAG_LABEL[f] || f}
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ─── Tariffs (Req 18.4, 19.4, 20.4) ──────────────────────────────────────────

function RateRow({ label, rate, missing }) {
  return (
    <div className="flex items-baseline justify-between gap-3 py-1.5 border-b border-gray-50 last:border-0">
      <span className="text-sm text-gray-600">{label}</span>
      {rate ? (
        <span className="text-right">
          <span className="text-sm font-semibold text-gray-900">{fmtRate(rate)}</span>
          {rate.sourceId && <span className="block text-[11px] font-mono text-gray-400">{rate.sourceId}</span>}
        </span>
      ) : (
        <span className="text-xs text-gray-400 italic">{missing}</span>
      )}
    </div>
  )
}

function TariffSection({ tariffs }) {
  const missing = 'Tidak tersedia di database untuk kode ini'
  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-6">
      <h3 className="text-sm font-semibold text-gray-700 mb-3 flex items-center gap-2">
        <span aria-hidden="true">💰</span> Tarif &amp; Pajak Impor
      </h3>
      <RateRow label="Bea Masuk MFN" rate={tariffs.mfn} missing={missing} />
      <RateRow label="PPN" rate={tariffs.ppn} missing={missing} />
      <RateRow label="PPh 22 Impor" rate={tariffs.pph22} missing={missing} />
      {tariffs.taxDisclaimer && <p className="text-xs text-gray-500 mt-2">{tariffs.taxDisclaimer}</p>}

      <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mt-5 mb-2">Tarif FTA yang Tersedia</h4>
      {tariffs.ftaRates?.length > 0 ? (
        <table className="w-full text-xs">
          <thead>
            <tr className="text-left text-gray-500">
              <th scope="col" className="py-1 font-medium">Skema</th>
              <th scope="col" className="py-1 font-medium">Negara Mitra</th>
              <th scope="col" className="py-1 font-medium text-right">Tarif</th>
            </tr>
          </thead>
          <tbody>
            {tariffs.ftaRates.map(f => (
              <tr key={`${f.schemeCode}-${f.partnerCountry}`} className="border-t border-gray-50">
                <td className="py-1 font-mono">{f.schemeCode}</td>
                <td className="py-1">{f.partnerCountry}</td>
                <td className="py-1 text-right font-semibold">{fmtRate(f)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-gray-400 italic">Tidak ada tarif FTA di database untuk kode ini</p>
      )}
      {/* Always shown: availability ≠ eligibility (Req 19.4) */}
      <p className="text-xs text-amber-700 bg-amber-50 rounded p-2 mt-2">
        {tariffs.ftaDisclaimer || 'Tarif FTA hanya referensi. Kelayakan bergantung pada pemenuhan ketentuan asal barang (SKA/COO) dan persyaratan skema.'}
      </p>
    </div>
  )
}
