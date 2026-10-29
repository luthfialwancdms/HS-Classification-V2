export default function TextField({ label, fieldKey, placeholder, value, onChange, required, multiline }) {
  return (
    <label className="field">
      <span>{label} {required && <em>*</em>}</span>
      {multiline ? (
        <textarea
          rows={3}
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(fieldKey, e.target.value)}
        />
      ) : (
        <input
          type="text"
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(fieldKey, e.target.value)}
        />
      )}
    </label>
  )
}
