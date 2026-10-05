import { useState } from 'react';
import { api } from '../api';
import { optional } from '../shared/format';
import { ProblemMessage } from '../shared/ProblemMessage';
import { SearchBar } from '../shared/SearchBar';
import { useList } from '../shared/useList';
import type { ComponentDetails } from '../types';

interface Form {
  id: string | null;
  name: string;
  description: string;
}

const emptyForm: Form = { id: null, name: '', description: '' };

export function ComponentsPage() {
  const list = useList<ComponentDetails>(api.components.search);
  const [form, setForm] = useState<Form>(emptyForm);

  const save = async () => {
    const command = { name: form.name, description: optional(form.description) };
    const result = form.id
      ? await api.components.update([{ id: form.id, ...command }])
      : await api.components.create([command]);

    if (result.ok) {
      setForm(emptyForm);
      list.setProblem(null);
      await list.reload(list.searchText);
    } else {
      list.setProblem(result.problem);
    }
  };

  const removeSelected = async () => {
    if (!confirm(`Remove ${list.selected.size} component(s)?`)) {
      return;
    }
    const result = await api.components.remove([...list.selected]);
    list.setProblem(result.ok ? null : result.problem);
    if (result.ok) {
      await list.reload(list.searchText);
    }
  };

  return (
    <section>
      <h2>Components</h2>
      <SearchBar value={list.searchText} onChange={list.setSearchText} onSearch={() => list.reload(list.searchText)} />
      <table>
        <thead>
          <tr>
            <th />
            <th>Name</th>
            <th>Description</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {list.items.map((component) => (
            <tr key={component.id}>
              <td>
                <input
                  type="checkbox"
                  aria-label={`Select ${component.name}`}
                  checked={list.selected.has(component.id)}
                  onChange={() => list.toggle(component.id)}
                />
              </td>
              <td>{component.name}</td>
              <td>{component.description}</td>
              <td>
                <button
                  type="button"
                  onClick={() =>
                    setForm({ id: component.id, name: component.name, description: component.description })
                  }
                >
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
        <h3>{form.id ? 'Edit component' : 'New component'}</h3>
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
        <div className="actions">
          <button type="submit">{form.id ? 'Save' : 'Create'}</button>
          {form.id && (
            <button type="button" onClick={() => setForm(emptyForm)}>
              Cancel
            </button>
          )}
        </div>
      </form>
    </section>
  );
}
