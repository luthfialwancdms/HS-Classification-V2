import { ChevronDown } from 'lucide-react'

export default function SelectField({ label, fieldKey, options, value, onChange, required }) {
  return (
    <label className="field">
      <span>{label} {required && <em>*</em>}</span>
      <div className="select-wrap">
        <select value={value} onChange={(e) => onChange(fieldKey, e.target.value)}>
          <option value="">-- Pilih --</option>
          {options.map((opt) => (
            <option key={opt} value={opt}>{opt}</option>
          ))}
        </select>
        <ChevronDown size={15} aria-hidden="true" />
      </div>
    </label>
  )
}
