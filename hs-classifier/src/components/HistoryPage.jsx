import { useCallback, useEffect, useState } from 'react'
import { ChevronDown, ChevronLeft, ChevronRight, Search } from 'lucide-react'
import ResultPanel from './ResultPanel'
import { listCases, loadCase } from '../services/apiClient'

// Riwayat klasifikasi — list stored cases and reopen one with its reasoning and tariffs.

const STATUS_LABEL = {
  Determined:        ['Ditentukan', 'success'],
  NeedClarification: ['Perlu klarifikasi', 'warning'],
  NeedExpertReview:  ['Review ahli', 'danger'],
  Rejected:          ['Ditolak ahli', 'neutral'],
}

export function StatusBadge({ status }) {
  const [label, tone] = STATUS_LABEL[status] || [status, 'neutral']
  return <span className={`badge ${tone}`}><span className="dot" aria-hidden="true" />{label}</span>
}

const PAGE_SIZE = 20
const fmtDate = (d) => new Date(d).toLocaleString('id-ID', { dateStyle: 'medium', timeStyle: 'short' })

export default function HistoryPage({ showUser = false, initialStatus = '', title = 'Riwayat Klasifikasi',
  subtitle = 'Semua klasifikasi yang pernah Anda jalankan', eyebrow = 'ACTIVITY LOG', renderDetail }) {
  const [filters, setFilters] = useState({ status: initialStatus, q: '', from: '', to: '' })
  const [page, setPage] = useState(1)
  const [reloadKey, setReloadKey] = useState(0)
  // Response tagged with the request it answers, so "loading" is derived instead of set inside the effect
  const [list, setList] = useState({ key: null, items: [], total: 0, error: null })
  const [selectedId, setSelectedId] = useState(null)
  const [detail, setDetail] = useState(null)
  const [loadingDetail, setLoadingDetail] = useState(false)

  const requestKey = JSON.stringify([filters, page, reloadKey])
  useEffect(() => {
    let alive = true
    listCases({ ...filters, page, pageSize: PAGE_SIZE }).then(
      d => alive && setList({ key: requestKey, items: d.items, total: d.total, error: null }),
      e => alive && setList(l => ({ ...l, key: requestKey, error: e.message })))
    return () => { alive = false }
  }, [filters, page, requestKey])
  const loadingList = list.key !== requestKey
  const data = list

  const open = useCallback(async (id) => {
    setSelectedId(id); setLoadingDetail(true); setDetail(null)
    try { setDetail(await loadCase(id)) }
    catch (e) { setDetail({ error: e.message }) }
    finally { setLoadingDetail(false) }
  }, [])

  // After an expert decision the list and the open case must both reflect it
  const afterChange = async () => { setReloadKey(k => k + 1); if (selectedId) await open(selectedId) }
  const listError = list.error

  const setFilter = (k, v) => { setPage(1); setFilters(f => ({ ...f, [k]: v })) }
  const pages = Math.max(1, Math.ceil(data.total / PAGE_SIZE))

  return (
    <div className="view">
      <div className="page-title">
        <div>
          <div className="eyebrow">{eyebrow}</div>
          <h1>{title}</h1>
          <p>{subtitle}</p>
        </div>
      </div>

      <div className="panel toolbar">
        <label className="field toolbar-search">
          <span>Cari nama barang / kode HS</span>
          <div className="search">
            <Search size={18} aria-hidden="true" />
            <input id="hist-q" type="search" value={filters.q} onChange={e => setFilter('q', e.target.value)}
              placeholder="mis. Laptop atau 8471" />
          </div>
        </label>
        <label className="field">
          <span>Status</span>
          <div className="select-wrap">
            <select id="hist-status" value={filters.status} onChange={e => setFilter('status', e.target.value)}>
              <option value="">Semua status</option>
              {Object.entries(STATUS_LABEL).map(([v, [l]]) => <option key={v} value={v}>{l}</option>)}
            </select>
            <ChevronDown size={15} aria-hidden="true" />
          </div>
        </label>
        <label className="field">
          <span>Dari tanggal</span>
          <input id="hist-from" type="date" value={filters.from} onChange={e => setFilter('from', e.target.value)} />
        </label>
        <label className="field">
          <span>Sampai tanggal</span>
          <input id="hist-to" type="date" value={filters.to} onChange={e => setFilter('to', e.target.value)} />
        </label>
      </div>

      {listError && <p role="alert" className="notice danger"><span>{listError}</span></p>}
      <p className="hint" aria-live="polite">{loadingList ? 'Memuat…' : `${data.total} case`}</p>

      <div className="master-detail">
        <section aria-label="Daftar case">
          <div className="case-list">
            {data.items.length === 0 && !loadingList && (
              <p className="hint">Belum ada case</p>
            )}
            {data.items.map(c => (
              <button key={c.id} onClick={() => open(c.id)}
                aria-current={selectedId === c.id ? 'true' : undefined}
                className={selectedId === c.id ? 'case-card selected' : 'case-card'}>
                <div className="case-card-top">
                  <span className="case-date">{fmtDate(c.createdAt)}</span>
                  <StatusBadge status={c.status} />
                </div>
                <strong>{c.goodsName || `Case #${c.id}`}</strong>
                <div className="case-card-bottom">
                  <code>{c.finalCode || '—'}</code>
                  {showUser && c.user && <span>{c.user}</span>}
                </div>
              </button>
            ))}
          </div>

          {pages > 1 && (
            <nav aria-label="Halaman" className="pager">
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)} className="outline-button">
                <ChevronLeft size={14} aria-hidden="true" /> Sebelumnya
              </button>
              <span>Halaman {page} / {pages}</span>
              <button disabled={page >= pages} onClick={() => setPage(p => p + 1)} className="outline-button">
                Berikutnya <ChevronRight size={14} aria-hidden="true" />
              </button>
            </nav>
          )}
        </section>

        <section aria-label="Detail case" className="detail-column">
          {!selectedId && (
            <div className="panel">
              <div className="empty-state compact">
                <p>Pilih case di sebelah kiri untuk melihat detail, reasoning, dan tarif.</p>
              </div>
            </div>
          )}
          {selectedId && (
            <>
              {detail && !detail.error && <CaseDetail detail={detail} />}
              {renderDetail && detail && !detail.error && renderDetail(detail, afterChange)}
              <ResultPanel result={detail} isLoading={loadingDetail} onChanged={afterChange} />
            </>
          )}
        </section>
      </div>
    </div>
  )
}

