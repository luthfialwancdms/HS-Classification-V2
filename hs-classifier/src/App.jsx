import { useEffect, useRef, useState } from 'react'
import { AlertTriangle, ChevronDown, ClipboardCheck, History, LayoutDashboard, LogOut, Menu, MessageCircleQuestion, Sparkles } from 'lucide-react'
import ClassificationForm from './components/ClassificationForm'
import Modal from './components/Modal'
import ResultPanel from './components/ResultPanel'
import AdminDashboard from './components/AdminDashboard'
import HistoryPage from './components/HistoryPage'
import ReviewPage from './components/ReviewPage'
import QuotaMeter from './components/QuotaMeter'
import { AUTH_EVENT, classifyWithBackend, clearToken, getQuota, getRole, getToken, login, submitAnswers } from './services/apiClient'

// view → [path, label, roles allowed (null = everyone)]
const VIEWS = {
  classify: ['/',        'Klasifikasi',       null],
  history:  ['/riwayat', 'Riwayat',           null],
  review:   ['/review',  'Review Ahli',       ['Admin', 'ExpertReviewer']],
  admin:    ['/admin',   'Admin Data Center', ['Admin']],
}
const viewFromPath = () =>
  Object.entries(VIEWS).find(([k, [p]]) => k !== 'classify' && location.pathname.startsWith(p))?.[0] ?? 'classify'

const NAV_ICON = { classify: Sparkles, history: History, review: ClipboardCheck, admin: LayoutDashboard }
const cx = (...parts) => parts.filter(Boolean).join(' ')

