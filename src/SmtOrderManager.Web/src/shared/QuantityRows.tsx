/** One editable row: a referenced item and a quantity, kept as text while editing. */
export interface QuantityRow {
  key: number;
  refId: string;
  quantity: string;
}

interface QuantityRowsProps {
  label: string;
  rows: QuantityRow[];
  options: { id: string; name: string }[];
  onChange: (rows: QuantityRow[]) => void;
}

let nextKey = 1;

export function newRow(refId: string, quantity: string): QuantityRow {
  return { key: nextKey++, refId, quantity };
}

/**
 * Edits a bill of materials or the lines of an order as simple rows: a dropdown, a quantity,
 * and buttons to add and remove rows. Checking the rows is left to the API.
 */
export function QuantityRows({ label, rows, options, onChange }: QuantityRowsProps) {
  const update = (key: number, change: Partial<QuantityRow>) =>
    onChange(rows.map((row) => (row.key === key ? { ...row, ...change } : row)));

  const addRow = () => {
    const unused = options.find((option) => !rows.some((row) => row.refId === option.id));
    onChange([...rows, newRow(unused?.id ?? options[0]?.id ?? '', '1')]);
  };

  return (
    <fieldset className="rows">
      <legend>{label}s</legend>
      {rows.map((row) => (
        <div className="row" key={row.key}>
          <select value={row.refId} onChange={(event) => update(row.key, { refId: event.target.value })}>
            {options.map((option) => (
              <option key={option.id} value={option.id}>
                {option.name}
              </option>
            ))}
          </select>
          <input
            type="number"
            min="1"
            aria-label="Quantity"
            value={row.quantity}
            onChange={(event) => update(row.key, { quantity: event.target.value })}
          />
          <button type="button" onClick={() => onChange(rows.filter((other) => other.key !== row.key))}>
            Remove
          </button>
        </div>
      ))}
      <button type="button" onClick={addRow} disabled={options.length === 0}>
        Add {label.toLowerCase()}
      </button>
    </fieldset>
  );
}

/** Converts the rows to the shape of the API commands. */
export function toQuantities(rows: QuantityRow[]): { id: string; quantity: number }[] {
  return rows.map((row) => ({ id: row.refId, quantity: Number(row.quantity) }));
}
