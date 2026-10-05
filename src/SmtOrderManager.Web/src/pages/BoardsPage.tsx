import { useEffect, useState } from 'react';
import { api } from '../api';
import { optional } from '../shared/format';
import { ProblemMessage } from '../shared/ProblemMessage';
import { newRow, QuantityRows, toQuantities, type QuantityRow } from '../shared/QuantityRows';
import { SearchBar } from '../shared/SearchBar';
import { useList } from '../shared/useList';
import type { BoardDetails, ComponentDetails } from '../types';

interface Form {
  id: string | null;
  name: string;
  description: string;
  length: string;
  width: string;
  rows: QuantityRow[];
}

const emptyForm = (): Form => ({ id: null, name: '', description: '', length: '', width: '', rows: [] });

function formOf(board: BoardDetails): Form {
  return {
    id: board.id,
    name: board.name,
    description: board.description,
    length: String(board.length),
    width: String(board.width),
    rows: board.billOfMaterials.map((entry) => newRow(entry.componentId, String(entry.quantity))),
  };
}

export function BoardsPage() {
  const list = useList<BoardDetails>(api.boards.search);
  const [components, setComponents] = useState<ComponentDetails[]>([]);
  const [form, setForm] = useState<Form>(emptyForm);

  useEffect(() => {
    void api.components.search('').then((result) => {
      if (result.ok) {
        setComponents(result.value);
      }
    });
  }, []);

  const save = async () => {
    const command = {
      name: form.name,
      description: optional(form.description),
      length: Number(form.length),
      width: Number(form.width),
      billOfMaterials: toQuantities(form.rows).map((row) => ({ componentId: row.id, quantity: row.quantity })),
    };
    const result = form.id
      ? await api.boards.update([{ id: form.id, ...command }])
      : await api.boards.create([command]);

    if (result.ok) {
      setForm(emptyForm());
      list.setProblem(null);
      await list.reload(list.searchText);
    } else {
      list.setProblem(result.problem);
    }
  };

  const removeSelected = async () => {
    if (!confirm(`Remove ${list.selected.size} board(s)?`)) {
      return;
    }
    const result = await api.boards.remove([...list.selected]);
    list.setProblem(result.ok ? null : result.problem);
    if (result.ok) {
      await list.reload(list.searchText);
    }
  };

  return (
    <section>
      <h2>Boards</h2>
      <SearchBar value={list.searchText} onChange={list.setSearchText} onSearch={() => list.reload(list.searchText)} />
      <table>
        <thead>
          <tr>
            <th />
            <th>Name</th>
            <th>Description</th>
            <th>Size (mm)</th>
            <th>Bill of materials</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {list.items.map((board) => (
            <tr key={board.id}>
              <td>
                <input
                  type="checkbox"
                  aria-label={`Select ${board.name}`}
                  checked={list.selected.has(board.id)}
                  onChange={() => list.toggle(board.id)}
                />
              </td>
              <td>{board.name}</td>
              <td>{board.description}</td>
              <td>
                {board.length} x {board.width}
              </td>
              <td>
                {board.billOfMaterials.map((entry) => `${entry.componentName} x ${entry.quantity}`).join(', ')}
              </td>
              <td>
                <button type="button" onClick={() => setForm(formOf(board))}>
                  Edit
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" onClick={removeSelected} disabled={list.selected.size === 0}>
        Remove selected
      </button>

      <ProblemMessage problem={list.problem} />

      <form
        className="editor"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <h3>{form.id ? 'Edit board' : 'New board'}</h3>
        <label>
          Name
          <input value={form.name} onChange={(event) => setForm({ ...form, name: event.target.value })} />
        </label>
        <label>
          Description
          <input
            value={form.description}
            onChange={(event) => setForm({ ...form, description: event.target.value })}
          />
        </label>
        <label>
          Length (mm)
          <input
            type="number"
            step="any"
            value={form.length}
            onChange={(event) => setForm({ ...form, length: event.target.value })}
          />
        </label>
        <label>
          Width (mm)
          <input
            type="number"
            step="any"
            value={form.width}
            onChange={(event) => setForm({ ...form, width: event.target.value })}
          />
        </label>
        <QuantityRows
          label="Component"
          rows={form.rows}
          options={components}
          onChange={(rows) => setForm({ ...form, rows })}
        />
        <div className="actions">
          <button type="submit">{form.id ? 'Save' : 'Create'}</button>
          {form.id && (
            <button type="button" onClick={() => setForm(emptyForm())}>
              Cancel
            </button>
          )}
        </div>
      </form>
    </section>
  );
}
