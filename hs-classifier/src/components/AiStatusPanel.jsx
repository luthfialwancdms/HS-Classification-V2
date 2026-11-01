import { useEffect, useState } from 'react'
import { Bot, ChevronDown, ChevronLeft, ChevronRight, Info, PlugZap } from 'lucide-react'
import { checkAiConnection, getAiStatus, getAiUsage, listAiRequests } from '../services/apiClient'

// Tab "Status AI" (Admin Data Center): koneksi ke AI, ringkasan penggunaan, dan riwayat request.
// Semua angka datang dari API; tidak ada nilai bawaan atau perkiraan di sisi klien.

const PAGE_SIZE = 20
const DAYS = 7
const nf = new Intl.NumberFormat('id-ID')
const num = (v) => (v == null ? '—' : nf.format(v))
const fmtTime = (iso) => (iso ? new Date(iso).toLocaleString('id-ID', { dateStyle: 'medium', timeStyle: 'medium' }) : '—')
const fmtDay = (ymd) => new Date(`${ymd}T00:00:00`).toLocaleDateString('id-ID', { day: '2-digit', month: 'short', year: 'numeric' })
const offsetLabel = (min) => {
  const a = Math.abs(min)
  return `UTC${min < 0 ? '−' : '+'}${Math.floor(a / 60)}${a % 60 ? `:${String(a % 60).padStart(2, '0')}` : ''}`
}

function Metric({ label, value, tone }) {
  return (
    <div className={tone ? `metric ${tone}` : 'metric'}>
      <span>{label}</span>
      <strong>{value}</strong>
    </div>
  )
}

export default function AiStatusPanel() {
  // Setelah cek koneksi sungguhan, ringkasan dan riwayat dimuat ulang (cek tercatat sebagai satu request).
  const [reloadKey, setReloadKey] = useState(0)
  return (
    <>
      <div className="content-heading">
        <div>
          <h2>Status AI</h2>
          <p>Koneksi ke penyedia AI, jumlah request, dan token. AI hanya menerjemahkan istilah barang; kode HS tetap dari database BTKI.</p>
        </div>
      </div>
      <ConnectionCard onChecked={() => setReloadKey(k => k + 1)} />
      <UsageSummary reloadKey={reloadKey} />
      <RequestHistory reloadKey={reloadKey} />
    </>
  )
}

// ─── Koneksi ──────────────────────────────────────────────────────────────────

function ConnectionCard({ onChecked }) {
  const [state, setState] = useState({ loading: true, status: null, error: null })
  const [checking, setChecking] = useState(false)
  const [checkError, setCheckError] = useState(null)
  const [cacheNote, setCacheNote] = useState(null)

  // Hanya membaca hasil cek terakhir dari memori server; tidak memanggil AI.
  useEffect(() => {
    let alive = true
    getAiStatus().then(
      s => alive && setState({ loading: false, status: s, error: null }),
      e => alive && setState({ loading: false, status: null, error: e.message }))
    return () => { alive = false }
  }, [])

  const check = async () => {
    setChecking(true); setCheckError(null); setCacheNote(null)
    try {
      const s = await checkAiConnection()
      setState({ loading: false, status: s, error: null })
      if (s.dariCache) {
        setCacheNote(`Cek koneksi dibatasi (maksimal satu cek sungguhan per beberapa detik). Menampilkan hasil terakhir. Coba lagi dalam ${s.bisaCekLagiDalamDetik} detik.`)
      } else {
        onChecked()
      }
    } catch (e) {
      setCheckError(e.message)
    } finally {
      setChecking(false)
    }
  }

  const s = state.status
  const checked = !!s?.pernahDicek
  const tone = !checked ? 'neutral' : s.terhubung ? 'success' : 'danger'
  const label = !checked ? 'Belum dicek' : s.terhubung ? 'Terhubung' : 'Tidak terhubung'

  return (
    <section aria-labelledby="ai-conn-title" className="result-section">
      <div className="ai-conn-head">
        <div className="subheading">
          <div><Bot size={17} aria-hidden="true" /><span id="ai-conn-title">Koneksi AI</span></div>
        </div>
        <div className="row-actions">
          <span className={`badge ${tone}`}><span className="dot" aria-hidden="true" />{label}</span>
          <button type="button" className="primary-button" onClick={check} disabled={checking || state.loading}>
            <PlugZap size={16} aria-hidden="true" /> {checking ? 'Memeriksa...' : 'Cek koneksi'}
          </button>
        </div>
      </div>

      {state.loading && <p className="hint" role="status">Memuat status…</p>}
      {state.error && <p role="alert" className="notice danger"><span>{state.error}</span></p>}
      {checkError && <p role="alert" className="notice danger"><span>{checkError}</span></p>}

      {s && (
        <div className="metric-grid wide">
          <Metric label="Penyedia" value={s.provider} />
          <Metric label="Model" value={s.model} />
          <Metric label="Latensi" value={s.latensiMs == null ? '—' : `${num(s.latensiMs)} ms`} />
          <Metric label="Cek terakhir" value={fmtTime(s.waktuCek)} />
          <Metric label="API key" value={s.keyTerkonfigurasi ? 'Terkonfigurasi' : 'Belum dikonfigurasi'}
            tone={s.keyTerkonfigurasi ? undefined : 'danger'} />
        </div>
      )}

      {checked && !s.terhubung && s.pesanError && (
        <p className="notice danger" role="status"><Info size={16} aria-hidden="true" /><span>{s.pesanError}</span></p>
      )}
      {cacheNote && <p className="notice" role="status"><Info size={16} aria-hidden="true" /><span>{cacheNote}</span></p>}
      <p className="hint">Cek koneksi mengirim satu permintaan kecil ke AI (beberapa token) dan tercatat di riwayat sebagai "Cek koneksi".</p>
    </section>
  )
}