export default function App() {
  const [view,      setView]      = useState(viewFromPath)
  const [result,    setResult]    = useState(null)
  const [isLoading, setIsLoading] = useState(false)
  const [clarifyState, setClarifyState] = useState(null) // { facts, questions, message, candidateCodes }

  const [authed, setAuthed] = useState(() => !!getToken())
  // Pemakaian kuota harian pengguna ini (n/10). Dimuat saat login dan dimuat ulang setelah setiap analisis.
  const [quota, setQuota] = useState(null)
  const [quotaTick, setQuotaTick] = useState(0)
  const [menuOpen, setMenuOpen] = useState(false) // tampilan saja: drawer navigasi di mobile
  const [userMenuOpen, setUserMenuOpen] = useState(false) // tampilan saja: dropdown user
  const burgerRef = useRef(null)
  const userMenuRef = useRef(null)
  const userMenuItemRef = useRef(null)
  const role = authed ? getRole() : null
  const allowed = (v) => !VIEWS[v][2] || VIEWS[v][2].includes(role)
  const isAdmin = role === 'Admin'

  useEffect(() => {
    if (!authed) return
    let alive = true
    getQuota().then(q => { if (alive) setQuota(q) }).catch(() => {})   // angka kuota bersifat tambahan: gagal memuat tidak mengganggu
    return () => { alive = false }
  }, [authed, quotaTick])

  useEffect(() => {
    const onPop = () => setView(viewFromPath())
    const onAuth = () => setAuthed(false)
    addEventListener('popstate', onPop)
    addEventListener(AUTH_EVENT, onAuth)
    return () => { removeEventListener('popstate', onPop); removeEventListener(AUTH_EVENT, onAuth) }
  }, [])

  // Tampilan saja: tutup drawer mobile lalu kembalikan fokus ke tombol pembukanya.
  const closeMenu = () => { setMenuOpen(false); burgerRef.current?.focus() }

  // Tampilan saja: dropdown user (Esc / klik di luar menutup, fokus balik ke trigger).
  const closeUserMenu = () => { setUserMenuOpen(false); userMenuRef.current?.querySelector('.user-menu-trigger')?.focus() }

  useEffect(() => {
    if (!userMenuOpen) return
    userMenuItemRef.current?.focus()
    const onDown = (e) => { if (!userMenuRef.current?.contains(e.target)) setUserMenuOpen(false) }
    const onKey = (e) => { if (e.key === 'Escape') closeUserMenu() }
    document.addEventListener('mousedown', onDown)
    document.addEventListener('keydown', onKey)
    return () => { document.removeEventListener('mousedown', onDown); document.removeEventListener('keydown', onKey) }
  }, [userMenuOpen])

  // REV2 butir 2 (tampilan saja): modal klarifikasi. Menutup hanya menyembunyikan; pertanyaan dan jawaban
  // sementara tetap ada. Turunan dari objek state (tanpa setState di effect): respons klarifikasi baru
  // otomatis terbuka. Error API saat Kirim Jawaban tampil di dalam modal (modal tetap terbuka).
  const [dismissedFor, setDismissedFor] = useState(null)
  const analyzeRef = useRef(null)
  const clarifyOpen = !!clarifyState && dismissedFor !== clarifyState
  const closeClarify = () => setDismissedFor(clarifyState)
  const resumeClarify = () => setDismissedFor(null)

  const clearResult = () => setResult(null)
  // REV2 butir 1: Reset form mengosongkan hasil, pertanyaan klarifikasi, dan (karena banner
  // ikut hilang) jawaban sementaranya. Hanya setter yang sudah ada; tanpa panggilan API.
  const resetAnalysis = () => { clearResult(); setClarifyState(null) }
  const hasFinalResult = !!result && !result.error && ['DETERMINED', 'NEED_EXPERT_REVIEW', 'REJECTED'].includes(result.status)

  const logout = () => { clearToken(); setAuthed(false); setResult(null); setClarifyState(null); setQuota(null) }

  const switchView = (v) => {
    history.pushState(null, '', VIEWS[v][0])
    setView(v)
    setMenuOpen(false)
  }

  const handle = async (run) => {
    setIsLoading(true)
    setResult(null)
    try {
      const res = await run()
      if (res.status === 'NEED_CLARIFICATION') setClarifyState(res)
      else { setClarifyState(null); setResult(res) }
    } catch (err) {
      setResult({ error: err.message })
    } finally {
      setIsLoading(false)
      setQuotaTick(t => t + 1)
    }
  }

  const handleSubmit  = (form)    => { setClarifyState(null); handle(() => classifyWithBackend(form)) }
  const handleAnswers = (answers) => handle(() => submitAnswers(clarifyState.caseId, answers))

  if (!authed) return <LoginScreen onLoggedIn={() => setAuthed(true)} />

  return (
    <div className="app-shell">
      <header className="topbar">
        <div className="brand">
          <button ref={burgerRef} type="button" className="mobile-menu" aria-label="Buka menu navigasi"
            aria-expanded={menuOpen} aria-controls="app-nav"
            onClick={() => setMenuOpen(open => !open)}>
            <Menu size={20} aria-hidden="true" />
          </button>
          <div className="brand-mark"><span>HS</span></div>
          <div>
            <div className="brand-name">HS Intelligence <span className="beta">V2 · DATABASE MODE</span></div>
            <div className="brand-sub">BTKI 2022 · PMK 26/2022</div>
          </div>
        </div>
        <div className="top-actions">
          <div className="user-menu" ref={userMenuRef}>
            <button type="button" className="user-menu-trigger" id="user-menu-trigger"
              aria-haspopup="menu" aria-expanded={userMenuOpen} aria-controls="user-menu"
              onClick={() => setUserMenuOpen(open => !open)}>
              <span className="avatar">{String(role || '?').slice(0, 2).toUpperCase()}</span>
              <span className="user-copy"><strong>{role}</strong><span>Sesi aktif</span></span>
              <ChevronDown size={16} className="muted" aria-hidden="true" />
            </button>
            {userMenuOpen && (
              <div className="user-dropdown" role="menu" id="user-menu" aria-labelledby="user-menu-trigger">
                <p className="user-dropdown-role">Role: <strong>{role}</strong></p>
                <button type="button" role="menuitem" ref={userMenuItemRef} className="user-dropdown-item"
                  onClick={() => { setUserMenuOpen(false); logout() }}>
                  <LogOut size={15} aria-hidden="true" /> Logout
                </button>
              </div>
            )}
          </div>
        </div>
      </header>

      <div className="body-shell">
        {menuOpen && (
          <button type="button" className="sidebar-overlay" aria-hidden="true" tabIndex={-1} onClick={closeMenu} />
        )}
        <aside id="app-nav" className={cx('sidebar', menuOpen && 'sidebar-open')}
          onKeyDown={e => { if (e.key === 'Escape') closeMenu() }}>
          <div className="nav-label">MENU</div>
          <nav aria-label="Navigasi utama">
            {Object.keys(VIEWS).filter(allowed).map(v => {
              const Icon = NAV_ICON[v]
              return (
                <button key={v} onClick={() => switchView(v)}
                  aria-current={view === v ? 'page' : undefined}
                  className={cx('nav-item', view === v && 'active')}>
                  <Icon size={18} aria-hidden="true" />
                  <span>{VIEWS[v][1]}</span>
                </button>
              )
            })}
          </nav>
          <div className="sidebar-bottom">
            <div className="version-card">
              <div className="version-top"><span className="status-dot" /> <span>BTKI 2022</span><span className="live">V2</span></div>
              <p>Mode database · kode &amp; tarif dibaca dari backend</p>
            </div>
          </div>
        </aside>

        <main className="main-content">
          {view === 'admin' && isAdmin ? <AdminDashboard />
            : view === 'history' ? <HistoryPage showUser={['Admin', 'ExpertReviewer'].includes(role)} />
            : view === 'review' && allowed('review') ? <ReviewPage />
            : (
            <div className="view">
              <div className="page-title">
                <div>
                  <div className="eyebrow">AI-POWERED CLASSIFICATION</div>
                  <h1>Klasifikasi HS Code</h1>
                  <p>Identifikasi kode HS barang impor berdasarkan BTKI 2022 dan KUM HS.</p>
                </div>
                <QuotaMeter quota={quota} />
              </div>

              <div className="classify-grid">
                <ClassificationForm onSubmit={handleSubmit} isLoading={isLoading} onReset={resetAnalysis}
                  submitRef={analyzeRef}
                  resumeAction={clarifyState && !clarifyOpen && (
                    <button type="button" className="outline-button resume-button" onClick={resumeClarify}>
                      <MessageCircleQuestion size={16} aria-hidden="true" /> Lanjutkan menjawab
                    </button>
                  )} />

                {clarifyState && (
                  <ClarificationBanner
                    key={clarifyState.questions.map(q => q.questionText).join('|')}
                    state={clarifyState}
                    onSubmit={handleAnswers}
                    isLoading={isLoading}
                    open={clarifyOpen}
                    onClose={closeClarify}
                    returnFocusRef={analyzeRef}
                    errorResult={result?.error ? result : null}
                  />
                )}

                {isLoading && !clarifyOpen && (
                  <div role="status" aria-live="polite" className="panel loading-panel">
                    <div className="spinner" aria-hidden="true" />
                    <p>Menjalankan KUM HS 1–6 terhadap database BTKI...</p>
                    <p className="hint">Ekstraksi fakta → kandidat → aturan → validasi → tarif</p>
                  </div>
                )}

                {result?.error && !clarifyOpen && (
                  <p role="alert" className="notice danger">
                    <AlertTriangle size={16} aria-hidden="true" />
                    <span><strong>Terjadi Kesalahan.</strong> {result.error}</span>
                  </p>
                )}

                {hasFinalResult && (
                  <ResultPanel result={result} onClear={clearResult} focusOnResult />
                )}
              </div>
            </div>
          )}
        </main>
      </div>

      <footer className="page-footer">
        <p>Output merupakan rekomendasi, bukan penetapan resmi DJBC. Selalu verifikasi dengan ahli kepabeanan.</p>
      </footer>
    </div>
  )
}

