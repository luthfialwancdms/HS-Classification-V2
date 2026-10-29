import { useCallback, useEffect, useState } from 'react'
import ResultPanel from './ResultPanel'
import { listCases, loadCase } from '../services/apiClient'

// Riwayat klasifikasi — list stored cases and reopen one with its reasoning and tariffs.

const STATUS_LABEL = {
  Determined:        ['Ditentukan', 'bg-green-100 text-green-800'],
  NeedClarification: ['Perlu klarifikasi', 'bg-amber-100 text-amber-800'],
  NeedExpertReview:  ['Review ahli', 'bg-red-100 text-red-800'],
  Rejected:          ['Ditolak ahli', 'bg-gray-200 text-gray-700'],
}

export function StatusBadge({ status }) {
  const [label, cls] = STATUS_LABEL[status] || [status, 'bg-gray-100 text-gray-700']
  return <span className={`text-xs px-2 py-0.5 rounded-full font-medium whitespace-nowrap ${cls}`}>{label}</span>
}

const PAGE_SIZE = 20
const fmtDate = (d) => new Date(d).toLocaleString('id-ID', { dateStyle: 'medium', timeStyle: 'short' })

export default function HistoryPage({ showUser = false, initialStatus = '', title = 'Riwayat Klasifikasi',
  subtitle = 'Semua klasifikasi yang pernah Anda jalankan', renderDetail }) {
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
    <main className="max-w-7xl mx-auto px-6 py-8">
      <div className="mb-4">
        <h2 className="text-lg font-semibold text-gray-900">{title}</h2>
        <p className="text-sm text-gray-500">{subtitle}</p>
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
        <section aria-label="Daftar case" className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
          <div className="grid grid-cols-2 gap-2 mb-4">
            <div className="col-span-2">
              <label htmlFor="hist-q" className="block text-xs text-gray-600 mb-1">Cari nama barang / kode HS</label>
              <input id="hist-q" type="search" value={filters.q} onChange={e => setFilter('q', e.target.value)}
                placeholder="mis. Laptop atau 8471" className="w-full text-sm border border-gray-300 rounded p-2" />
            </div>
            <div className="col-span-2">
              <label htmlFor="hist-status" className="block text-xs text-gray-600 mb-1">Status</label>
              <select id="hist-status" value={filters.status} onChange={e => setFilter('status', e.target.value)}
                className="w-full text-sm border border-gray-300 rounded p-2 bg-white">
                <option value="">Semua status</option>
                {Object.entries(STATUS_LABEL).map(([v, [l]]) => <option key={v} value={v}>{l}</option>)}
              </select>
            </div>
            <div>
              <label htmlFor="hist-from" className="block text-xs text-gray-600 mb-1">Dari tanggal</label>
              <input id="hist-from" type="date" value={filters.from} onChange={e => setFilter('from', e.target.value)}
                className="w-full text-sm border border-gray-300 rounded p-2" />
            </div>
            <div>
              <label htmlFor="hist-to" className="block text-xs text-gray-600 mb-1">Sampai tanggal</label>
              <input id="hist-to" type="date" value={filters.to} onChange={e => setFilter('to', e.target.value)}
                className="w-full text-sm border border-gray-300 rounded p-2" />
            </div>
          </div>

          {listError && <p role="alert" className="text-sm text-red-600 mb-2">{listError}</p>}
          <p className="text-xs text-gray-500 mb-2" aria-live="polite">
            {loadingList ? 'Memuat…' : `${data.total} case`}
          </p>

          <ul className="flex flex-col divide-y divide-gray-100">
            {data.items.length === 0 && !loadingList && (
              <li className="py-6 text-center text-sm text-gray-400">Belum ada case</li>
            )}
            {data.items.map(c => (
              <li key={c.id}>
                <button onClick={() => open(c.id)} aria-current={selectedId === c.id ? 'true' : undefined}
                  className={`w-full text-left py-3 px-2 rounded-lg flex flex-col gap-1 hover:bg-gray-50 ${selectedId === c.id ? 'bg-blue-50' : ''}`}>
                  <span className="flex items-center justify-between gap-2">
                    <span className="text-sm font-medium text-gray-900 truncate">{c.goodsName || `Case #${c.id}`}</span>
                    <StatusBadge status={c.status} />
                  </span>
                  <span className="flex items-center gap-3 text-xs text-gray-500">
                    <span className="font-mono text-blue-700">{c.finalCode || '—'}</span>
                    <span>{fmtDate(c.createdAt)}</span>
                    {showUser && c.user && <span className="truncate">{c.user}</span>}
                  </span>
                </button>
              </li>
            ))}
          </ul>

          {pages > 1 && (
            <nav aria-label="Halaman" className="flex items-center justify-between mt-4 text-sm">
              <button disabled={page <= 1} onClick={() => setPage(p => p - 1)}
                className="px-3 py-1 rounded border border-gray-300 disabled:opacity-40">← Sebelumnya</button>
              <span className="text-gray-500">Halaman {page} / {pages}</span>
              <button disabled={page >= pages} onClick={() => setPage(p => p + 1)}
                className="px-3 py-1 rounded border border-gray-300 disabled:opacity-40">Berikutnya →</button>
            </nav>
          )}
        </section>

        <section aria-label="Detail case" className="lg:sticky lg:top-24 flex flex-col gap-4">
          {!selectedId && (
            <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-8 text-center text-sm text-gray-500">
              Pilih case di sebelah kiri untuk melihat detail, reasoning, dan tarif.
            </div>
          )}
          {selectedId && (
            <>
              {detail && !detail.error && <CaseFacts detail={detail} />}
              {renderDetail && detail && !detail.error && renderDetail(detail, afterChange)}
              <ResultPanel result={detail} isLoading={loadingDetail} onChanged={afterChange} />
            </>
          )}
        </section>
      </div>
    </main>
  )
}

