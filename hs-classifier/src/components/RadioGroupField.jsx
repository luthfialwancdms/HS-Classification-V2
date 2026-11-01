// Pilihan singkat sebagai radio. Opsi, nama field, dan nilai yang dikirim
// sama persis dengan versi <select> (SelectField): nilai = teks opsi dari data.
export default function RadioGroupField({ label, fieldKey, options, value, onChange }) {
  return (
    <fieldset className="choice-list radio-field">
      <legend className="radio-legend">{label}</legend>
      {options.map((opt) => (
        <label key={opt} className="choice">
          <input
            type="radio"
            name={fieldKey}
            value={opt}
            checked={value === opt}
            onChange={() => onChange(fieldKey, opt)}
          />
          {opt}
        </label>
      ))}
    </fieldset>
  )
}