// ─── Clarification Banner (Req 5.5, 6.1, 6.3) ─────────────────────────────────

function ClarificationBanner({ state, onSubmit, isLoading, open, onClose, returnFocusRef, errorResult }) {
  const [answers, setAnswers] = useState({})
  // Tampilan saja: pesan error milik hasil `errorResult` disembunyikan begitu pengguna mengubah jawaban;
  // mengirim ulang mengosongkan hasil di induk, dan error baru adalah objek baru sehingga tampil lagi.
  const [ackedError, setAckedError] = useState(null)
  const shownError = errorResult && ackedError !== errorResult ? errorResult.error : null
  const { questions, message, candidateCodes } = state

  const handleChange = (factType, value) => {
    setAckedError(errorResult)
    setAnswers(prev => ({ ...prev, [factType]: value }))
  }

  const allAnswered = questions.every(q => answers[q.factType]?.trim())

  return (
    <Modal open={open} onClose={onClose} returnFocusRef={returnFocusRef} titleId="clarify-title"
      closeLabel="Tutup (jawaban tetap tersimpan)"
      title={<><AlertTriangle size={18} aria-hidden="true" /> Informasi tambahan diperlukan sebelum kode HS dapat ditentukan.</>}
      footer={(
        <>
          {shownError && (
            <p role="alert" className="notice danger">
              <AlertTriangle size={16} aria-hidden="true" />
              <span><strong>Terjadi Kesalahan.</strong> {shownError}</span>
            </p>
          )}
          <button className="primary-button" onClick={() => onSubmit(answers)} disabled={isLoading || !allAnswered}>
            {isLoading ? 'Memproses...' : 'Kirim Jawaban'}
          </button>
        </>
      )}>
      {message && <p className="notice">{message}</p>}
      {candidateCodes?.length > 0 && (
        <p className="hint">Kandidat heading saat ini: <span className="mono">{candidateCodes.join(', ')}</span></p>
      )}
      <div className="clarify-questions">
        {questions.map((q, i) => {
          const id = `clarify-${i}`
          const impact = q.impact || {}
          return (
            <div key={q.factType} className="field">
              {q.choices?.length
                ? <p aria-hidden="true" className="field-label">{q.questionText}</p>
                : <label htmlFor={id} className="field-label">{q.questionText}</label>}
              {impact.candidatesAffected > 0 && (
                <p className="hint">
                  Mempengaruhi {impact.candidatesAffected} kandidat
                  {impact.retainedCodes?.length > 0 && <> · tetap: <span className="mono">{impact.retainedCodes.join(', ')}</span></>}
                  {impact.eliminatedCodes?.length > 0 && <> · tereliminasi: <span className="mono">{impact.eliminatedCodes.join(', ')}</span></>}
                </p>
              )}
              {q.choices?.length ? (
                // Official BTKI texts are long → radio list instead of a select
                <fieldset className="choice-list" aria-describedby={`${id}-hint`}>
                  <legend className="sr-only">{q.questionText}</legend>
                  <p id={`${id}-hint`} className="hint">Pilihan diambil dari uraian resmi BTKI.</p>
                  {q.choices.map(c => (
                    <label key={c.value} className="choice">
                      <input type="radio" name={id} value={c.value}
                        checked={answers[q.factType] === c.value}
                        onChange={() => handleChange(q.factType, c.value)} />
                      <span>{c.label}</span>
                    </label>
                  ))}
                </fieldset>
              ) : q.options?.length ? (
                <div className="select-wrap">
                  <select id={id} onChange={e => handleChange(q.factType, e.target.value)} defaultValue="">
                    <option value="" disabled>Pilih jawaban...</option>
                    {q.options.map(opt => <option key={opt}>{opt}</option>)}
                  </select>
                </div>
              ) : (
                <input id={id} type="text" placeholder="Ketik jawaban..."
                  onChange={e => handleChange(q.factType, e.target.value)} />
              )}
            </div>
          )
        })}
      </div>
    </Modal>
  )
}

