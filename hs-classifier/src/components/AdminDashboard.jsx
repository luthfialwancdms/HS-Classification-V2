import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, BookOpen, Bot, Check, ChevronDown, Download, FileCheck2, FileText, UploadCloud } from 'lucide-react'
import { apiFetch } from '../services/apiClient'
import AiStatusPanel from './AiStatusPanel'

// Admin Data Center (Task 18.2) — thin UI over the existing /api/admin endpoints.

const TABS = [
  ['versions', 'Versi Nomenklatur', BookOpen],
  ['docx',     'Upload Dokumen', UploadCloud],
  ['reports',  'Laporan Validasi', FileCheck2],
  ['ambiguity','Antrian Ambiguitas', AlertTriangle],
  ['template', 'Template CSV/Excel', FileText],
  ['ai',       'Status AI', Bot],
]

const cx = (...parts) => parts.filter(Boolean).join(' ')

/** `files` is one File or an array; the BTKI endpoints read the field "files", the others "file". */
function upload(path, files, field = 'file') {
  const body = new FormData()
  for (const f of [].concat(files)) body.append(field, f)
  return apiFetch(path, { method: 'POST', body })
}

/** Runs an async action with loading/error/message state shared by every panel. */
function useAction() {
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState(null)
  const [notice, setNotice] = useState(null)
  const run = async (fn) => {
    setBusy(true); setError(null); setNotice(null)
    try { return await fn() }
    catch (e) { setError(e.message) }
    finally { setBusy(false) }
  }
  return { busy, error, notice, setNotice, run }
}

function Feedback({ error, notice }) {
  if (error)  return <p role="alert" className="notice danger"><AlertTriangle size={16} aria-hidden="true" /><span>{error}</span></p>
  if (notice) return <p role="status" className="notice success"><Check size={16} aria-hidden="true" /><span>{notice}</span></p>
  return null
}

/** Orientasi nav admin mengikuti breakpoint v0: vertikal >1050px, horizontal ≤1050px. */
function useNarrow(query = '(max-width: 1050px)') {
  const [narrow, setNarrow] = useState(() => matchMedia(query).matches)
  useEffect(() => {
    const mq = matchMedia(query)
    const onChange = (e) => setNarrow(e.matches)
    mq.addEventListener('change', onChange)
    return () => mq.removeEventListener('change', onChange)
  }, [query])
  return narrow
}

export default function AdminDashboard() {
  const [tab, setTab] = useState('versions')
  const [versions, setVersions] = useState([])
  const [loadError, setLoadError] = useState(null)
  const [batchId, setBatchId] = useState(null)

  const tabRefs = useRef([])
  const tabIds = TABS.map(([id]) => id)
  const narrow = useNarrow()

  const reload = () => apiFetch('/api/admin/versions').then(setVersions, e => setLoadError(e.message))
  useEffect(() => { reload() }, [])
  const openBatch = (id) => { setBatchId(id); setTab('reports') }

  // Panah atas/bawah (vertikal) + kiri/kanan (saat nav jadi horizontal <1050px) + Home/End.
  const onTabKeyDown = (e) => {
    const step = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[e.key]
    let next = null
    if (step) next = (tabIds.indexOf(tab) + step + tabIds.length) % tabIds.length
    else if (e.key === 'Home') next = 0
    else if (e.key === 'End') next = tabIds.length - 1
    if (next === null) return
    e.preventDefault()
    setTab(tabIds[next])
    tabRefs.current[next]?.focus()
  }

  return (
    <div className="view">
      <div className="page-title">
        <div>
          <div className="eyebrow">ADMINISTRATION</div>
          <h1>Admin Data Center</h1>
          <p>Kelola nomenklatur, tarif, dan kualitas data BTKI.</p>
        </div>
      </div>

      {loadError && <p role="alert" className="notice danger"><span>{loadError}</span></p>}

      <div className="admin-layout">
        <div className="admin-nav" role="tablist" aria-orientation={narrow ? 'horizontal' : 'vertical'} aria-label="Bagian Admin Data Center"
          onKeyDown={onTabKeyDown}>
          {TABS.map(([id, label, Icon], i) => (
            <button key={id} role="tab" id={`admin-tab-${id}`} aria-controls={`admin-panel-${id}`}
              aria-selected={tab === id} aria-label={label} tabIndex={tab === id ? 0 : -1}
              ref={el => { tabRefs.current[i] = el }}
              onClick={() => setTab(id)}
              className={tab === id ? 'active' : undefined}>
              <Icon size={16} aria-hidden="true" /><span>{label}</span>
            </button>
          ))}
        </div>

        <div className="admin-content" role="tabpanel" id={`admin-panel-${tab}`} aria-labelledby={`admin-tab-${tab}`}>
          {tab === 'versions' && <VersionsPanel versions={versions} reload={reload} />}
          {tab === 'docx'     && <DocxPanel versions={versions} reload={reload} openBatch={openBatch} />}
          {tab === 'reports'  && <BatchesPanel versions={versions} reload={reload} batchId={batchId} setBatchId={setBatchId} />}
          {tab === 'ambiguity'&& <BatchesPanel versions={versions} reload={reload} batchId={batchId} setBatchId={setBatchId} pendingOnly />}
          {tab === 'template' && <TemplatePanel versions={versions} reload={reload} />}
          {tab === 'ai'       && <AiStatusPanel />}
        </div>
      </div>
    </div>
  )
}

