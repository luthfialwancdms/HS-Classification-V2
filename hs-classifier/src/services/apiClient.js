/**
 * apiClient.js — V2 client for the C# backend (Tasks 18.1, 18.3).
 * All HS codes, descriptions and rates come from the backend database; the
 * browser no longer calls any LLM directly.
 */

const BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5284'

// ─── Auth helpers ─────────────────────────────────────────────────────────────

export function getToken()  { return localStorage.getItem('hs_token') }
export function getRole()   { return localStorage.getItem('hs_role') }
export function clearToken(){ localStorage.removeItem('hs_token'); localStorage.removeItem('hs_role') }

/** Fired when the session is missing/expired so the app can show the login screen. */
export const AUTH_EVENT = 'hs:auth-required'

function authHeaders() {
  const token = getToken()
  return token ? { Authorization: `Bearer ${token}` } : {}
}

// ─── Fetch with friendly errors + retry on transient failures (Task 18.3) ────

const FRIENDLY_STATUS = {
  400: 'Data yang dikirim tidak valid. Periksa kembali isian form.',
  401: 'Sesi Anda berakhir. Silakan login kembali.',
  403: 'Anda tidak memiliki akses untuk aksi ini.',
  404: 'Data tidak ditemukan di server.',
  500: 'Terjadi kesalahan di server. Coba beberapa saat lagi.',
}
const TRANSIENT = new Set([502, 503, 504])
const MAX_RETRIES = 2

const sleep = ms => new Promise(r => setTimeout(r, ms))

export async function apiFetch(path, options = {}, attempt = 0) {
  const isForm = options.body instanceof FormData
  let res
  try {
    res = await fetch(`${BASE_URL}${path}`, {
      ...options,
      headers: {
        ...(isForm ? {} : { 'Content-Type': 'application/json' }),
        ...authHeaders(),
        ...(options.headers || {}),
      },
    })
  } catch {
    // Network error (server down / CORS / offline)
    if (attempt < MAX_RETRIES) { await sleep(500 * 2 ** attempt); return apiFetch(path, options, attempt + 1) }
    throw new Error('Tidak dapat terhubung ke server backend. Pastikan API berjalan.')
  }

  if (TRANSIENT.has(res.status) && attempt < MAX_RETRIES) {
    await sleep(500 * 2 ** attempt)
    return apiFetch(path, options, attempt + 1)
  }

  if (res.status === 401 && path !== '/api/auth/login') {
    clearToken()
    dispatchEvent(new Event(AUTH_EVENT))
  }

  if (!res.ok) {
    const body = await res.json().catch(() => ({}))
    throw new Error(body?.message || FRIENDLY_STATUS[res.status] || `Kesalahan API (${res.status})`)
  }

  const type = res.headers.get('content-type') || ''
  return type.includes('application/json') ? res.json() : res.blob()
}

// ─── Auth ─────────────────────────────────────────────────────────────────────

export async function login(email, password) {
  const data = await apiFetch('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ email, password }),
  })
  // ponytail: token in localStorage is readable by any XSS; upgrade path is an httpOnly cookie issued by the API.
  localStorage.setItem('hs_token', data.token)
  localStorage.setItem('hs_role', data.role)
  return data
}

// ─── Classification ────────────────────────────────────────────────────────────

/** Map V1 form fields → backend fact types. Unknown facts stay null (never defaulted). */
function mapFormToFacts(form) {
  return {
    commercial_name:   form.nama_dagang       || null,
    generic_name:      form.nama_generik      || null,
    material:          form.material_utama?.join(', ') || null,
    primary_function:  form.fungsi_utama      || null,
    mobility:          form.mobilitas         || null,
    operating_area:    form.area_operasi      || null,
    primary_use:       form.sektor            || null,
    assembled_status:  mapKondisi(form.kondisi_impor),
    // "Kondisi Saat Impor" can itself say part / set / mixture; the explicit questions win when answered
    part_accessory:    kondisiIs(form, 'Aksesori') ? 'accessory'
                       : form.apakah_part === 'Ya' || kondisiIs(form, 'Bagian/Part') ? 'part'
                       : form.apakah_part === 'Tidak' ? 'complete' : null,
    set_kit:           form.apakah_set === 'Ya' || kondisiIs(form, 'Set/Kit') ? 'retail_set' : null,
    mixture_composite: kondisiIs(form, 'Campuran') ? 'mixture' : null,
    packaging_container: kondisiIs(form, 'Wadah/Kemasan') ? 'with_goods' : null,
    bulk_unit:         kondisiIs(form, 'Curah') ? 'bulk' : null,
    used_condition:    kondisiIs(form, 'Barang bekas') ? 'used' : null,
    waste_scrap:       kondisiIs(form, 'Sisa/Skrap') ? 'waste_scrap' : null,
    multifunction:     form.ada_komponen_tambahan?.startsWith('Ya') ? 'yes' : null,
    power_source:      form.cara_kerja?.join(', ') || null,
    supporting_documents: form.spesifikasi    || null,
  }
}

const kondisiIs = (form, label) => (form.kondisi_impor || '').startsWith(label)

