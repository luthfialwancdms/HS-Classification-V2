import { useState } from 'react'
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
    icon: '📦',
    fields: ['nama_dagang', 'nama_generik'],
  },
  {
    title: '2. Fisik & Material',
    icon: '🔩',
    fields: ['material_utama', 'kondisi_impor'],
  },
  {
    title: '3. Fungsi & Cara Kerja',
    icon: '⚙️',
    fields: ['fungsi_utama', 'cara_kerja', 'mobilitas'],
  },
  {
    title: '4. Konteks Penggunaan',
    icon: '🏭',
    fields: ['area_operasi', 'sektor'],
  },
  {
    title: '5. Karakteristik Tambahan',
    icon: '📋',
    fields: ['apakah_part', 'apakah_set', 'ada_komponen_tambahan', 'spesifikasi', 'keterangan_tambahan'],
  },
]

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
    <form onSubmit={handleSubmit} className="flex flex-col gap-6">
      {SECTIONS.map((section) => (
        <div key={section.title} className="bg-white rounded-2xl border border-gray-200 shadow-sm p-5">
          <h2 className="text-sm font-semibold text-gray-800 mb-4 flex items-center gap-2">
            <span>{section.icon}</span> {section.title}
          </h2>
          <div className="flex flex-col gap-4">
            {section.fields.map(renderField)}
          </div>
        </div>
      ))}

      <div className="flex gap-3">
        <button
          type="submit"
          disabled={isLoading}
          className="flex-1 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-300 text-white font-semibold py-3 px-6 rounded-xl transition-colors text-sm cursor-pointer"
        >
          {isLoading ? 'Menganalisis...' : '🔍 Analisis HS Code'}
        </button>
        <button
          type="button"
          onClick={handleReset}
          disabled={isLoading}
          className="bg-gray-100 hover:bg-gray-200 text-gray-700 font-medium py-3 px-4 rounded-xl transition-colors text-sm cursor-pointer"
        >
          Reset
        </button>
      </div>
    </form>
  )
}