const FACT_LABEL = {
  commercial_name: 'Nama dagang', generic_name: 'Nama generik', primary_function: 'Fungsi utama',
  material: 'Material', assembled_status: 'Kondisi', primary_use: 'Sektor', power_source: 'Cara kerja',
  mobility: 'Mobilitas', operating_area: 'Area operasi', supporting_documents: 'Spesifikasi',
  subheading_choice: 'Pilihan subpos', part_accessory: 'Part/aksesori', set_kit: 'Set/kit', packaging_container: 'Wadah/kemasan',
  bulk_unit: 'Curah', used_condition: 'Barang bekas', waste_scrap: 'Sisa/skrap', multifunction: 'Multifungsi',
}

function CaseFacts({ detail }) {
  const facts = Object.entries(detail.facts || {}).filter(([, v]) => v)
  return (
    <div className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
      <h3 className="text-sm font-semibold text-gray-700 mb-2">Fakta barang · Case #{detail.caseId}</h3>
      <dl className="grid grid-cols-3 gap-x-3 gap-y-1 text-sm">
        {facts.map(([k, v]) => (
          <div key={k} className="contents">
            <dt className="text-gray-500 col-span-1">{FACT_LABEL[k] || k}</dt>
            <dd className="text-gray-900 col-span-2 break-words">{v}</dd>
          </div>
        ))}
      </dl>
      {detail.corrections?.length > 0 && (
        <>
          <h4 className="text-xs font-semibold text-gray-700 uppercase tracking-wide mt-4 mb-1">Riwayat keputusan ahli</h4>
          <ul className="flex flex-col gap-1">
            {detail.corrections.map((e, i) => (
              <li key={i} className="text-xs text-gray-600">
                <span className="font-mono font-semibold">{e.action}</span>{' '}
                {e.afterCode && <span className="font-mono">→ {e.afterCode}</span>} · {e.reason}
                <span className="text-gray-400"> · {fmtDate(e.createdAt)}</span>
              </li>
            ))}
          </ul>
        </>
      )}
    </div>
  )
}
