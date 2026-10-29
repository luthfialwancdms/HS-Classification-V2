import { useEffect, useState } from 'react'
import { apiFetch } from '../services/apiClient'

// Admin Data Center (Task 18.2) — thin UI over the existing /api/admin endpoints.

const TABS = [
  ['versions', 'Versi Nomenklatur'],
  ['docx',     'Upload Dokumen'],
  ['reports',  'Laporan Validasi'],
  ['ambiguity','Antrian Ambiguitas'],
  ['template', 'Template CSV/Excel'],
]

const card = 'bg-white rounded-2xl border border-gray-200 shadow-sm p-6'
const btn  = 'px-3 py-1.5 text-sm rounded-lg font-medium disabled:opacity-50'
const primary = `${btn} bg-blue-600 text-white hover:bg-blue-700`
const secondary = `${btn} border border-gray-300 text-gray-700 hover:bg-gray-50`

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
  if (error)  return <p role="alert" className="text-sm text-red-600 bg-red-50 rounded p-2 mt-3">{error}</p>
  if (notice) return <p role="status" className="text-sm text-green-700 bg-green-50 rounded p-2 mt-3">{notice}</p>
  return null
}

export default function AdminDashboard() {
  const [tab, setTab] = useState('versions')
  const [versions, setVersions] = useState([])
  const [loadError, setLoadError] = useState(null)
  const [batchId, setBatchId] = useState(null)

  const reload = () => apiFetch('/api/admin/versions').then(setVersions, e => setLoadError(e.message))
  useEffect(() => { reload() }, [])
  const openBatch = (id) => { setBatchId(id); setTab('reports') }

  return (
    <main className="max-w-7xl mx-auto px-6 py-8">
      <h2 className="text-lg font-semibold text-gray-900 mb-4">Admin Data Center</h2>
      <div role="tablist" className="flex gap-2 mb-6">
        {TABS.map(([id, label]) => (
          <button key={id} role="tab" aria-selected={tab === id} onClick={() => setTab(id)}
            className={tab === id ? primary : secondary}>{label}</button>
        ))}
      </div>
      {loadError && <p role="alert" className="text-sm text-red-600 mb-4">{loadError}</p>}
      {tab === 'versions' && <VersionsPanel versions={versions} reload={reload} />}
      {tab === 'docx'     && <DocxPanel versions={versions} reload={reload} openBatch={openBatch} />}
      {tab === 'reports'  && <BatchesPanel versions={versions} reload={reload} batchId={batchId} setBatchId={setBatchId} />}
      {tab === 'ambiguity'&& <BatchesPanel versions={versions} reload={reload} batchId={batchId} setBatchId={setBatchId} pendingOnly />}
      {tab === 'template' && <TemplatePanel versions={versions} reload={reload} />}
    </main>
  )
}

// ─── Versions (Req 4.4, 4.6, 4.7) ────────────────────────────────────────────

