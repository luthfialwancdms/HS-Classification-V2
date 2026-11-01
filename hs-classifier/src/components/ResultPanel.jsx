import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, BarChart3, Check, ChevronDown, ChevronRight, ChevronUp, Scale, Search, ShieldCheck, Wallet, X } from 'lucide-react'
import { getRole, requestReview } from '../services/apiClient'

const CONFIDENCE_CLASS = {
  TINGGI: 'success',
  SEDANG: 'warning',
  RENDAH: 'danger',
}

const DECISION_CLASS = {
  RETAINED: 'success',
  ELIMINATED: 'danger',
  UNRESOLVED: 'warning',
}

const STATUS_LABEL = {
  DETERMINED: 'Ditentukan',
  NEED_CLARIFICATION: 'Perlu klarifikasi',
  NEED_EXPERT_REVIEW: 'Perlu review ahli',
  REJECTED: 'Ditolak ahli',
}

const fmtRate = r => (r.unit === 'PERCENT' ? `${r.value}%` : `${r.value} (${r.unit})`)

const EXPERT_BADGE = {
  EXPERT_APPROVED: ['Disetujui ahli', 'success'],
  EXPERT_OVERRIDE: ['Dikoreksi ahli (override)', 'info'],
}

function Badge({ tone = 'neutral', children }) {
  return <span className={`badge ${tone}`}><span className="dot" aria-hidden="true" />{children}</span>
}

/** "Minta Expert Review" — only for the case owner's writer roles; the API enforces it too. */
function RequestReviewBox({ caseId, onChanged }) {
  const [open, setOpen] = useState(false)
  const [reason, setReason] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [done, setDone] = useState(false)

  if (!['Admin', 'Classifier'].includes(getRole())) return null
  if (done) return <p role="status" className="notice success"><Check size={16} aria-hidden="true" /><span>Case dikirim ke antrian review ahli.</span></p>

  const submit = async () => {
    setBusy(true); setError(null)
    try { await requestReview(caseId, reason); setDone(true); onChanged?.() }
    catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }

  if (!open) {
    return (
      <div className="review-request">
        <button onClick={() => setOpen(true)} className="link-button">
          <ShieldCheck size={15} aria-hidden="true" /> Minta Expert Review <ChevronRight size={13} aria-hidden="true" />
        </button>
      </div>
    )
  }

  return (
    <div className="review-request">
      <label className="field">
        <span>Alasan meminta review ahli</span>
        <textarea rows={2} value={reason} onChange={e => setReason(e.target.value)}
          placeholder="mis. ragu antara subpos smartphone dan telepon lainnya" />
      </label>
      {error && <p role="alert" className="notice danger"><AlertTriangle size={16} aria-hidden="true" /><span>{error}</span></p>}
      <div className="result-actions">
        <button onClick={() => setOpen(false)} className="outline-button">Batal</button>
        <button onClick={submit} disabled={busy || reason.trim().length < 5} className="primary-button">
          {busy ? 'Mengirim…' : 'Kirim ke ahli'}
        </button>
      </div>
    </div>
  )
}

export default function ResultPanel({ result, isLoading, onChanged, onClear, focusOnResult = false }) {
  // Hasil baru (objek baru dari API) otomatis kembali terbuka penuh: status kecil
  // hanya terikat pada objek hasil tertentu, tanpa setState di dalam effect.
  const [collapsedFor, setCollapsedFor] = useState(null)
  const collapsed = collapsedFor === result
  const toggleCollapsed = () => setCollapsedFor(collapsed ? null : result)
  const titleRef = useRef(null)
  const sectionRef = useRef(null)

  useEffect(() => {
    if (!focusOnResult || !result || result.error || isLoading) return
    const reduce = matchMedia('(prefers-reduced-motion: reduce)').matches
    sectionRef.current?.scrollIntoView({ behavior: reduce ? 'auto' : 'smooth', block: 'start' })
    titleRef.current?.focus({ preventScroll: true })
  }, [result, isLoading, focusOnResult])

  let body
  if (isLoading) {
    body = (
      <div role="status" aria-live="polite" className="empty-state">
        <div className="spinner" aria-hidden="true" />
        <h3>Menjalankan KUM HS 1–6 terhadap database BTKI...</h3>
        <p>Ekstraksi fakta → kandidat → aturan → validasi → tarif</p>
      </div>
    )
  } else if (!result) {
    body = (
      <div className="empty-state">
        <div className="empty-icon" aria-hidden="true"><Search size={26} /></div>
        <h3>Belum ada analisis</h3>
        <p>Isi form lalu klik <strong>Analisis HS Code</strong>.</p>
        <div className="empty-hint">Sistem akan menjalankan reasoning berdasarkan KUM HS 1–6</div>
      </div>
    )
  } else if (result.error) {
    body = (
      <div role="alert" className="result-content">
        <div className="notice danger">
          <AlertTriangle size={16} aria-hidden="true" />
          <span><strong>Terjadi Kesalahan.</strong> {result.error}</span>
        </div>
      </div>
    )
  } else {
    body = <ResultBody result={result} onChanged={onChanged} />
  }

  return (
    <section className="panel result-panel" ref={sectionRef} aria-live="polite">
      <div className="panel-heading">
        <div>
          <div className="eyebrow">AI ANALYSIS</div>
          <h2 id="result-title" tabIndex={-1} ref={titleRef}>Hasil Analisis</h2>
          <p>Rekomendasi kode HS berdasarkan BTKI 2022 dan KUM HS.</p>
        </div>
        {onClear && (
          <div className="result-panel-actions">
            <button type="button" className="outline-button" aria-expanded={!collapsed} aria-controls="result-body"
              onClick={toggleCollapsed}>
              {collapsed ? <ChevronDown size={15} aria-hidden="true" /> : <ChevronUp size={15} aria-hidden="true" />}
              {collapsed ? 'Perbesar' : 'Perkecil'}
            </button>
            <button type="button" className="outline-button result-clear" onClick={onClear}>
              <X size={15} aria-hidden="true" /> Hapus hasil
            </button>
          </div>
        )}
      </div>

      {collapsed && result && !result.error && <CompactSummary result={result} />}

      <div id="result-body" hidden={collapsed}>{body}</div>
    </section>
  )
}

