import { Check } from 'lucide-react'

export default function MultiSelectField({ label, fieldKey, options, value = [], onChange }) {
  const toggle = (opt) => {
    const next = value.includes(opt)
      ? value.filter((v) => v !== opt)
      : [...value, opt]
    onChange(fieldKey, next)
  }

  return (
    <div className="field">
      <span>{label}</span>
      <div className="chips" role="group" aria-label={label}>
        {options.map((opt) => (
          <button
            key={opt}
            type="button"
            onClick={() => toggle(opt)}
            aria-pressed={value.includes(opt)}
            className={value.includes(opt) ? 'chip selected' : 'chip'}
          >
            {value.includes(opt) && <Check size={13} aria-hidden="true" />}
            {opt}
          </button>
        ))}
      </div>
      {value.length > 0 && (
        <p className="hint">Dipilih: {value.join(', ')}</p>
      )}
    </div>
  )
}
