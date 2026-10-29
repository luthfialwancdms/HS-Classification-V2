import { useEffect, useState } from 'react'
import ClassificationForm from './components/ClassificationForm'
import ResultPanel from './components/ResultPanel'
import AdminDashboard from './components/AdminDashboard'
import HistoryPage from './components/HistoryPage'
import ReviewPage from './components/ReviewPage'
import { AUTH_EVENT, classifyWithBackend, clearToken, getRole, getToken, login, submitAnswers } from './services/apiClient'

// view → [path, label, roles allowed (null = everyone)]
const VIEWS = {
  classify: ['/',        'Klasifikasi',       null],
  history:  ['/riwayat', 'Riwayat',           null],
  review:   ['/review',  'Review Ahli',       ['Admin', 'ExpertReviewer']],
  admin:    ['/admin',   'Admin Data Center', ['Admin']],
}
const viewFromPath = () =>
  Object.entries(VIEWS).find(([k, [p]]) => k !== 'classify' && location.pathname.startsWith(p))?.[0] ?? 'classify'

export default function App() {
  const [view,      setView]      = useState(viewFromPath)
  const [result,    setResult]    = useState(null)
  const [isLoading, setIsLoading] = useState(false)
  const [clarifyState, setClarifyState] = useState(null) // { facts, questions, message, candidateCodes }

  const [authed, setAuthed] = useState(() => !!getToken())
  const role = authed ? getRole() : null
  const allowed = (v) => !VIEWS[v][2] || VIEWS[v][2].includes(role)
  const isAdmin = role === 'Admin'

  useEffect(() => {
    const onPop = () => setView(viewFromPath())
    const onAuth = () => setAuthed(false)
    addEventListener('popstate', onPop)
    addEventListener(AUTH_EVENT, onAuth)
    return () => { removeEventListener('popstate', onPop); removeEventListener(AUTH_EVENT, onAuth) }
  }, [])

  const logout = () => { clearToken(); setAuthed(false); setResult(null); setClarifyState(null) }

  const switchView = (v) => {
    history.pushState(null, '', VIEWS[v][0])
    setView(v)
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
    }
  }

  const handleSubmit  = (form)    => { setClarifyState(null); handle(() => classifyWithBackend(form)) }
  const handleAnswers = (answers) => handle(() => submitAnswers(clarifyState.caseId, answers))

  if (!authed) return <LoginScreen onLoggedIn={() => setAuthed(true)} />

  return (
    <div className="min-h-screen bg-gray-50">
      <header className="bg-white border-b border-gray-200 shadow-sm sticky top-0 z-10">
        <div className="max-w-7xl mx-auto px-6 py-4 flex items-center justify-between">
          <div className="flex items-center gap-3">
            <div className="w-9 h-9 bg-blue-600 rounded-lg flex items-center justify-center text-white font-bold text-sm">
              HS
            </div>
            <div>
              <h1 className="text-base font-bold text-gray-900 leading-tight">
                AI HS Classification Intelligence
              </h1>
              <p className="text-xs text-gray-500">SMART · BTKI 2022 · PMK 26/2022</p>
            </div>
          </div>
          <nav className="flex items-center gap-2" aria-label="Navigasi utama">
            <span className="text-xs bg-green-100 text-green-700 px-2 py-1 rounded-full font-medium">V2 · Database Mode</span>
            {Object.keys(VIEWS).filter(allowed).map(v => (
              <button
                key={v}
                onClick={() => switchView(v)}
                aria-current={view === v ? 'page' : undefined}
                className={`text-sm px-3 py-1.5 rounded-lg font-medium ${view === v ? 'bg-blue-600 text-white' : 'text-gray-600 hover:bg-gray-100'}`}
              >
                {VIEWS[v][1]}
              </button>
            ))}
            <button onClick={logout} className="text-sm px-3 py-1.5 rounded-lg text-gray-500 hover:bg-gray-100">Keluar</button>
          </nav>
        </div>
      </header>

      {view === 'admin' && isAdmin ? <AdminDashboard />
        : view === 'history' ? <HistoryPage showUser={['Admin', 'ExpertReviewer'].includes(role)} />
        : view === 'review' && allowed('review') ? <ReviewPage />
        : (
        <>
          {clarifyState && (
            <div className="max-w-7xl mx-auto px-6 pt-6">
              <ClarificationBanner
                key={clarifyState.questions.map(q => q.questionText).join('|')}
                state={clarifyState}
                onSubmit={handleAnswers}
                isLoading={isLoading}
              />
            </div>
          )}

          <main className="max-w-7xl mx-auto px-6 py-8">
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 items-start">
              <div>
                <div className="mb-4">
                  <h2 className="text-lg font-semibold text-gray-900">Data Barang</h2>
                  <p className="text-sm text-gray-500">Isi informasi barang selengkap mungkin untuk hasil yang akurat</p>
                </div>
                <ClassificationForm onSubmit={handleSubmit} isLoading={isLoading} />
              </div>
              <div className="lg:sticky lg:top-24">
                <div className="mb-4">
                  <h2 className="text-lg font-semibold text-gray-900">Hasil Analisis</h2>
                  <p className="text-sm text-gray-500">Rekomendasi kode HS + reasoning KUM HS</p>
                </div>
                <ResultPanel result={result} isLoading={isLoading} />
              </div>
            </div>
          </main>
        </>
      )}

      <footer className="border-t border-gray-200 mt-12 py-4 text-center">
        <p className="text-xs text-gray-400">
          Output merupakan rekomendasi, bukan penetapan resmi DJBC. Selalu verifikasi dengan ahli kepabeanan.
        </p>
      </footer>
    </div>
  )
}