// Satu baris ringkas saat panel diperkecil: kode HS, badge confidence, deskripsi singkat.
function CompactSummary({ result }) {
  const tone = CONFIDENCE_CLASS[result.confidence] || 'warning'
  const desc = result.hs_code ? result.uraian : (result.alasan_expert_review || (result.status === 'REJECTED' ? 'Ditolak oleh ahli kepabeanan' : 'Belum dapat ditentukan otomatis'))
  return (
    <div className="result-compact">
      <span className="compact-code">{result.hs_code || (STATUS_LABEL[result.status] || result.status)}</span>
      {result.hs_code && <Badge tone={tone}>Confidence: {result.confidence}</Badge>}
      {desc && <span className="compact-desc">{desc}</span>}
    </div>
  )
}

function ResultBody({ result, onChanged }) {
  const tone = CONFIDENCE_CLASS[result.confidence] || 'warning'
  const statusTone = result.status === 'REJECTED' ? 'neutral'
    : result.status === 'NEED_EXPERT_REVIEW' ? 'danger'
    : result.status === 'NEED_CLARIFICATION' ? 'warning' : 'success'
  const correction = result.corrections?.at(-1)
  const expert = correction && EXPERT_BADGE[correction.action]

  return (
    <div className="result-content">
      {/* No final code: the engine refused to guess (Req 16.7), or an expert rejected it */}
      {!result.hs_code ? (
        <div className="result-hero">
          <div>
            <div className="result-label">KODE HS REKOMENDASI</div>
            <div className="result-unresolved">
              {result.status === 'REJECTED' ? 'Ditolak oleh ahli kepabeanan' : 'Belum dapat ditentukan otomatis'}
            </div>
            {result.status === 'REJECTED' && result.corrections?.length > 0 && (
              <p>Alasan: {result.corrections.at(-1).reason}</p>
            )}
            {result.candidateCodes?.length > 0 && (
              <p>Kandidat untuk ditinjau ahli: <span className="mono">{result.candidateCodes.join(', ')}</span></p>
            )}
          </div>
          <Badge tone={statusTone}>{STATUS_LABEL[result.status] || result.status}</Badge>
        </div>
      ) : (
        <div className="result-hero">
          <div>
            <div className="result-label">KODE HS REKOMENDASI</div>
            <div className="hs-code">{result.hs_code}</div>
            <p>{result.uraian}</p>
          </div>
          <Badge tone={tone}>Confidence: {result.confidence}{result.confidenceScore != null && ` · ${result.confidenceScore}/100`}</Badge>
        </div>
      )}

      <div className="result-meta">
        <span><Check size={14} aria-hidden="true" /> Status: <strong>{STATUS_LABEL[result.status] || result.status}</strong></span>
        {result.reasoningChain?.length > 0 && (
          <span>{result.reasoningChain.length} langkah reasoning · {result.eliminatedCount ?? 0} kandidat dieliminasi</span>
        )}
      </div>

      {expert && (
        <div>
          <Badge tone={expert[1]}>{expert[0]}</Badge>
        </div>
      )}

      {result.tariffs && <TariffSection tariffs={result.tariffs} />}

      {result.confidenceDetail && <ConfidenceSection detail={result.confidenceDetail} score={result.confidenceScore} />}

      {/* Expert Review Flag */}
      {result.perlu_expert_review && (
        <div role="alert" className="notice danger">
          <ShieldCheck size={16} aria-hidden="true" />
          <span><strong>Perlu Expert Review.</strong> {result.alasan_expert_review}</span>
        </div>
      )}

      {/* Reasoning chain viewer (Req 17.3 traceability) */}
      {result.reasoningChain?.length > 0 && (
        <div className="reasoning">
          <div className="subheading">
            <div><Scale size={16} aria-hidden="true" /> Reasoning KUM HS <span className="ai-pill">AI SUGGESTION</span></div>
          </div>
          <ol className="reason-list">
            {result.reasoningChain.map(step => (
              <li key={step.eventOrder}>
                <details className="reason-details">
                  <summary className="reason-step">
                    <span className="step-number">{step.eventOrder}</span>
                    <div>
                      <span className="rule-chip">{step.ruleApplied}</span>{' '}
                      <Badge tone={DECISION_CLASS[step.decision?.toUpperCase()] || 'neutral'}>{step.decision}</Badge>
                    </div>
                    <ChevronDown size={15} className="muted" aria-hidden="true" />
                  </summary>
                  <div className="reason-body">
                    <p>{step.reasoning}</p>
                    {step.sourceIds?.length > 0 && (
                      <ul className="chips" aria-label="Sumber hukum">
                        {step.sourceIds.map(s => (
                          <li key={s} className="chip">{s}</li>
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

      {result.status === 'DETERMINED' && result.caseId && (
        <RequestReviewBox key={result.caseId} caseId={result.caseId} onChanged={onChanged} />
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
    <div className="result-section">
      <div className="subheading">
        <div>
          <BarChart3 size={16} aria-hidden="true" /> Rincian Confidence
          {score != null && <span className="mono">{score}/100</span>}
        </div>
      </div>
      {detail.components?.length > 0 && (
        <div className="table-scroll">
          <table className="data-table">
            <caption>Komponen perhitungan confidence</caption>
            <thead>
              <tr><th scope="col">Komponen</th><th scope="col" className="num">Poin / Bobot</th></tr>
            </thead>
            <tbody>
              {detail.components.map(c => (
                <tr key={c.name}>
                  <td>{c.name}</td>
                  <td className="num mono">{c.points} / {c.weight}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {detail.caps?.length > 0 && (
        <ul className="flag-list">
          {detail.caps.map(c => <li key={c} className="hint">Batas: {c}</li>)}
        </ul>
      )}
      {flags.length > 0 && (
        <ul className="chips" aria-label="Risk flags">
          {flags.map(f => (
            <li key={f} title={f}><Badge tone="warning">{FLAG_LABEL[f] || f}</Badge></li>
          ))}
        </ul>
      )}
    </div>
  )
}

// ─── Tariffs (Req 18.4, 19.4, 20.4) ──────────────────────────────────────────

const MISSING = 'Tidak tersedia di database untuk kode ini'

function TariffCard({ label, rate }) {
  return (
    <div className="tariff-card">
      <span>{label}</span>
      <strong>{rate ? fmtRate(rate) : '—'}</strong>
      <small>{rate ? (rate.sourceId || 'Database BTKI') : MISSING}</small>
    </div>
  )
}

function TariffSection({ tariffs }) {
  const fta = tariffs.ftaRates || []
  return (
    <div className="result-section">
      <div className="subheading">
        <div><Wallet size={16} aria-hidden="true" /> Tarif &amp; Pajak Impor</div>
      </div>
      <div className="tariff-grid">
        <TariffCard label="Bea Masuk MFN" rate={tariffs.mfn} />
        <TariffCard label="PPN" rate={tariffs.ppn} />
        <TariffCard label="PPh 22 Impor" rate={tariffs.pph22} />
        <div className="tariff-card">
          <span>FTA tersedia</span>
          <strong>{fta.length}</strong>
          <small>{fta.length > 0 ? 'skema preferensi' : 'Tidak ada tarif FTA di database untuk kode ini'}</small>
        </div>
      </div>
      {tariffs.taxDisclaimer && <p className="hint">{tariffs.taxDisclaimer}</p>}

      <div className="subheading"><div>Tarif FTA yang Tersedia</div></div>
      {fta.length > 0 ? (
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th scope="col">Skema</th>
                <th scope="col">Negara Mitra</th>
                <th scope="col" className="num">Tarif</th>
              </tr>
            </thead>
            <tbody>
              {fta.map(f => (
                <tr key={`${f.schemeCode}-${f.partnerCountry}`}>
                  <td className="mono">{f.schemeCode}</td>
                  <td>{f.partnerCountry}</td>
                  <td className="num">{fmtRate(f)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="hint">Tidak ada tarif FTA di database untuk kode ini</p>
      )}
      {/* Always shown: availability ≠ eligibility (Req 19.4) */}
      <div className="notice">
        <AlertTriangle size={16} aria-hidden="true" />
        <span>{tariffs.ftaDisclaimer || 'Tarif FTA hanya referensi. Kelayakan bergantung pada pemenuhan ketentuan asal barang (SKA/COO) dan persyaratan skema.'}</span>
      </div>
    </div>
  )
}
