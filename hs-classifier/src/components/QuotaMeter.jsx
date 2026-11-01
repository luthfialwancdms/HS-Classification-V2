/**
 * Pemakaian kuota klasifikasi harian pengguna yang sedang login ("3/10").
 * Angka datang dari GET /api/classification/quota. `limit` 0 = tanpa batas (Admin, Ahli): tidak ditampilkan.
 */
export default function QuotaMeter({ quota }) {
  if (!quota || !quota.limit) return null

  const { used, limit, resetAt } = quota
  const left = Math.max(limit - used, 0)
  const tone = left === 0 ? 'danger' : left <= 2 ? 'warning' : ''
  const percent = Math.min(100, Math.round((used / limit) * 100))
  const reset = new Date(resetAt).toLocaleString('id-ID', { dateStyle: 'medium', timeStyle: 'short' })

  return (
    <div className={`quota-meter ${tone}`.trim()} role="status" aria-live="polite"
      aria-label={`Pemakaian hari ini ${used} dari ${limit} klasifikasi`}>
      <span className="quota-label">Pemakaian hari ini</span>
      <strong className="quota-count">{used}/{limit}</strong>
      <div className="quota-bar" aria-hidden="true"><span style={{ width: `${percent}%` }} /></div>
      <small className="hint">{left === 0 ? 'Kuota habis.' : `Sisa ${left}.`} Diperbarui {reset}.</small>
    </div>
  )
}
