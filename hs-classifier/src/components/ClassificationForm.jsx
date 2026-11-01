import { useState } from 'react'
import { RefreshCw, ShieldCheck, Zap } from 'lucide-react'
import dropdownMaster from '../data/dropdown_master.json'
import SelectField from './SelectField'
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

export default function ClassificationForm({ onSubmit, isLoading }) {
  const [form, setForm] = useState(INITIAL_STATE)

  const handleChange = (key, val) => setForm((prev) => ({ ...prev, [key]: val }))

  const handleReset = () => setForm(INITIAL_STATE)

  const handleSubmit = (e) => {
    e.preventDefault()
    if (!form.nama_generik.trim()) {
      alert('Nama generik barang wajib diisi.')
      return
    }
    onSubmit(form)
  }

  const { dropdown_fields, text_fields } = dropdownMaster
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
        <button type="button" onClick={handleReset} disabled={isLoading} className="ghost-button">
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
        </div>
      ))}

      <button type="submit" disabled={isLoading} className="primary-button analyze">
        <Zap size={17} aria-hidden="true" /> {isLoading ? 'Menganalisis...' : 'Analisis HS Code'}
        <span className="button-arrow" aria-hidden="true">→</span>
      </button>
      <p className="form-footnote">
        <ShieldCheck size={13} aria-hidden="true" /> Analisis dijalankan di server internal · hasil dapat memakai saran istilah AI.
      </p>
    </form>
  )
}
