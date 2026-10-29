export default function TextField({ label, fieldKey, placeholder, value, onChange, required, multiline }) {
  const baseClass =
    'border border-gray-300 rounded-lg px-3 py-2 text-sm bg-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent w-full'

  return (
    <div className="flex flex-col gap-1">
      <label className="text-sm font-medium text-gray-700">
        {label}
        {required && <span className="text-red-500 ml-1">*</span>}
      </label>
      {multiline ? (
        <textarea
          rows={3}
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(fieldKey, e.target.value)}
          className={baseClass}
        />
      ) : (
        <input
          type="text"
          placeholder={placeholder}
          value={value}
          onChange={(e) => onChange(fieldKey, e.target.value)}
          className={baseClass}
        />
      )}
    </div>
  )
}