// ─── Clarification Banner (Req 5.5, 6.1, 6.3) ─────────────────────────────────

function ClarificationBanner({ state, onSubmit, isLoading }) {
  const [answers, setAnswers] = useState({})
  const { questions, message, candidateCodes } = state

  const handleChange = (factType, value) =>
    setAnswers(prev => ({ ...prev, [factType]: value }))

  const allAnswered = questions.every(q => answers[q.factType]?.trim())

  return (
    <section aria-labelledby="clarify-title" className="bg-amber-50 border border-amber-300 rounded-xl p-4 mb-2">
      <h3 id="clarify-title" className="text-sm font-semibold text-amber-800 mb-1">
        ⚠️ Informasi tambahan diperlukan sebelum kode HS dapat ditentukan
      </h3>
      {message && <p className="text-xs text-amber-700 mb-2">{message}</p>}
      {candidateCodes?.length > 0 && (
        <p className="text-xs text-amber-700 mb-3">
          Kandidat heading saat ini: <span className="font-mono">{candidateCodes.join(', ')}</span>
        </p>
      )}
      <div className="space-y-3">
        {questions.map((q, i) => {
          const id = `clarify-${i}`
          const impact = q.impact || {}
          return (
            <div key={q.factType}>
              {q.choices?.length
                ? <p aria-hidden="true" className="text-sm text-amber-900 font-medium mb-1">{q.questionText}</p>
                : <label htmlFor={id} className="text-sm text-amber-900 font-medium block mb-1">{q.questionText}</label>}
              {impact.candidatesAffected > 0 && (
                <p className="text-xs text-amber-600 mb-1">
                  Mempengaruhi {impact.candidatesAffected} kandidat
                  {impact.retainedCodes?.length > 0 && <> · tetap: <span className="font-mono">{impact.retainedCodes.join(', ')}</span></>}
                  {impact.eliminatedCodes?.length > 0 && <> · tereliminasi: <span className="font-mono">{impact.eliminatedCodes.join(', ')}</span></>}
                </p>
              )}
              {q.choices?.length ? (
                // Official BTKI texts are long → radio list instead of a select
                <fieldset aria-describedby={`${id}-hint`} className="flex flex-col gap-1.5">
                  <legend className="sr-only">{q.questionText}</legend>
                  <p id={`${id}-hint`} className="text-xs text-amber-700">Pilihan diambil dari uraian resmi BTKI.</p>
                  {q.choices.map(c => (
                    <label key={c.value} className="flex items-start gap-2 text-sm text-amber-900 bg-white border border-amber-200 rounded p-2 cursor-pointer has-[:checked]:border-amber-500 has-[:checked]:bg-amber-100">
                      <input type="radio" name={id} value={c.value} className="mt-1"
                        checked={answers[q.factType] === c.value}
                        onChange={() => handleChange(q.factType, c.value)} />
                      <span>{c.label}</span>
                    </label>
                  ))}
                </fieldset>
              ) : q.options?.length ? (
                <select
                  id={id}
                  className="w-full text-sm border border-amber-300 rounded p-2 bg-white"
                  onChange={e => handleChange(q.factType, e.target.value)}
                  defaultValue=""
                >
                  <option value="" disabled>Pilih jawaban...</option>
                  {q.options.map(opt => <option key={opt}>{opt}</option>)}
                </select>
              ) : (
                <input
                  id={id}
                  type="text"
                  placeholder="Ketik jawaban..."
                  className="w-full text-sm border border-amber-300 rounded p-2 bg-white"
                  onChange={e => handleChange(q.factType, e.target.value)}
                />
              )}
            </div>
          )
        })}
      </div>
      <button
        onClick={() => onSubmit(answers)}
        disabled={isLoading || !allAnswered}
        className="mt-3 px-4 py-2 bg-amber-600 text-white text-sm rounded font-medium hover:bg-amber-700 disabled:opacity-50"
      >
        {isLoading ? 'Memproses...' : 'Kirim Jawaban'}
      </button>
    </section>
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
    <main className="min-h-screen bg-gray-50 flex items-center justify-center px-6">
      <form onSubmit={submit} className="bg-white rounded-2xl border border-gray-200 shadow-sm p-8 w-full max-w-sm flex flex-col gap-4">
        <div>
          <h1 className="text-lg font-bold text-gray-900">AI HS Classification Intelligence</h1>
          <p className="text-sm text-gray-500">Masuk untuk melanjutkan</p>
        </div>
        <div>
          <label htmlFor="login-email" className="block text-sm text-gray-700 mb-1">Email</label>
          <input id="login-email" type="email" autoComplete="username" required value={email}
            onChange={e => setEmail(e.target.value)} className="w-full text-sm border border-gray-300 rounded p-2" />
        </div>
        <div>
          <label htmlFor="login-password" className="block text-sm text-gray-700 mb-1">Password</label>
          <input id="login-password" type="password" autoComplete="current-password" required value={password}
            onChange={e => setPassword(e.target.value)} className="w-full text-sm border border-gray-300 rounded p-2" />
        </div>
        {error && <p role="alert" className="text-sm text-red-600">{error}</p>}
        <button type="submit" disabled={busy}
          className="px-4 py-2 bg-blue-600 text-white text-sm rounded-lg font-medium hover:bg-blue-700 disabled:opacity-50">
          {busy ? 'Memproses...' : 'Masuk'}
        </button>
      </form>
    </main>
  )
}