const FACT_LABEL = {
  commercial_name: 'Nama dagang', generic_name: 'Nama generik', primary_function: 'Fungsi utama',
  material: 'Material', assembled_status: 'Kondisi', primary_use: 'Sektor', power_source: 'Cara kerja',
  mobility: 'Mobilitas', operating_area: 'Area operasi', supporting_documents: 'Spesifikasi',
  subheading_choice: 'Pilihan subpos', part_accessory: 'Part/aksesori', set_kit: 'Set/kit', packaging_container: 'Wadah/kemasan',
  bulk_unit: 'Curah', used_condition: 'Barang bekas', waste_scrap: 'Sisa/skrap', multifunction: 'Multifungsi',
}

function CaseDetail({ detail }) {
  const facts = Object.entries(detail.facts || {}).filter(([, v]) => v)
  return (
    <div className="detail-card">
      <div className="detail-header">
        <div>
          <span className="case-id">Case #{detail.caseId}</span>
          <h2>{detail.facts?.commercial_name || 'Detail case'}</h2>
          <p>{detail.facts?.generic_name || ''}</p>
        </div>
      </div>

      <div className="result-section">
        <div className="subheading"><div>Fakta barang</div></div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr><th scope="col">Atribut</th><th scope="col">Nilai</th></tr>
            </thead>
            <tbody>
              {facts.map(([k, v]) => (
                <tr key={k}>
                  <td>{FACT_LABEL[k] || k}</td>
                  <td>{v}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {detail.corrections?.length > 0 && (
        <div className="result-section">
          <div className="subheading"><div>Riwayat keputusan ahli</div></div>
          <div className="table-scroll">
            <table className="data-table">
              <thead>
                <tr>
                  <th scope="col">Aksi</th>
                  <th scope="col">Kode</th>
                  <th scope="col">Alasan</th>
                  <th scope="col">Waktu</th>
                </tr>
              </thead>
              <tbody>
                {detail.corrections.map((e, i) => (
                  <tr key={i}>
                    <td className="mono">{e.action}</td>
                    <td className="mono">{e.afterCode || '—'}</td>
                    <td>{e.reason}</td>
                    <td>{fmtDate(e.createdAt)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      )}
    </div>
  )
}
