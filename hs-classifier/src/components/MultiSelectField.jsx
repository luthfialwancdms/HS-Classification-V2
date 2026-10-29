export default function MultiSelectField({ label, fieldKey, options, value = [], onChange }) {
  const toggle = (opt) => {
    const next = value.includes(opt)
      ? value.filter((v) => v !== opt)
      : [...value, opt]
    onChange(fieldKey, next)
  }

  return (
    <div className="flex flex-col gap-1">
      <label className="text-sm font-medium text-gray-700">{label}</label>
      <div className="border border-gray-300 rounded-lg p-3 bg-white flex flex-wrap gap-2 max-h-40 overflow-y-auto">
        {options.map((opt) => (
          <button
            key={opt}
            type="button"
            onClick={() => toggle(opt)}
            className={`px-3 py-1 rounded-full text-xs font-medium transition-colors cursor-pointer ${
              value.includes(opt)
                ? 'bg-blue-600 text-white'
                : 'bg-gray-100 text-gray-700 hover:bg-gray-200'
            }`}
          >
            {opt}
          </button>
        ))}
      </div>
      {value.length > 0 && (
        <p className="text-xs text-blue-600">Dipilih: {value.join(', ')}</p>
      )}
    </div>
  )
}
