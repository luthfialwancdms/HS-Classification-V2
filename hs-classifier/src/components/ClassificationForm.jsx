import { useState } from 'react'
import { AlertTriangle, RefreshCw, ShieldCheck, Zap } from 'lucide-react'
import dropdownMaster from '../data/dropdown_master.json'
import SelectField from './SelectField'
import RadioGroupField from './RadioGroupField'
import MultiSelectField from './MultiSelectField'
import TextField from './TextField'

const INITIAL_STATE = {
  nama_dagang: '',
  nama_generik: '',
  spesifikasi: '',
  keterangan_tambahan: '',
  material_utama: [],
  kondisi_impor: '',
  fungsi_utama: '',
  cara_kerja: [],
  mobilitas: '',
  area_operasi: '',
  sektor: '',
  apakah_part: '',
  apakah_set: '',
  ada_komponen_tambahan: '',
}

const SECTIONS = [
  {
    title: '1. Identitas Barang',
    fields: ['nama_dagang', 'nama_generik'],
  },
  {
    title: '2. Fisik & Material',
    fields: ['material_utama', 'kondisi_impor'],
  },
  {
    title: '3. Fungsi & Cara Kerja',
    fields: ['fungsi_utama', 'cara_kerja', 'mobilitas'],
  },
  {
    title: '4. Konteks Penggunaan',
    fields: ['area_operasi', 'sektor'],
  },
  {
    title: '5. Karakteristik Tambahan',
    fields: ['apakah_part', 'apakah_set', 'ada_komponen_tambahan', 'spesifikasi', 'keterangan_tambahan'],
  },
]

// Field yang butuh lebar penuh di grid 2 kolom (chip & textarea panjang).
const WIDE = new Set(['material_utama', 'cara_kerja', 'spesifikasi', 'keterangan_tambahan'])

// Tampilan saja: tiga pertanyaan pilihan singkat ditampilkan sebagai radio (opsi tetap dari dropdown_master.json).
const RADIO_FIELDS = new Set(['apakah_part', 'apakah_set', 'ada_komponen_tambahan'])

export default function ClassificationForm({ onSubmit, isLoading, onReset, submitRef, resumeAction }) {
  const [form, setForm] = useState(INITIAL_STATE)

  const handleChange = (key, val) => setForm((prev) => ({ ...prev, [key]: val }))

  // REV2 butir 1: Reset juga memberi tahu induk agar hasil dan klarifikasi dikosongkan.
  const handleReset = () => { setForm(INITIAL_STATE); onReset?.() }

  const handleSubmit = (e) => {
    e.preventDefault()
    if (!form.nama_generik.trim()) {
      alert('Nama generik barang wajib diisi.')
      return
    }
    onSubmit(form)
  }

  const { dropdown_fields, text_fields } = dropdownMaster

  // Fungsi yang biasanya bergantung pada ada atau tidaknya motor; Cara kerja yang kosong membuat mesin harus menebak.
  const needsCaraKerja = /^(Mengangkut|Mengangkat|Menggali|Menggerakkan|Pertanian)/.test(form.fungsi_utama) && form.cara_kerja.length === 0
  const textFieldMap = Object.fromEntries(text_fields.map((f) => [f.key, f]))

  const renderField = (key) => {
    // Text fields
    if (textFieldMap[key]) {
      const tf = textFieldMap[key]
      return (
        <TextField
          key={key}
          label={tf.label}
          fieldKey={key}
          placeholder={tf.placeholder}
          value={form[key]}
          onChange={handleChange}
          required={tf.required}
          multiline={key === 'spesifikasi' || key === 'keterangan_tambahan'}
        />
      )
    }
    // Dropdown fields
    if (dropdown_fields[key]) {
      const df = dropdown_fields[key]
      if (df.multi) {
        return (
          <MultiSelectField
            key={key}
            label={df.label}
            fieldKey={key}
            options={df.options}
            value={form[key]}
            onChange={handleChange}
          />
        )
      }
      if (RADIO_FIELDS.has(key)) {
        return (
          <RadioGroupField
            key={key}
            label={df.label}
            fieldKey={key}
            options={df.options}
            value={form[key]}
            onChange={handleChange}
          />
        )
      }
      return (
        <SelectField
          key={key}
          label={df.label}
          fieldKey={key}
          options={df.options}
          value={form[key]}
          onChange={handleChange}
        />
      )
    }
    return null
  }

  return (
    <form onSubmit={handleSubmit} className="panel form-panel">
      <div className="panel-heading">
        <div>
          <div className="eyebrow">INPUT DATA</div>
          <h2>Data Barang</h2>
          <p>Lengkapi karakteristik barang untuk analisis akurat.</p>
        </div>
        <button type="button" onClick={handleReset} disabled={isLoading} className="reset-button">
          <RefreshCw size={15} aria-hidden="true" /> Reset
        </button>
      </div>

      {SECTIONS.map((section) => (
        <div key={section.title} className="form-section">
          <h3 className="section-title">{section.title}</h3>
          <div className="form-grid">
            {section.fields.map((key) => (
              <div key={key} className={WIDE.has(key) ? 'span-2' : undefined}>
                {renderField(key)}
              </div>
            ))}
          </div>
          {section.fields.includes('cara_kerja') && needsCaraKerja && (
            <p role="status" className="notice" id="cara-kerja-hint">
              <AlertTriangle size={16} aria-hidden="true" />
              <span>
                Cara kerja belum diisi. Untuk barang transportasi atau mesin, pilih apakah barang digerakkan tenaga manual,
                motor listrik, atau mesin bensin/diesel. Tanpa itu sistem bisa salah memilih antara pos bermotor dan tidak bermotor
                (misalnya sepeda biasa dan sepeda motor).
              </span>
            </p>
          )}
        </div>
      ))}

      <button type="submit" ref={submitRef} disabled={isLoading} className="primary-button analyze">
        <Zap size={17} aria-hidden="true" /> {isLoading ? 'Menganalisis...' : 'Analisis HS Code'}
        <span className="button-arrow" aria-hidden="true">→</span>
      </button>
      {resumeAction}
      <p className="form-footnote">
        <ShieldCheck size={13} aria-hidden="true" /> Analisis dijalankan di server internal · hasil dapat memakai saran istilah AI.
      </p>
    </form>
  )
}