// ─── Login ─────────────────────────────────────────────────────────────────────

function LoginScreen({ onLoggedIn }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState(null)
  const [busy, setBusy] = useState(false)

  const submit = async (e) => {
    e.preventDefault()
    setBusy(true); setError(null)
    try { await login(email, password); onLoggedIn() }
    catch (err) { setError(err.message) }
    finally { setBusy(false) }
  }

  return (
    <main className="login-page">
      <form onSubmit={submit} className="panel login-card">
        <div className="brand login-brand">
          <div className="brand-mark"><span>HS</span></div>
          <div>
            <h1 className="brand-name">HS Intelligence <span className="beta">V2 · DATABASE MODE</span></h1>
            <div className="brand-sub">BTKI 2022 · PMK 26/2022</div>
          </div>
        </div>
        <p className="login-sub">Masuk untuk melanjutkan</p>
        <label className="field">
          <span>Email</span>
          <input id="login-email" type="email" autoComplete="username" required value={email}
            onChange={e => setEmail(e.target.value)} />
        </label>
        <label className="field">
          <span>Password</span>
          <input id="login-password" type="password" autoComplete="current-password" required value={password}
            onChange={e => setPassword(e.target.value)} />
        </label>
        {error && (
          <p role="alert" className="notice danger"><AlertTriangle size={16} aria-hidden="true" /><span>{error}</span></p>
        )}
        <button type="submit" className="primary-button full" disabled={busy}>
          {busy ? 'Memproses...' : 'Masuk'}
        </button>
      </form>
    </main>
  )
}