// ─── Ringkasan ────────────────────────────────────────────────────────────────

function MetricGroup({ title, b }) {
  return (
    <>
      <h3 className="metric-group-title">{title}</h3>
      <div className="metric-grid wide">
        <Metric label="Total request" value={num(b.request)} />
        <Metric label="Sukses" value={num(b.sukses)} tone={b.sukses > 0 ? 'success' : undefined} />
        <Metric label="Gagal" value={num(b.gagal)} tone={b.gagal > 0 ? 'danger' : undefined} />
        <Metric label="Token masuk" value={num(b.tokenMasuk)} />
        <Metric label="Token keluar" value={num(b.tokenKeluar)} />
        <Metric label="Token total" value={num(b.tokenTotal)} />
        <Metric label="Dipakai klasifikasi" value={`${num(b.klasifikasi.request)} request`} />
        <Metric label="Dari cek koneksi" value={`${num(b.cekKoneksi.request)} request · ${num(b.cekKoneksi.tokenTotal)} token`} />
      </div>
    </>
  )
}

function UsageSummary({ reloadKey }) {
  const [st, setSt] = useState({ key: null, data: null, error: null })
  useEffect(() => {
    let alive = true
    getAiUsage(DAYS).then(
      d => alive && setSt({ key: reloadKey, data: d, error: null }),
      e => alive && setSt(s => ({ ...s, key: reloadKey, error: e.message })))
    return () => { alive = false }
  }, [reloadKey])

  const d = st.data
  return (
    <section aria-labelledby="ai-usage-title" className="result-section">
      <div className="subheading"><div><span id="ai-usage-title">Ringkasan penggunaan</span></div></div>
      {st.key !== reloadKey && !st.error && !d && <p className="hint" role="status">Memuat ringkasan…</p>}
      {st.error && <p role="alert" className="notice danger"><span>{st.error}</span></p>}
      {d && (
        <>
          <p className="hint">
            Hari mengikuti zona waktu {offsetLabel(d.zonaWaktuOffsetMenit)}. Total sudah termasuk cek koneksi.
          </p>
          <MetricGroup title="Hari ini" b={d.hariIni} />
          <MetricGroup title="30 hari terakhir" b={d.tigaPuluhHari} />
          {d.requestSuksesTanpaDataToken > 0 && (
            <p className="hint">
              {num(d.requestSuksesTanpaDataToken)} request sukses tidak melaporkan jumlah token, jadi tidak ikut dalam total token.
            </p>
          )}

          <div className="table-scroll">
            <table className="data-table">
              <caption>Rincian {DAYS} hari terakhir</caption>
              <thead>
                <tr>
                  <th scope="col">Tanggal</th>
                  <th scope="col" className="num">Request</th>
                  <th scope="col" className="num">Sukses</th>
                  <th scope="col" className="num">Gagal</th>
                  <th scope="col" className="num">Token masuk</th>
                  <th scope="col" className="num">Token keluar</th>
                  <th scope="col" className="num">Token total</th>
                  <th scope="col" className="num">Cek koneksi</th>
                </tr>
              </thead>
              <tbody>
                {d.perHari.map(r => (
                  <tr key={r.tanggal}>
                    <td className="mono">{fmtDay(r.tanggal)}</td>
                    <td className="num">{num(r.ringkasan.request)}</td>
                    <td className="num">{num(r.ringkasan.sukses)}</td>
                    <td className="num">{num(r.ringkasan.gagal)}</td>
                    <td className="num">{num(r.ringkasan.tokenMasuk)}</td>
                    <td className="num">{num(r.ringkasan.tokenKeluar)}</td>
                    <td className="num">{num(r.ringkasan.tokenTotal)}</td>
                    <td className="num">{num(r.ringkasan.cekKoneksi.request)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </section>
  )
}

// ─── Riwayat request ──────────────────────────────────────────────────────────

function RequestHistory({ reloadKey }) {
  const [filters, setFilters] = useState({ status: '', kind: '', from: '', to: '' })
  const [page, setPage] = useState(1)
  // Respons ditandai dengan request yang dijawabnya, sehingga "memuat" diturunkan, bukan di-set di dalam effect.
  const [list, setList] = useState({ key: null, items: [], total: 0, error: null })

  const requestKey = JSON.stringify([filters, page, reloadKey])
  useEffect(() => {
    let alive = true
    listAiRequests({ ...filters, page, pageSize: PAGE_SIZE }).then(
      d => alive && setList({ key: requestKey, items: d.items, total: d.total, error: null }),
      e => alive && setList(l => ({ ...l, key: requestKey, error: e.message })))
    return () => { alive = false }
  }, [filters, page, requestKey])

  const loading = list.key !== requestKey
  const setFilter = (k, v) => { setPage(1); setFilters(f => ({ ...f, [k]: v })) }
  const pages = Math.max(1, Math.ceil(list.total / PAGE_SIZE))
  const filtered = Object.values(filters).some(Boolean)

  return (
    <section aria-labelledby="ai-req-title" className="result-section">
      <div className="subheading"><div><span id="ai-req-title">Riwayat request ke AI</span></div></div>

      <div className="toolbar toolbar-quad">
        <label className="field">
          <span>Status</span>
          <div className="select-wrap">
            <select id="ai-req-status" value={filters.status} onChange={e => setFilter('status', e.target.value)}>
              <option value="">Semua status</option>
              <option value="Success">Sukses</option>
              <option value="Failed">Gagal</option>
            </select>
            <ChevronDown size={15} aria-hidden="true" />
          </div>
        </label>
        <label className="field">
          <span>Jenis</span>
          <div className="select-wrap">
            <select id="ai-req-kind" value={filters.kind} onChange={e => setFilter('kind', e.target.value)}>
              <option value="">Semua jenis</option>
              <option value="TermSuggest">Klasifikasi</option>
              <option value="HealthCheck">Cek koneksi</option>
            </select>
            <ChevronDown size={15} aria-hidden="true" />
          </div>
        </label>
        <label className="field">
          <span>Dari tanggal</span>
          <input id="ai-req-from" type="date" value={filters.from} onChange={e => setFilter('from', e.target.value)} />
        </label>
        <label className="field">
          <span>Sampai tanggal</span>
          <input id="ai-req-to" type="date" value={filters.to} onChange={e => setFilter('to', e.target.value)} />
        </label>
      </div>

      {list.error && <p role="alert" className="notice danger"><span>{list.error}</span></p>}
      <p className="hint" aria-live="polite">{loading ? 'Memuat…' : `${num(list.total)} request`}</p>

      <div className="table-scroll">
        <table className="data-table">
          <caption>Request ke AI, terbaru dulu</caption>
          <thead>
            <tr>
              <th scope="col">Waktu</th>
              <th scope="col">Jenis</th>
              <th scope="col">Status</th>
              <th scope="col">Model</th>
              <th scope="col" className="num">Latensi</th>
              <th scope="col" className="num">Token masuk</th>
              <th scope="col" className="num">Token keluar</th>
              <th scope="col" className="num">Token total</th>
              <th scope="col" className="num">Case</th>
            </tr>
          </thead>
          <tbody>
            {list.items.length === 0 && !loading && (
              <tr>
                <td colSpan={9} className="hint">
                  {filtered ? 'Tidak ada request yang cocok dengan filter.' : 'Belum ada request ke AI'}
                </td>
              </tr>
            )}
            {list.items.map(i => (
              <tr key={i.id}>
                <td>{fmtTime(i.waktu)}</td>
                <td>
                  <span className={`badge ${i.jenis === 'HealthCheck' ? 'info' : 'neutral'}`}>
                    {i.jenis === 'HealthCheck' ? 'Cek koneksi' : 'Klasifikasi'}
                  </span>
                </td>
                <td>
                  <div className="cell-stack">
                    <span className={`badge ${i.status === 'Success' ? 'success' : 'danger'}`}>
                      <span className="dot" aria-hidden="true" />{i.status === 'Success' ? 'Sukses' : 'Gagal'}
                    </span>
                    {i.kodeError && <span className="mono">{i.kodeError}</span>}
                    {i.pesanError && <span className="hint">{i.pesanError}</span>}
                  </div>
                </td>
                <td>
                  <div className="cell-stack">
                    <span>{i.model}</span>
                    <span className="hint">{i.host || i.provider}</span>
                  </div>
                </td>
                <td className="num">{i.latensiMs == null ? '—' : `${num(i.latensiMs)} ms`}</td>
                <td className="num">{num(i.tokenMasuk)}</td>
                <td className="num">{num(i.tokenKeluar)}</td>
                <td className="num">{num(i.tokenTotal)}</td>
                <td className="num">{i.caseId ?? '—'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {pages > 1 && (
        <nav aria-label="Halaman riwayat request AI" className="pager">
          <button type="button" disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="outline-button">
            <ChevronLeft size={14} aria-hidden="true" /> Sebelumnya
          </button>
          <span>Halaman {page} / {pages}</span>
          <button type="button" disabled={page >= pages} onClick={() => setPage(p => p + 1)} className="outline-button">
            Berikutnya <ChevronRight size={14} aria-hidden="true" />
          </button>
        </nav>
      )}
    </section>
  )
}