const STATE_COLOR = {
  ACTIVE: 'bg-green-100 text-green-800', INDEXED: 'bg-blue-100 text-blue-800',
  COMMITTED: 'bg-blue-50 text-blue-700', DRYRUNFAILED: 'bg-red-100 text-red-800',
}

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
    <div className="mt-5 border-t border-gray-100 pt-4">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold text-gray-700">
          Beda "{version.name}" vs versi aktif{data ? ` "${data.baseline.name}"` : ''}
        </h3>
        <button className={secondary} onClick={onClose}>Tutup</button>
      </div>
      {error && <p role="alert" className="text-sm text-red-600 bg-red-50 rounded p-2 mt-2">{error}</p>}
      {!data && !error && <p className="text-xs text-gray-500 mt-2">Menghitung...</p>}
      {data && (
        <>
          <dl className="grid grid-cols-2 md:grid-cols-6 gap-3 text-sm mt-3">
            {[['Versi aktif', c.baseline], ['Versi ini', c.candidate], ['Sama', c.unchanged],
              ['Ditambah', c.added], ['Dihapus', c.removed], ['Uraian berubah', c.changed]].map(([k, v]) => (
              <div key={k} className="bg-gray-50 rounded p-2">
                <dt className="text-xs text-gray-500">{k}</dt><dd className="font-mono">{v}</dd>
              </div>
            ))}
          </dl>
          <p className="text-xs text-gray-500 mt-1">Beda OCR saja: {c.ocrOnly}. Spasi, tanda baca, dan huruf besar/kecil tidak dihitung sebagai beda.</p>

          <div className="mt-3">
            <label htmlFor="diff-chapter" className="text-xs text-gray-600 mr-2">Tampilkan Bab</label>
            <select id="diff-chapter" value={chapter} onChange={e => setChapter(e.target.value)}
              className="text-sm border border-gray-300 rounded p-1.5 bg-white">
              <option value="">Semua Bab</option>
              {data.perChapter.map(p => (
                <option key={p.chapter} value={p.chapter}>
                  {p.chapter} (+{p.added} −{p.removed} ~{p.changed + p.ocrOnly})
                </option>
              ))}
            </select>
          </div>

          {DIFF_KINDS.map(([key, label]) => (
            <details key={key} className="mt-3">
              <summary className="text-sm font-semibold text-gray-700 cursor-pointer">
                {label}: {data[key].total}{data[key].truncated && ` (menampilkan ${data[key].items.length})`}
              </summary>
              <table className="w-full text-xs mt-2">
                <thead><tr className="text-left text-gray-500">
                  <th scope="col">Kode</th><th scope="col">Tingkat</th><th scope="col">Versi aktif</th><th scope="col">Versi ini</th>
                </tr></thead>
                <tbody>
                  {data[key].items.map(e => (
                    <tr key={e.code} className="border-t border-gray-50 align-top">
                      <td className="font-mono py-1 pr-2">{e.code}</td><td className="pr-2">{e.level}</td>
                      <td className="pr-2">{e.before}</td><td>{e.after}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </details>
          ))}
        </>
      )}
    </div>
  )
}

function VersionsPanel({ versions, reload }) {
  const { busy, error, notice, setNotice, run } = useAction()
  const [diffId, setDiffId] = useState(null)
  const diffVersion = versions.find(v => v.id === diffId)
  const act = (id, action) => run(async () => {
    const res = await apiFetch(`/api/admin/versions/${id}/${action}`, { method: 'POST' })
    setNotice(res.message); await reload()
  })

  return (
    <section className={card}>
      <table className="w-full text-sm">
        <caption className="text-left text-sm font-semibold text-gray-700 mb-3">Daftar versi BTKI</caption>
        <thead>
          <tr className="text-left text-xs text-gray-500">
            <th scope="col" className="py-2">Nama</th><th scope="col">Edisi</th>
            <th scope="col">Berlaku</th><th scope="col">Status Import</th><th scope="col">Aksi</th>
          </tr>
        </thead>
        <tbody>
          {versions.length === 0 && <tr><td colSpan={5} className="py-4 text-gray-400 text-center">Belum ada versi</td></tr>}
          {versions.map(v => (
            <tr key={v.id} className="border-t border-gray-100">
              <td className="py-2 font-medium">{v.name}</td>
              <td>{v.btkiEdition}</td>
              <td className="text-xs">{v.effectiveFrom?.slice(0, 10)} – {v.effectiveTo?.slice(0, 10) || '∞'}</td>
              <td>
                <span className={`text-xs px-2 py-0.5 rounded-full ${STATE_COLOR[(v.isActive ? 'ACTIVE' : v.state).toUpperCase()] || 'bg-gray-100 text-gray-700'}`}>
                  {v.isActive ? 'Active' : v.state}
                </span>
              </td>
              <td className="py-2 flex gap-2">
                {v.isActive
                  ? <button className={secondary} disabled={busy} onClick={() => act(v.id, 'deactivate')}>Nonaktifkan / Rollback</button>
                  : <>
                      <button className={primary} disabled={busy || v.state !== 'Indexed'} onClick={() => act(v.id, 'activate')}
                        title={v.state !== 'Indexed' ? 'Hanya versi berstatus Indexed yang dapat diaktifkan' : undefined}>Aktifkan</button>
                      {['Indexed', 'Committed'].includes(v.state) &&
                        <button className={secondary} onClick={() => setDiffId(v.id)}>Bandingkan</button>}
                    </>}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <p className="text-xs text-gray-500 mt-3">
        Mengaktifkan versi menonaktifkan versi lain di rentang tanggal yang sama. Riwayat klasifikasi tetap tersimpan.
      </p>
      <Feedback error={error} notice={notice} />
      {diffVersion && <DiffView key={diffVersion.id} version={diffVersion} onClose={() => setDiffId(null)} />}
      <NewVersionForm reload={reload} />
    </section>
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
    <div>
      <label htmlFor={`nv-${k}`} className="block text-xs text-gray-600 mb-1">{label}{required && ' *'}</label>
      <input id={`nv-${k}`} type={type} required={required} value={f[k]} onChange={set(k)}
        className="w-full text-sm border border-gray-300 rounded p-2" />
    </div>
  )

  return (
    <form onSubmit={submit} className="mt-6 border-t border-gray-100 pt-4">
      <h3 className="text-sm font-semibold text-gray-700 mb-2">Tambah Versi</h3>
      <div className="grid grid-cols-2 md:grid-cols-5 gap-3 items-end">
        {field('name', 'Nama', 'text', true)}
        {field('btkiEdition', 'Edisi')}
        {field('regulationNumber', 'No. regulasi')}
        {field('effectiveFrom', 'Berlaku mulai', 'date', true)}
        {field('effectiveTo', 'Berlaku sampai', 'date')}
      </div>
      <button type="submit" className={`${primary} mt-3`} disabled={busy}>Tambah Versi</button>
      <Feedback error={error} notice={notice} />
    </form>
  )
}

// ─── Laporan Validasi & Antrian Ambiguitas (Req 1.4, 1.9, 1.11) ───────────────

const DECISION_STYLE = {
  Pending: 'bg-amber-100 text-amber-800', Approved: 'bg-green-100 text-green-800', Rejected: 'bg-red-100 text-red-800',
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
    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
      <section className={card}>
        <label htmlFor="batch-version" className="block text-xs text-gray-600 mb-1">Filter versi</label>
        <select id="batch-version" value={versionId} onChange={e => setVersionId(e.target.value)}
          className="w-full text-sm border border-gray-300 rounded p-2 bg-white mb-3">
          <option value="">Semua versi</option>
          {versions.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}
        </select>
        {listError && <p role="alert" className="text-sm text-red-600">{listError}</p>}
        <ul className="flex flex-col divide-y divide-gray-100">
          {shown.length === 0 && (
            <li className="py-4 text-sm text-gray-400 text-center">
              {pendingOnly ? 'Tidak ada ambiguitas yang menunggu keputusan' : 'Belum ada laporan validasi'}
            </li>
          )}
          {shown.map(b => (
            <li key={b.id}>
              <button onClick={() => setBatchId(b.id)} aria-current={batchId === b.id ? 'true' : undefined}
                className={`w-full text-left py-2 px-2 rounded-lg hover:bg-gray-50 ${batchId === b.id ? 'bg-blue-50' : ''}`}>
                <span className="block text-sm font-medium text-gray-900 truncate">{b.fileName}</span>
                <span className="block text-xs text-gray-500">
                  {versionName(b.versionId)} · {new Date(b.createdAt).toLocaleString('id-ID')}
                </span>
                <span className="block text-xs mt-0.5">
                  <span className={b.errorCount ? 'text-red-600' : 'text-gray-500'}>{b.errorCount} error</span> ·{' '}
                  <span className={b.pending ? 'text-amber-700 font-medium' : 'text-gray-500'}>{b.pending}/{b.ambiguous} ambiguitas pending</span>
                </span>
              </button>
            </li>
          ))}
        </ul>
      </section>

      <section className={`${card} lg:col-span-2`}>
        {!batchId && <p className="text-sm text-gray-500">Pilih laporan di sebelah kiri.</p>}
        {detail?.error && <p role="alert" className="text-sm text-red-600">{detail.error}</p>}
        {detail && !detail.error && <BatchDetail detail={detail} pendingOnly={pendingOnly} onDecided={refresh} />}
      </section>
    </div>
  )
}

function BatchDetail({ detail, pendingOnly, onDecided }) {
  const r = detail.report || {}
  const items = pendingOnly ? detail.items.filter(i => i.decision === 'Pending') : detail.items
  const pending = detail.items.filter(i => i.decision === 'Pending').length

  return (
    <div>
      <h3 className="text-sm font-semibold text-gray-700">{detail.fileName}</h3>
      <p className="text-xs text-gray-500 mb-3 break-all">
        checksum {detail.checksum} · oleh {detail.createdBy} · {new Date(detail.createdAt).toLocaleString('id-ID')}
        {!detail.isLatest && <span className="text-amber-700"> · upload lama (sudah digantikan upload terbaru)</span>}
      </p>
      <dl className="grid grid-cols-2 md:grid-cols-5 gap-2 text-sm mb-4">
        {[['Node', detail.totalNodes], ['Baris dibaca', r.totalRows], ['Dilewati', r.skippedRows],
          ['Error', detail.errorCount], ['Warning', detail.warningCount]].map(([k, v]) => (
          <div key={k} className={`rounded p-2 ${k === 'Error' && v ? 'bg-red-50' : 'bg-gray-50'}`}>
            <dt className="text-xs text-gray-500">{k}</dt><dd className="font-mono">{v ?? '—'}</dd>
          </div>
        ))}
      </dl>

      <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mb-1">
        Antrian ambiguitas ({pending} pending dari {detail.items.length})
      </h4>
      {items.length === 0 ? (
        <p className="text-sm text-gray-500 mb-4">Tidak ada item.</p>
      ) : (
        <table className="w-full text-xs mb-4">
          <thead><tr className="text-left text-gray-500">
            <th scope="col" className="py-1">Kode</th><th scope="col">Baris</th><th scope="col">Parent usulan</th>
            <th scope="col">Alasan</th><th scope="col">Keputusan</th>
          </tr></thead>
          <tbody>
            {items.map(i => (
              <tr key={i.id} className="border-t border-gray-100 align-top">
                <td className="py-2 font-mono">{i.code}</td>
                <td>{i.rowNumber}</td>
                <td className="font-mono">{i.proposedParent || '—'}</td>
                <td className="pr-2">{i.reason}</td>
                <td className="min-w-64">
                  {i.decision === 'Pending' && detail.isLatest
                    ? <DecideItem item={i} onDecided={onDecided} />
                    : (
                      <span>
                        <span className={`px-2 py-0.5 rounded-full ${DECISION_STYLE[i.decision]}`}>{i.decision}</span>
                        {i.correctedParent && <span className="font-mono"> → {i.correctedParent}</span>}
                        {i.note && <span className="block text-gray-500 mt-1">{i.note} · {i.decidedBy}</span>}
                      </span>
                    )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}

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
    <div className="flex flex-col gap-1">
      <label htmlFor={`amb-p-${item.id}`} className="sr-only">Parent koreksi untuk {item.code}</label>
      <input id={`amb-p-${item.id}`} value={parent} onChange={e => setParent(e.target.value)}
        placeholder="Parent koreksi (opsional)" className="border border-gray-300 rounded p-1 font-mono" />
      <label htmlFor={`amb-n-${item.id}`} className="sr-only">Catatan keputusan untuk {item.code}</label>
      <input id={`amb-n-${item.id}`} value={note} onChange={e => setNote(e.target.value)}
        placeholder="Catatan (wajib)" className="border border-gray-300 rounded p-1" />
      <div className="flex gap-1">
        <button disabled={busy || !ok} onClick={() => decide('Approved')}
          className="px-2 py-1 rounded bg-green-600 text-white disabled:opacity-50">Approve</button>
        <button disabled={busy || !ok || parent.trim() !== ''} onClick={() => decide('Rejected')}
          title="Node yang di-reject tidak ikut di-commit"
          className="px-2 py-1 rounded bg-red-600 text-white disabled:opacity-50">Reject</button>
      </div>
      {error && <p role="alert" className="text-red-600">{error}</p>}
    </div>
  )
}

function VersionSelect({ versions, value, onChange, id }) {
  return (
    <select id={id} value={value} onChange={e => onChange(e.target.value)}
      className="text-sm border border-gray-300 rounded p-2 bg-white">
      <option value="">Pilih versi target...</option>
      {versions.map(v => <option key={v.id} value={v.id}>{v.name} ({v.state})</option>)}
    </select>
  )
}

function IssuesTable({ issues }) {
  if (!issues?.length) return null
  return (
    <table className="w-full text-xs mt-3">
      <caption className="text-left font-semibold text-gray-700 mb-1">Isu validasi (maks. 50)</caption>
      <thead><tr className="text-left text-gray-500">
        <th scope="col">Severity</th><th scope="col">Tipe</th><th scope="col">Kode</th><th scope="col">Baris</th><th scope="col">Pesan</th>
      </tr></thead>
      <tbody>
        {issues.map((i, n) => (
          <tr key={n} className="border-t border-gray-50">
            <td className={i.severity === 'ERROR' ? 'text-red-600 font-semibold' : 'text-amber-600'}>{i.severity}</td>
            <td className="font-mono">{i.type}</td><td className="font-mono">{i.code}</td>
            <td>{i.rowNumber}</td><td>{i.message}</td>
          </tr>
        ))}
      </tbody>
    </table>
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

  const reset = () => setReport(null)
  const isBtki = kind === 'btki'
  // BTKI uploads go against a version still being prepared (re-upload allowed after a failed dry run)
  const uploadable = versions.filter(v => ['Uploaded', 'DryRunFailed'].includes(v.state))
  const version = versions.find(v => v.id === versionId)

  const analyse = () => run(async () => {
    reset()
    const path = isBtki ? `/api/admin/btki/upload?versionId=${versionId}` : `/api/admin/${kind}/upload`
    setReport(await upload(path, isBtki ? files : files[0], isBtki ? 'files' : 'file'))
    if (isBtki) await reload()
  })
  const commit  = () => run(async () => {
    const res = await upload(`/api/admin/btki/commit/${versionId}`, files, 'files')
    setNotice(`${res.message} (${res.inserted} baris). Aktifkan versi di tab Versi.`); await reload()
  })

  const dry = report?.dryRun
  const canCommit = isBtki && version?.state === 'ReadyToCommit'

  return (
    <section className={card}>
      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="docx-kind" className="block text-xs text-gray-600 mb-1">Jenis dokumen</label>
          <select id="docx-kind" value={kind} onChange={e => { setKind(e.target.value); setFiles([]); reset() }}
            className="text-sm border border-gray-300 rounded p-2 bg-white">
            {Object.entries(DOCX_KINDS).map(([k, l]) => <option key={k} value={k}>{l}</option>)}
          </select>
        </div>
        {isBtki && (
          <div>
            <label htmlFor="docx-version" className="block text-xs text-gray-600 mb-1">Versi target</label>
            <VersionSelect id="docx-version" versions={report ? versions : uploadable} value={versionId}
              onChange={v => { setVersionId(v); reset() }} />
          </div>
        )}
        <div>
          <label htmlFor="docx-file" className="block text-xs text-gray-600 mb-1">
            {isBtki ? 'File .docx, atau satu/banyak .pdf resmi' : 'File .docx'}
          </label>
          <input id="docx-file" key={kind} type="file" accept={isBtki ? '.docx,.pdf' : '.docx'} multiple={isBtki}
            onChange={e => { setFiles([...e.target.files]); reset() }} className="text-sm" />
          {files.length > 1 && <p className="text-xs text-gray-500 mt-1">{files.length} file dipilih</p>}
        </div>
        <button className={primary} disabled={files.length === 0 || busy || (isBtki && !versionId)} onClick={analyse}>
          {busy && !report ? 'Mem-parsing...' : 'Parse & Dry Run'}
        </button>
      </div>
      {isBtki && uploadable.length === 0 && !report && (
        <p className="text-xs text-amber-700 mt-2">Belum ada versi yang siap di-upload. Buat dulu lewat "Tambah Versi" di tab Versi.</p>
      )}

      {report && (
        <div className="mt-5">
          <h3 className="text-sm font-semibold text-gray-700 mb-2">Laporan validasi</h3>
          <dl className="grid grid-cols-2 md:grid-cols-4 gap-3 text-sm">
            {Object.entries(report)
              .filter(([, v]) => typeof v === 'number' || typeof v === 'string')
              .map(([k, v]) => (
                <div key={k} className="bg-gray-50 rounded p-2">
                  <dt className="text-xs text-gray-500">{k}</dt>
                  <dd className="font-mono text-xs break-all">{String(v)}</dd>
                </div>
              ))}
            {dry && Object.entries(dry).map(([k, v]) => (
              <div key={k} className={`rounded p-2 ${k === 'canCommit' ? (v ? 'bg-green-50' : 'bg-red-50') : 'bg-gray-50'}`}>
                <dt className="text-xs text-gray-500">dryRun.{k}</dt>
                <dd className="font-mono text-xs">{String(v)}</dd>
              </div>
            ))}
          </dl>

          {report.nodesByLevel && (
            <p className="text-xs text-gray-600 mt-2">
              Isi: {Object.entries(report.nodesByLevel).map(([k, v]) => `${k} ${v}`).join(' · ')} · {report.chapters} Bab
            </p>
          )}
          <IssuesTable issues={report.issues} />

          {report.sample?.length > 0 && (
            <details className="mt-3">
              <summary className="text-xs font-semibold text-gray-700 cursor-pointer">Contoh hasil parsing vs sumber (10 baris pertama)</summary>
              <pre className="text-[11px] bg-gray-50 rounded p-2 mt-2 overflow-auto max-h-64">{JSON.stringify(report.sample, null, 2)}</pre>
            </details>
          )}

          {isBtki ? (
            <div className="mt-4 border-t border-gray-100 pt-4 flex flex-col gap-3">
              <p className="text-sm text-gray-700">
                Status versi: <span className="font-mono">{version?.state}</span>
                {dry?.ambiguousNodes > 0 && <> · {dry.ambiguousNodes} node STRUCTURE_AMBIGUITY menunggu keputusan</>}
              </p>
              {report.batchId && (
                <button className={secondary + ' self-start'} onClick={() => openBatch(report.batchId)}>
                  Buka laporan validasi &amp; antrian ambiguitas →
                </button>
              )}
              <div className="flex items-center gap-3">
                <button className={primary} disabled={!canCommit || busy} onClick={commit}>Commit ke Database</button>
                <span className="text-xs text-gray-500">Gunakan file yang sama dengan yang divalidasi.</span>
              </div>
              {!canCommit && (
                <p className="text-xs text-red-600">
                  {dry?.errorCount > 0 ? 'Commit diblokir: masih ada error struktur. Perbaiki file lalu upload ulang.'
                    : 'Commit diblokir sampai semua ambiguitas diputuskan di laporan validasi.'}
                </p>
              )}
            </div>
          ) : (
            <p className="text-xs text-gray-500 mt-4">Commit untuk {DOCX_KINDS[kind]} belum tersedia di API backend.</p>
          )}
        </div>
      )}
      <Feedback error={error} notice={notice} />
    </section>
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

  return (
    <section className={card}>
      <div className="flex gap-2 mb-4">
        <button className={secondary} disabled={busy} onClick={() => download('csv')}>⬇ Template CSV</button>
        <button className={secondary} disabled={busy} onClick={() => download('xlsx')}>⬇ Template Excel</button>
      </div>

      <div className="flex flex-wrap items-end gap-3">
        <div>
          <label htmlFor="tpl-file" className="block text-xs text-gray-600 mb-1">File .csv / .xlsx</label>
          <input id="tpl-file" type="file" accept=".csv,.xlsx"
            onChange={e => { setFile(e.target.files[0] || null); reset() }} className="text-sm" />
        </div>
        <button className={primary} disabled={!file || busy} onClick={validate}>1. Validasi</button>
      </div>

      {validation && (
        <div className="mt-4">
          <p className={`text-sm font-medium ${validation.isValid ? 'text-green-700' : 'text-red-600'}`}>
            {validation.validRows}/{validation.totalRows} baris valid · {validation.errorCount} error · {validation.warningCount} warning
          </p>
          {[...validation.errors.map(e => ({ ...e, sev: 'ERROR' })), ...validation.warnings.map(w => ({ ...w, sev: 'WARNING' }))].length > 0 && (
            <table className="w-full text-xs mt-2">
              <caption className="text-left font-semibold text-gray-700 mb-1">Kesalahan per baris</caption>
              <thead><tr className="text-left text-gray-500">
                <th scope="col">Baris</th><th scope="col">Kolom</th><th scope="col">Jenis</th><th scope="col">Nilai</th><th scope="col">Saran perbaikan</th>
              </tr></thead>
              <tbody>
                {[...validation.errors.map(e => ({ ...e, sev: 'ERROR' })), ...validation.warnings.map(w => ({ ...w, sev: 'WARNING' }))].map((e, i) => (
                  <tr key={i} className="border-t border-gray-50">
                    <td>{e.rowNumber}</td><td className="font-mono">{e.columnName}</td>
                    <td className={e.sev === 'ERROR' ? 'text-red-600' : 'text-amber-600'}>{e.errorType}</td>
                    <td className="font-mono">{e.actualValue}</td><td>{e.suggestedFix}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {validation.isValid && (
            <div className="flex items-center gap-3 mt-4">
              <VersionSelect id="tpl-version" versions={versions} value={versionId} onChange={v => { setVersionId(v); setPreview(null); setConfirmed(false) }} />
              <button className={primary} disabled={!versionId || busy} onClick={doPreview}>2. Preview Diff</button>
            </div>
          )}
        </div>
      )}

      {preview && (
        <div className="mt-4">
          <div className="flex gap-3 text-sm">
            <span className="px-2 py-1 rounded bg-green-50 text-green-700">INSERT {preview.insertCount}</span>
            <span className="px-2 py-1 rounded bg-blue-50 text-blue-700">UPDATE {preview.updateCount}</span>
            <span className="px-2 py-1 rounded bg-gray-100 text-gray-600">SKIP (unchanged) {preview.unchangedCount}</span>
          </div>
          {preview.toUpdate?.length > 0 && (
            <table className="w-full text-xs mt-3">
              <caption className="text-left font-semibold text-gray-700 mb-1">Perubahan (maks. 20)</caption>
              <thead><tr className="text-left text-gray-500"><th scope="col">Kode</th><th scope="col">Lama</th><th scope="col">Baru</th></tr></thead>
              <tbody>
                {preview.toUpdate.map(u => (
                  <tr key={u.code} className="border-t border-gray-50 align-top">
                    <td className="font-mono">{u.code}</td>
                    <td className="text-red-600 line-through">{JSON.stringify(u.oldValue)}</td>
                    <td className="text-green-700">{JSON.stringify(u.newValue)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}
          {preview.toInsert?.length > 0 && (
            <p className="text-xs text-gray-600 mt-2">Kode baru: <span className="font-mono">{preview.toInsert.map(r => r.code).join(', ')}</span></p>
          )}
          <label className="flex items-start gap-2 text-sm text-gray-700 mt-4">
            <input type="checkbox" checked={confirmed} onChange={e => setConfirmed(e.target.checked)} className="mt-1" />
            Saya sudah meninjau preview dan menyetujui perubahan ini.
          </label>
          <button className={`${primary} mt-3`} disabled={!confirmed || busy} onClick={commit}>3. Commit</button>
        </div>
      )}
      <Feedback error={error} notice={notice} />
    </section>
  )
}