// ─── Versions (Req 4.4, 4.6, 4.7) ────────────────────────────────────────────

const STATE_TONE = {
  ACTIVE: 'aktif', INDEXED: 'info', COMMITTED: 'info', DRYRUNFAILED: 'danger',
}
const stateTone = (v) => STATE_TONE[(v.isActive ? 'ACTIVE' : v.state).toUpperCase()] || 'inactive'

const DIFF_KINDS = [
  ['added',   'Ditambah (hanya di versi ini)'],
  ['removed', 'Dihapus (hanya di versi aktif)'],
  ['changed', 'Uraian berubah'],
  ['ocrOnly', 'Beda karena salah ketik OCR (l/I/1, 0/O)'],
]

/** What activating a version would change versus the active one: counts per chapter plus the entries. */
function DiffView({ version, onClose }) {
  const [chapter, setChapter] = useState('')
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    let alive = true
    apiFetch(`/api/admin/versions/${version.id}/diff${chapter ? `?chapter=${chapter}` : ''}`)
      .then(d => alive && (setData(d), setError(null)), e => alive && setError(e.message))
    return () => { alive = false }
  }, [version.id, chapter])

  const c = data?.counts
  return (
    <div className="result-section">
      <div className="subheading">
        <div>Beda "{version.name}" vs versi aktif{data ? ` "${data.baseline.name}"` : ''}</div>
        <button className="outline-button" onClick={onClose}>Tutup</button>
      </div>
      {error && <p role="alert" className="notice danger"><span>{error}</span></p>}
      {!data && !error && <p className="hint">Menghitung...</p>}
      {data && (
        <>
          <div className="metric-grid">
            {[['Versi aktif', c.baseline], ['Versi ini', c.candidate], ['Sama', c.unchanged],
              ['Ditambah', c.added], ['Dihapus', c.removed], ['Uraian berubah', c.changed]].map(([k, v]) => (
              <div className="metric" key={k}>
                <span>{k}</span><strong>{v}</strong>
              </div>
            ))}
          </div>
          <p className="hint">Beda OCR saja: {c.ocrOnly}. Spasi, tanda baca, dan huruf besar/kecil tidak dihitung sebagai beda.</p>

          <label className="field">
            <span>Tampilkan Bab</span>
            <div className="select-wrap">
              <select id="diff-chapter" value={chapter} onChange={e => setChapter(e.target.value)}>
                <option value="">Semua Bab</option>
                {data.perChapter.map(p => (
                  <option key={p.chapter} value={p.chapter}>
                    {p.chapter} (+{p.added} −{p.removed} ~{p.changed + p.ocrOnly})
                  </option>
                ))}
              </select>
              <ChevronDown size={15} aria-hidden="true" />
            </div>
          </label>

          {DIFF_KINDS.map(([key, label]) => (
            <details key={key} className="diff-block">
              <summary>
                {label}: {data[key].total}{data[key].truncated && ` (menampilkan ${data[key].items.length})`}
              </summary>
              <div className="table-scroll">
                <table className="data-table">
                  <thead>
                    <tr>
                      <th scope="col">Kode</th><th scope="col">Tingkat</th>
                      <th scope="col">Versi aktif</th><th scope="col">Versi ini</th>
                    </tr>
                  </thead>
                  <tbody>
                    {data[key].items.map(e => (
                      <tr key={e.code}>
                        <td className="mono">{e.code}</td><td>{e.level}</td>
                        <td>{e.before}</td><td>{e.after}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </details>
          ))}
        </>
      )}
    </div>
  )
}

const SIDE_STATUS = { DETERMINED: 'Ditetapkan', NEED_CLARIFICATION: 'Perlu klarifikasi', NEED_EXPERT_REVIEW: 'Review ahli', ERROR: 'Gagal validasi', EXCEPTION: 'Error' }
const sideText = (s) => s.finalCode || SIDE_STATUS[s.status] || s.status

/** Same cases on the active version and on a draft; saves nothing and does not call the AI. */
function ShadowView({ version, onClose }) {
  const [custom, setCustom] = useState('')
  const [data, setData] = useState(null)
  const { busy, error, run } = useAction()

  const start = () => run(async () => {
    setData(null)
    const cases = custom.trim() ? JSON.parse(custom) : undefined
    setData(await apiFetch(`/api/admin/versions/${version.id}/shadow-run`, { method: 'POST', body: JSON.stringify({ cases }) }))
  })

  return (
    <div className="result-section">
      <div className="subheading">
        <div>Shadow run "{version.name}" vs versi aktif</div>
        <button className="outline-button" onClick={onClose}>Tutup</button>
      </div>
      <p className="hint">
        Kasus yang sama dijalankan di versi aktif dan di draft ini. Tidak ada yang disimpan, kuota tidak terpakai, dan AI tidak dipanggil,
        jadi perbedaan datang dari isi data (termasuk EN dan Catatan).
      </p>
      <details className="diff-block">
        <summary>Kasus sendiri (JSON, opsional; kosong = daftar bawaan)</summary>
        <label className="field">
          <span>Contoh: [{'{'}"name":"Pompa","facts":{'{'}"generic_name":"pompa"{'}'}{'}'}]</span>
          <textarea id="shadow-cases" rows={5} value={custom} onChange={e => setCustom(e.target.value)} className="mono" />
        </label>
      </details>
      <button className="primary-button" disabled={busy} onClick={start}>{busy ? 'Menjalankan...' : 'Jalankan shadow run'}</button>
      <Feedback error={error} />
      {data && (
        <>
          <p className="hint" role="status">{data.changed} dari {data.total} kasus berubah.</p>
          <div className="table-scroll">
            <table className="data-table">
              <caption>Hasil shadow run</caption>
              <thead>
                <tr>
                  <th scope="col">Kasus</th><th scope="col">Aktif: {data.active.name}</th>
                  <th scope="col">Draft: {data.draft.name}</th><th scope="col">Berubah</th><th scope="col">Catatan yang berperan (draft)</th>
                </tr>
              </thead>
              <tbody>
                {data.rows.map(r => (
                  <tr key={r.name}>
                    <td>{r.name}</td>
                    <td className="mono">{sideText(r.active)}</td>
                    <td className="mono">{sideText(r.draft)}</td>
                    <td className={r.changed ? 'text-warning' : undefined}>{r.changed ? 'Ya' : 'Tidak'}</td>
                    <td>{r.draft.notes.length ? r.draft.notes.join(' | ') : '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  )
}

function VersionsPanel({ versions, reload }) {
  const { busy, error, notice, setNotice, run } = useAction()
  const [diffId, setDiffId] = useState(null)
  const [shadowId, setShadowId] = useState(null)
  const diffVersion = versions.find(v => v.id === diffId)
  const shadowVersion = versions.find(v => v.id === shadowId)
  const act = (id, action) => run(async () => {
    const res = await apiFetch(`/api/admin/versions/${id}/${action}`, { method: 'POST' })
    setNotice(res.message); await reload()
  })

  return (
    <>
      <div className="content-heading">
        <div>
          <h2>Versi Nomenklatur</h2>
          <p>Kelola versi nomenklatur dan status publikasinya.</p>
        </div>
      </div>

      <div className="table-scroll">
        <table className="data-table">
          <caption>Daftar versi BTKI</caption>
          <thead>
            <tr>
              <th scope="col">Nama</th><th scope="col">Edisi</th><th scope="col">Berlaku</th>
              <th scope="col">Status Import</th><th scope="col">Aksi</th>
            </tr>
          </thead>
          <tbody>
            {versions.length === 0 && (
              <tr><td colSpan={5} className="hint">Belum ada versi</td></tr>
            )}
            {versions.map(v => (
              <tr key={v.id}>
                <td>{v.name}</td>
                <td>{v.btkiEdition}</td>
                <td className="mono">{v.effectiveFrom?.slice(0, 10)} – {v.effectiveTo?.slice(0, 10) || '∞'}</td>
                <td><span className={`state ${stateTone(v)}`}>{v.isActive ? 'Active' : v.state}</span></td>
                <td>
                  <div className="row-actions">
                    {v.isActive
                      ? <button className="outline-button" disabled={busy} onClick={() => act(v.id, 'deactivate')}>Nonaktifkan / Rollback</button>
                      : <>
                          <button className="primary-button" disabled={busy || v.state !== 'Indexed'} onClick={() => act(v.id, 'activate')}
                            title={v.state !== 'Indexed' ? 'Hanya versi berstatus Indexed yang dapat diaktifkan' : undefined}>Aktifkan</button>
                          {['Indexed', 'Committed'].includes(v.state) &&
                            <button className="outline-button" onClick={() => setDiffId(v.id)}>Bandingkan</button>}
                          {['Indexed', 'Committed'].includes(v.state) &&
                            <button className="outline-button" onClick={() => setShadowId(v.id)}>Shadow run</button>}
                        </>}
                    {['Indexed', 'Active'].includes(v.state) &&
                      <button className="outline-button" disabled={busy} onClick={() => act(v.id, 'clone')}
                        title="Salin versi ini menjadi draft baru untuk memuat EN dan Catatan">Klon jadi draft</button>}
                  </div>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p className="hint">Mengaktifkan versi menonaktifkan versi lain di rentang tanggal yang sama. Riwayat klasifikasi tetap tersimpan.</p>
      <Feedback error={error} notice={notice} />
      {diffVersion && <DiffView key={diffVersion.id} version={diffVersion} onClose={() => setDiffId(null)} />}
      {shadowVersion && <ShadowView key={shadowVersion.id} version={shadowVersion} onClose={() => setShadowId(null)} />}
      <NewVersionForm reload={reload} />
    </>
  )
}

// "Tambah Versi" — a new version starts at Uploaded; the DOCX is uploaded against it next
function NewVersionForm({ reload }) {
  const empty = { name: '', btkiEdition: '', regulationNumber: '', effectiveFrom: '', effectiveTo: '' }
  const [f, setF] = useState(empty)
  const { busy, error, notice, setNotice, run } = useAction()
  const set = (k) => (e) => setF(prev => ({ ...prev, [k]: e.target.value }))

  const submit = (e) => {
    e.preventDefault()
    run(async () => {
      await apiFetch('/api/admin/versions', { method: 'POST', body: JSON.stringify({
        ...f, effectiveTo: f.effectiveTo || null,
      }) })
      setNotice('Versi dibuat. Lanjutkan upload DOCX ke versi ini.'); setF(empty); await reload()
    })
  }

  const field = (k, label, type = 'text', required = false) => (
    <label className="field" key={k}>
      <span>{label}{required && <em> *</em>}</span>
      <input id={`nv-${k}`} type={type} required={required} value={f[k]} onChange={set(k)} />
    </label>
  )

  return (
    <form onSubmit={submit} className="result-section">
      <div className="subheading"><div>Tambah Versi</div></div>
      <div className="form-grid">
        {field('name', 'Nama', 'text', true)}
        {field('btkiEdition', 'Edisi')}
        {field('regulationNumber', 'No. regulasi')}
        {field('effectiveFrom', 'Berlaku mulai', 'date', true)}
        {field('effectiveTo', 'Berlaku sampai', 'date')}
      </div>
      <button type="submit" className="primary-button" disabled={busy}>Tambah Versi</button>
      <Feedback error={error} notice={notice} />
    </form>
  )
}

// ─── Laporan Validasi & Antrian Ambiguitas (Req 1.4, 1.9, 1.11) ───────────────

const DECISION_TONE = {
  Pending: 'warning', Approved: 'success', Rejected: 'danger',
}

function BatchesPanel({ versions, reload, batchId, setBatchId, pendingOnly = false }) {
  const [versionId, setVersionId] = useState('')
  const [batches, setBatches] = useState([])
  const [listError, setListError] = useState(null)
  const [detail, setDetail] = useState(null)
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    let alive = true
    apiFetch(`/api/admin/import-batches${versionId ? `?versionId=${versionId}` : ''}`).then(
      b => alive && (setBatches(b), setListError(null)), e => alive && setListError(e.message))
    return () => { alive = false }
  }, [versionId, reloadKey])

  useEffect(() => {
    if (!batchId) return
    let alive = true
    apiFetch(`/api/admin/import-batches/${batchId}`).then(d => alive && setDetail(d), e => alive && setDetail({ error: e.message }))
    return () => { alive = false }
  }, [batchId, reloadKey])

  const shown = pendingOnly ? batches.filter(b => b.pending > 0) : batches
  const versionName = (id) => versions.find(v => v.id === id)?.name ?? id
  const refresh = async () => { setReloadKey(k => k + 1); await reload() }

  return (
    <>
      <div className="content-heading">
        <div>
          <h2>{pendingOnly ? 'Antrian Ambiguitas' : 'Laporan Validasi'}</h2>
          <p>{pendingOnly ? 'Node yang menunggu keputusan struktur.' : 'Ringkasan hasil parsing dan validasi impor.'}</p>
        </div>
      </div>

      <div className="master-detail">
        <section>
          <label className="field">
            <span>Filter versi</span>
            <div className="select-wrap">
              <select id="batch-version" value={versionId} onChange={e => setVersionId(e.target.value)}>
                <option value="">Semua versi</option>
                {versions.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
              </select>
              <ChevronDown size={15} aria-hidden="true" />
            </div>
          </label>
          {listError && <p role="alert" className="notice danger"><span>{listError}</span></p>}
          <div className="case-list">
            {shown.length === 0 && (
              <p className="hint">
                {pendingOnly ? 'Tidak ada ambiguitas yang menunggu keputusan' : 'Belum ada laporan validasi'}
              </p>
            )}
            {shown.map(b => (
              <button key={b.id} onClick={() => setBatchId(b.id)} aria-current={batchId === b.id ? 'true' : undefined}
                className={cx('case-card', batchId === b.id && 'selected')}>
                <div className="case-card-top">
                  <span className="case-date">{new Date(b.createdAt).toLocaleString('id-ID')}</span>
                  {b.pending > 0 && <span className="badge warning"><span className="dot" aria-hidden="true" />{b.pending} pending</span>}
                </div>
                <strong>{b.fileName}</strong>
                <div className="case-card-bottom">
                  <span>{versionName(b.versionId)}</span>
                  <span className={b.errorCount ? 'text-danger' : undefined}>{b.errorCount} error</span>
                </div>
              </button>
            ))}
          </div>
        </section>

        <section>
          {!batchId && <p className="hint">Pilih laporan di sebelah kiri.</p>}
          {detail?.error && <p role="alert" className="notice danger"><span>{detail.error}</span></p>}
          {detail && !detail.error && <BatchDetail detail={detail} pendingOnly={pendingOnly} onDecided={refresh} />}
        </section>
      </div>
    </>
  )
}

function BatchDetail({ detail, pendingOnly, onDecided }) {
  const r = detail.report || {}
  const items = pendingOnly ? detail.items.filter(i => i.decision === 'Pending') : detail.items
  const pending = detail.items.filter(i => i.decision === 'Pending').length

  return (
    <div className="detail-card">
      <div className="detail-header">
        <div>
          <h2>{detail.fileName}</h2>
          <p className="break-all">
            checksum {detail.checksum} · oleh {detail.createdBy} · {new Date(detail.createdAt).toLocaleString('id-ID')}
            {!detail.isLatest && <span className="text-warning"> · upload lama (sudah digantikan upload terbaru)</span>}
          </p>
        </div>
      </div>

      <div className="metric-grid">
        {[['Node', detail.totalNodes], ['Baris dibaca', r.totalRows], ['Dilewati', r.skippedRows],
          ['Error', detail.errorCount], ['Warning', detail.warningCount]].map(([k, v]) => (
          <div key={k} className={cx('metric', k === 'Error' && v && 'danger')}>
            <span>{k}</span><strong>{v ?? '—'}</strong>
          </div>
        ))}
      </div>

      <div className="result-section">
        <div className="subheading">
          <div>Antrian ambiguitas <span className="hint">{pending} pending dari {detail.items.length}</span></div>
        </div>
        {items.length === 0 ? (
          <p className="hint">Tidak ada item.</p>
        ) : (
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Kode</th><th scope="col">Baris</th><th scope="col">Parent usulan</th>
                  <th scope="col">Alasan</th><th scope="col">Keputusan</th>
                </tr>
              </thead>
              <tbody>
                {items.map(i => (
                  <tr key={i.id}>
                    <td className="mono">{i.code}</td>
                    <td>{i.rowNumber}</td>
                    <td className="mono">{i.proposedParent || '—'}</td>
                    <td>{i.reason}</td>
                    <td>
                      {i.decision === 'Pending' && detail.isLatest
                        ? <DecideItem item={i} onDecided={onDecided} />
                        : (
                          <span>
                            <span className={`badge ${DECISION_TONE[i.decision] || 'neutral'}`}>
                              <span className="dot" aria-hidden="true" />{i.decision}
                            </span>
                            {i.correctedParent && <span className="mono"> → {i.correctedParent}</span>}
                            {i.note && <span className="hint">{i.note} · {i.decidedBy}</span>}
                          </span>
                        )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {!pendingOnly && <IssuesTable issues={(r.issues || []).slice(0, 50)} />}
    </div>
  )
}

function DecideItem({ item, onDecided }) {
  const [parent, setParent] = useState('')
  const [note, setNote] = useState('')
  const { busy, error, run } = useAction()
  const decide = (decision) => run(async () => {
    await apiFetch(`/api/admin/ambiguity/${item.id}/decide`, { method: 'POST',
      body: JSON.stringify({ decision, correctedParent: decision === 'Approved' ? parent || null : null, note }) })
    await onDecided()
  })
  const ok = note.trim().length >= 5

  return (
    <div className="decision-cell">
      <label htmlFor={`amb-p-${item.id}`} className="sr-only">Parent koreksi untuk {item.code}</label>
      <input id={`amb-p-${item.id}`} value={parent} onChange={e => setParent(e.target.value)}
        placeholder="Parent koreksi (opsional)" className="cell-input mono" />
      <label htmlFor={`amb-n-${item.id}`} className="sr-only">Catatan keputusan untuk {item.code}</label>
      <input id={`amb-n-${item.id}`} value={note} onChange={e => setNote(e.target.value)}
        placeholder="Catatan (wajib)" className="cell-input" />
      <div className="row-actions">
        <button disabled={busy || !ok} onClick={() => decide('Approved')}
          className="primary-button">Approve</button>
        <button disabled={busy || !ok || parent.trim() !== ''} onClick={() => decide('Rejected')}
          title="Node yang di-reject tidak ikut di-commit"
          className="danger-button">Reject</button>
      </div>
      {error && <p role="alert" className="notice danger"><span>{error}</span></p>}
    </div>
  )
}

function VersionSelect({ versions, value, onChange, id }) {
  return (
    <div className="select-wrap">
      <select id={id} value={value} onChange={e => onChange(e.target.value)}>
        <option value="">Pilih versi target...</option>
        {versions.map(v => <option key={v.id} value={v.id}>{v.name} ({v.state})</option>)}
      </select>
      <ChevronDown size={15} aria-hidden="true" />
    </div>
  )
}

function IssuesTable({ issues }) {
  if (!issues?.length) return null
  return (
    <div className="result-section">
      <div className="subheading"><div>Isu validasi (maks. 50)</div></div>
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              <th scope="col">Severity</th><th scope="col">Tipe</th><th scope="col">Kode</th>
              <th scope="col">Baris</th><th scope="col">Pesan</th>
            </tr>
          </thead>
          <tbody>
            {issues.map((i, n) => (
              <tr key={n}>
                <td className={i.severity === 'ERROR' ? 'text-danger' : 'text-warning'}>{i.severity}</td>
                <td className="mono">{i.type}</td><td className="mono">{i.code}</td>
                <td>{i.rowNumber}</td><td>{i.message}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  )
}

// ─── DOCX upload: BTKI / EN / Legal Notes (Req 1.9, 1.11, 2.10, 3.7) ──────────

const DOCX_KINDS = { btki: 'BTKI (Tarif)', en: 'Explanatory Notes', notes: 'Catatan Bagian/Bab' }

function DocxPanel({ versions, reload, openBatch }) {
  const [kind, setKind] = useState('btki')
  const [files, setFiles] = useState([])
  const [report, setReport] = useState(null)
  const [versionId, setVersionId] = useState('')
  const { busy, error, notice, setNotice, run } = useAction()

  const [replace, setReplace] = useState(false)

  const reset = () => setReport(null)
  const isBtki = kind === 'btki'
  // EN and Catatan are only loaded into a draft that is not active (the active version is never changed)
  const isDraftKind = !isBtki
  // BTKI uploads go against a version still being prepared (re-upload allowed after a failed dry run)
  const uploadable = isBtki ? versions.filter(v => ['Uploaded', 'DryRunFailed'].includes(v.state))
    : versions.filter(v => !v.isActive && ['Committed', 'Indexed'].includes(v.state))
  const version = versions.find(v => v.id === versionId)

  const analyse = () => run(async () => {
    reset()
    const path = isBtki ? `/api/admin/btki/upload?versionId=${versionId}`
      : `/api/admin/${kind}/upload?versionId=${versionId}`
    setReport(await upload(path, isBtki ? files : files[0], isBtki ? 'files' : 'file'))
    if (isBtki) await reload()
  })
  const commit  = () => run(async () => {
    const res = await upload(`/api/admin/btki/commit/${versionId}`, files, 'files')
    setNotice(`${res.message} (${res.inserted} baris). Aktifkan versi di tab Versi.`); await reload()
  })
  const commitDraft = () => run(async () => {
    const res = await upload(`/api/admin/${kind}/commit/${versionId}${replace ? '?replace=true' : ''}`, files[0])
    setNotice(`${res.message} Bandingkan dengan shadow run di tab Versi sebelum mengaktifkan.`)
  })

  const dry = report?.dryRun
  const canCommit = isBtki && version?.state === 'ReadyToCommit'
  const canCommitDraft = isDraftKind && !!report?.batchId && report.errorCount === 0

  return (
    <>
      <div className="content-heading">
        <div>
          <h2>Upload Dokumen</h2>
          <p>Parse &amp; dry run dokumen BTKI, Explanatory Notes, atau Catatan Bagian/Bab.</p>
        </div>
      </div>

      <div className="form-grid">
        <label className="field">
          <span>Jenis dokumen</span>
          <div className="select-wrap">
            <select id="docx-kind" value={kind} onChange={e => { setKind(e.target.value); setFiles([]); setVersionId(''); setReplace(false); reset() }}>
              {Object.entries(DOCX_KINDS).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
            </select>
            <ChevronDown size={15} aria-hidden="true" />
          </div>
        </label>
        <label className="field">
          <span>{isBtki ? 'Versi target' : 'Draft target (bukan versi aktif)'}</span>
          <VersionSelect id="docx-version" versions={report ? versions : uploadable} value={versionId}
            onChange={v => { setVersionId(v); reset() }} />
        </label>
        <label className="field">
          <span>{isBtki ? 'File .docx, atau satu/banyak .pdf resmi' : 'File .docx'}</span>
          <input id="docx-file" key={kind} type="file" accept={isBtki ? '.docx,.pdf' : '.docx'} multiple={isBtki}
            onChange={e => { setFiles([...e.target.files]); reset() }} className="file-input" />
          {files.length > 1 && <p className="hint">{files.length} file dipilih</p>}
        </label>
      </div>

      <button className="primary-button" disabled={files.length === 0 || busy || !versionId} onClick={analyse}>
        {busy && !report ? 'Mem-parsing...' : 'Parse & Dry Run'}
      </button>

      {isBtki && uploadable.length === 0 && !report && (
        <p className="notice"><AlertTriangle size={16} aria-hidden="true" /><span>Belum ada versi yang siap di-upload. Buat dulu lewat "Tambah Versi" di tab Versi.</span></p>
      )}
      {isDraftKind && uploadable.length === 0 && !report && (
        <p className="notice"><AlertTriangle size={16} aria-hidden="true" /><span>Belum ada draft. Klon versi aktif lewat tombol "Klon jadi draft" di tab Versi, lalu muat dokumen ke draft itu.</span></p>
      )}

      {report && (
        <div className="result-section">
          <div className="subheading"><div>Laporan validasi</div></div>
          <div className="metric-grid">
            {Object.entries(report)
              .filter(([, v]) => typeof v === 'number' || typeof v === 'string')
              .map(([k, v]) => (
                <div className="metric" key={k}>
                  <span>{k}</span><strong className="mono">{String(v)}</strong>
                </div>
              ))}
            {dry && Object.entries(dry).map(([k, v]) => (
              <div key={k} className={cx('metric', k === 'canCommit' && (v ? 'success' : 'danger'))}>
                <span>dryRun.{k}</span><strong className="mono">{String(v)}</strong>
              </div>
            ))}
          </div>

          {report.nodesByLevel && (
            <p className="hint">
              Isi: {Object.entries(report.nodesByLevel).map(([k, v]) => `${k} ${v}`).join(' · ')} · {report.chapters} Bab
            </p>
          )}
          <IssuesTable issues={report.issues} />

          {report.sample?.length > 0 && (
            <details className="diff-block">
              <summary>Contoh hasil parsing vs sumber (10 baris pertama)</summary>
              <pre className="code-block">{JSON.stringify(report.sample, null, 2)}</pre>
            </details>
          )}

          {isBtki ? (
            <div className="result-section">
              <p className="hint">
                Status versi: <span className="mono">{version?.state}</span>
                {dry?.ambiguousNodes > 0 && <> · {dry.ambiguousNodes} node STRUCTURE_AMBIGUITY menunggu keputusan</>}
              </p>
              {report.batchId && (
                <button className="outline-button self-start" onClick={() => openBatch(report.batchId)}>
                  Buka laporan validasi &amp; antrian ambiguitas →
                </button>
              )}
              <div className="row-actions">
                <button className="primary-button" disabled={!canCommit || busy} onClick={commit}>Commit ke Database</button>
                <span className="hint">Gunakan file yang sama dengan yang divalidasi.</span>
              </div>
              {!canCommit && (
                <p className="notice danger">
                  <AlertTriangle size={16} aria-hidden="true" />
                  <span>
                    {dry?.errorCount > 0 ? 'Commit diblokir: masih ada error struktur. Perbaiki file lalu upload ulang.'
                      : 'Commit diblokir sampai semua ambiguitas diputuskan di laporan validasi.'}
                  </span>
                </p>
              )}
            </div>
          ) : (
            <div className="result-section">
              <label className="field">
                <span><input id="docx-replace" type="checkbox" checked={replace} onChange={e => setReplace(e.target.checked)} /> Ganti data {DOCX_KINDS[kind]} yang sudah ada di draft ini</span>
              </label>
              <div className="row-actions">
                <button className="primary-button" disabled={!canCommitDraft || busy} onClick={commitDraft}>Muat ke Draft</button>
                <span className="hint">Gunakan file yang sama dengan yang divalidasi. Versi aktif tidak berubah.</span>
              </div>
              {!canCommitDraft && (
                <p className="notice danger">
                  <AlertTriangle size={16} aria-hidden="true" />
                  <span>{report.errorCount > 0 ? 'Muat diblokir: dry run masih berisi error. Perbaiki file lalu upload ulang.' : 'Muat diblokir: dry run belum tercatat untuk draft ini.'}</span>
                </p>
              )}
            </div>
          )}
        </div>
      )}
      <Feedback error={error} notice={notice} />
    </>
  )
}

// ─── CSV/Excel template (Req 21.1, 21.2, 21.4, 21.8, 21.9) ───────────────────

function TemplatePanel({ versions, reload }) {
  const [file, setFile] = useState(null)
  const [versionId, setVersionId] = useState('')
  const [validation, setValidation] = useState(null)
  const [preview, setPreview] = useState(null)
  const [confirmed, setConfirmed] = useState(false)
  const { busy, error, notice, setNotice, run } = useAction()

  const reset = () => { setValidation(null); setPreview(null); setConfirmed(false) }

  const download = (fmt) => run(async () => {
    const blob = await apiFetch(`/api/admin/template/download/${fmt}`)
    const a = Object.assign(document.createElement('a'), { href: URL.createObjectURL(blob), download: `btki_template.${fmt}` })
    a.click(); URL.revokeObjectURL(a.href)
  })
  const validate = () => run(async () => { reset(); setValidation(await upload('/api/admin/template/validate', file)) })
  const doPreview = () => run(async () => setPreview(await upload(`/api/admin/template/preview/${versionId}`, file)))
  const commit = () => run(async () => {
    const r = await upload(`/api/admin/template/commit/${versionId}`, file)
    setNotice(`${r.message} Insert: ${r.inserted}, Update: ${r.updated}, Unchanged: ${r.unchanged}`)
    reset(); await reload()
  })

  const problems = validation
    ? [...validation.errors.map(e => ({ ...e, sev: 'ERROR' })), ...validation.warnings.map(w => ({ ...w, sev: 'WARNING' }))]
    : []

  return (
    <>
      <div className="content-heading">
        <div>
          <h2>Template CSV/Excel</h2>
          <p>Unduh template, validasi, preview diff, lalu commit ke versi target.</p>
        </div>
      </div>

      <div className="row-actions">
        <button className="outline-button" disabled={busy} onClick={() => download('csv')}><Download size={15} aria-hidden="true" /> Template CSV</button>
        <button className="outline-button" disabled={busy} onClick={() => download('xlsx')}><Download size={15} aria-hidden="true" /> Template Excel</button>
      </div>

      <div className="form-grid">
        <label className="field">
          <span>File .csv / .xlsx</span>
          <input id="tpl-file" type="file" accept=".csv,.xlsx" className="file-input"
            onChange={e => { setFile(e.target.files[0] || null); reset() }} />
        </label>
      </div>
      <button className="primary-button" disabled={!file || busy} onClick={validate}>1. Validasi</button>

      {validation && (
        <div className="result-section">
          <p className={validation.isValid ? 'hint text-success' : 'hint text-danger'}>
            {validation.validRows}/{validation.totalRows} baris valid · {validation.errorCount} error · {validation.warningCount} warning
          </p>
          {problems.length > 0 && (
            <div className="table-scroll">
              <table className="data-table">
                <caption>Kesalahan per baris</caption>
                <thead>
                  <tr>
                    <th scope="col">Baris</th><th scope="col">Kolom</th><th scope="col">Jenis</th>
                    <th scope="col">Nilai</th><th scope="col">Saran perbaikan</th>
                  </tr>
                </thead>
                <tbody>
                  {problems.map((e, i) => (
                    <tr key={i}>
                      <td>{e.rowNumber}</td><td className="mono">{e.columnName}</td>
                      <td className={e.sev === 'ERROR' ? 'text-danger' : 'text-warning'}>{e.errorType}</td>
                      <td className="mono">{e.actualValue}</td><td>{e.suggestedFix}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {validation.isValid && (
            <div className="row-actions">
              <VersionSelect id="tpl-version" versions={versions} value={versionId}
                onChange={v => { setVersionId(v); setPreview(null); setConfirmed(false) }} />
              <button className="primary-button" disabled={!versionId || busy} onClick={doPreview}>2. Preview Diff</button>
            </div>
          )}
        </div>
      )}

      {preview && (
        <div className="result-section">
          <div className="row-actions">
            <span className="badge success"><span className="dot" aria-hidden="true" />INSERT {preview.insertCount}</span>
            <span className="badge info"><span className="dot" aria-hidden="true" />UPDATE {preview.updateCount}</span>
            <span className="badge neutral"><span className="dot" aria-hidden="true" />SKIP (unchanged) {preview.unchangedCount}</span>
          </div>
          {preview.toUpdate?.length > 0 && (
            <div className="table-scroll">
              <table className="data-table">
                <caption>Perubahan (maks. 20)</caption>
                <thead><tr><th scope="col">Kode</th><th scope="col">Lama</th><th scope="col">Baru</th></tr></thead>
                <tbody>
                  {preview.toUpdate.map(u => (
                    <tr key={u.code}>
                      <td className="mono">{u.code}</td>
                      <td className="val-old">{JSON.stringify(u.oldValue)}</td>
                      <td className="val-new">{JSON.stringify(u.newValue)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
          {preview.toInsert?.length > 0 && (
            <p className="hint">Kode baru: <span className="mono">{preview.toInsert.map(r => r.code).join(', ')}</span></p>
          )}
          <label className="check-row">
            <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} />
            Saya sudah meninjau preview dan menyetujui perubahan ini.
          </label>
          <button className="primary-button" disabled={!confirmed || busy} onClick={commit}>3. Commit</button>
        </div>
      )}
      <Feedback error={error} notice={notice} />
    </>
  )
}