/** Only real assembly states; part/set/mixture/bulk options are mapped to their own facts, not "complete". */
function mapKondisi(kondisi) {
  if (!kondisi) return null
  if (kondisi.includes('CKD')) return 'CKD'
  if (kondisi.includes('SKD')) return 'SKD'
  if (kondisi.startsWith('Belum selesai')) return 'unfinished'
  if (kondisi.startsWith('Belum lengkap')) return 'incomplete'
  if (kondisi.startsWith('Lengkap')) return 'complete'
  return null
}

/** Start a classification from the form. */
export function classifyWithBackend(form) {
  return runClassification('/api/classification/start', mapFormToFacts(form))
}

/** Answer clarification questions; the backend merges them into the stored case facts. */
export function submitAnswers(caseId, answers) {
  return runClassification(`/api/classification/${caseId}/facts`, answers)
}

async function runClassification(path, facts) {
  const data = await apiFetch(path, {
    method: 'POST',
    body: JSON.stringify({ facts }),
  })

  if (data.status === 'NEED_CLARIFICATION') {
    return {
      status:         'NEED_CLARIFICATION',
      caseId:         data.caseId,
      message:        data.message,
      questions:      data.questions || [],
      candidateCodes: data.candidateCodes || [],
    }
  }
  if (data.status === 'ERROR') throw new Error(data.message || 'Klasifikasi gagal divalidasi.')

  // Reasoning chain is optional for display; a failure here must not hide the result.
  const reasoningChain = data.caseId
    ? await getReasoning(data.caseId).catch(() => [])
    : []
  return toResult({ ...data, reasoningChain })
}

/** Shared ResultPanel shape for a fresh classification and a case reopened from history. */
function toResult(d) {
  const expert = d.status === 'NEED_EXPERT_REVIEW'
  return {
    status:     d.status,
    hs_code:    d.finalCode,
    uraian:     d.finalDescription,
    confidence: mapConfidence(d.confidence),
    confidenceScore:  d.confidenceScore ?? null,
    confidenceDetail: d.confidenceDetail ?? null,
    perlu_expert_review:  expert,
    alasan_expert_review: expert ? (d.message || 'Hasil berisiko tinggi, butuh review ahli kepabeanan.') : null,
    candidateCodes:  d.candidateCodes || [],
    caseId:          d.caseId,
    tariffs:         d.tariffs,
    reasoningChain:  d.reasoningChain || [],
    eliminatedCount: d.eliminatedCount,
    corrections:     d.corrections || [],
    facts:           d.facts,
  }
}

// Case status is stored as an enum name; the live API speaks SCREAMING_SNAKE.
const STATUS_API = { Determined: 'DETERMINED', NeedClarification: 'NEED_CLARIFICATION',
  NeedExpertReview: 'NEED_EXPERT_REVIEW', Rejected: 'REJECTED' }

// ─── History (Riwayat) ────────────────────────────────────────────────────────

export function listCases({ status, q, from, to, page = 1, pageSize = 20 } = {}) {
  const p = new URLSearchParams({ page, pageSize })
  if (status) p.set('status', status)
  if (q) p.set('q', q)
  if (from) p.set('from', from)
  if (to) p.set('to', to)
  return apiFetch(`/api/classification/cases?${p}`)
}

/** Reopen a stored case in the same shape as a live result (tariffs only exist with a final code). */
export async function loadCase(caseId) {
  const [c, reasoningChain] = await Promise.all([
    apiFetch(`/api/classification/${caseId}`),
    getReasoning(caseId).catch(() => []),
  ])
  const tariffs = c.finalCode ? await apiFetch(`/api/classification/${caseId}/tariffs`).catch(() => null) : null
  return toResult({ ...c, caseId: c.id, status: STATUS_API[c.status] || c.status, reasoningChain, tariffs })
}

export function getReasoning(caseId) {
  return apiFetch(`/api/classification/${caseId}/reasoning`)
}

// ─── Expert review ────────────────────────────────────────────────────────────

const post = (path, body) => apiFetch(path, { method: 'POST', body: JSON.stringify(body) })

export const approveCase   = (caseId, code, reason) => post(`/api/review/${caseId}/approve`, { code, reason })
export const rejectCase    = (caseId, reason)       => post(`/api/review/${caseId}/reject`, { reason })
export const requestReview = (caseId, reason)       => post(`/api/classification/${caseId}/request-review`, { reason })

/** Official BTKI line for a code, or null when it does not exist in the active version. */
export const lookupCode = (code) =>
  apiFetch(`/api/btki/${encodeURIComponent(code)}`).catch(() => null)

// ─── BTKI search ──────────────────────────────────────────────────────────────

export function searchBtki(query, limit = 10) {
  return apiFetch(`/api/btki/search?q=${encodeURIComponent(query)}&limit=${limit}`)
}

export function getBtkiStats() {
  return apiFetch('/api/btki/stats')
}

// ─── Helpers ──────────────────────────────────────────────────────────────────

function mapConfidence(c) {
  const upper = (c || '').toUpperCase()
  if (upper === 'HIGH')   return 'TINGGI'
  if (upper === 'MEDIUM') return 'SEDANG'
  return 'RENDAH'
}
