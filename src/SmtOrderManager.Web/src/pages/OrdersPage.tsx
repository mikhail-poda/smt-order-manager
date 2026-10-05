import { useEffect, useState } from 'react';
import { api } from '../api';
import { formatDateTime, fromLocalInput, optional, toLocalInput } from '../shared/format';
import { ProblemMessage } from '../shared/ProblemMessage';
import { newRow, QuantityRows, toQuantities, type QuantityRow } from '../shared/QuantityRows';
import { SearchBar } from '../shared/SearchBar';
import { useList } from '../shared/useList';
import type { BoardDetails, OrderDetails, OrderDownloadResult } from '../types';

interface Form {
  id: string | null;
  name: string;
  description: string;
  orderDate: string;
  rows: QuantityRow[];
}

const emptyForm = (): Form => ({
  id: null,
  name: '',
  description: '',
  orderDate: toLocalInput(new Date().toISOString()),
  rows: [],
});

function formOf(order: OrderDetails): Form {
  return {
    id: order.id,
    name: order.name,
    description: order.description,
    orderDate: toLocalInput(order.orderDate),
    rows: order.lines.map((line) => newRow(line.boardId, String(line.quantity))),
  };
}

export function OrdersPage() {
  const list = useList<OrderDetails>(api.orders.search);
  const [boards, setBoards] = useState<BoardDetails[]>([]);
  const [form, setForm] = useState<Form>(emptyForm);
  const [download, setDownload] = useState<OrderDownloadResult | null>(null);

  useEffect(() => {
    void api.boards.search('').then((result) => {
      if (result.ok) {
        setBoards(result.value);
      }
    });
  }, []);

  const save = async () => {
    // Editing a downloaded order is not blocked here: the API answers with its own violation.
    const command = {
      name: form.name,
      description: optional(form.description),
      orderDate: fromLocalInput(form.orderDate),
      lines: toQuantities(form.rows).map((row) => ({ boardId: row.id, quantity: row.quantity })),
    };
    const result = form.id
      ? await api.orders.update([{ id: form.id, ...command }])
      : await api.orders.create([command]);

    if (result.ok) {
      setForm(emptyForm());
      list.setProblem(null);
      await list.reload(list.searchText);
    } else {
      list.setProblem(result.problem);
    }
  };

  const removeSelected = async () => {
    if (!confirm(`Remove ${list.selected.size} order(s)?`)) {
      return;
    }
    const result = await api.orders.remove([...list.selected]);
    list.setProblem(result.ok ? null : result.problem);
    if (result.ok) {
      await list.reload(list.searchText);
    }
  };

  const downloadOrder = async (id: string) => {
    setDownload(null);
    const result = await api.orders.download(id);
    if (result.ok) {
      list.setProblem(null);
      setDownload(result.value);
      await list.reload(list.searchText);
    } else {
      list.setProblem(result.problem);
    }
  };

  return (
    <section>
      <h2>Orders</h2>
      <SearchBar value={list.searchText} onChange={list.setSearchText} onSearch={() => list.reload(list.searchText)} />
      <table>
        <thead>
          <tr>
            <th />
            <th>Name</th>
            <th>Order date</th>
            <th>Status</th>
            <th>Boards</th>
            <th />
          </tr>
        </thead>
        <tbody>
          {list.items.map((order) => (
            <tr key={order.id}>
              <td>
                <input
                  type="checkbox"
                  aria-label={`Select ${order.name}`}
                  checked={list.selected.has(order.id)}
                  onChange={() => list.toggle(order.id)}
                />
              </td>
              <td>
                {order.name}
                {order.description && <div className="muted">{order.description}</div>}
              </td>
              <td>{formatDateTime(order.orderDate)}</td>
              <td>
                <span className={`status ${order.status}`}>{order.status}</span>
                {order.downloadedAt && <div className="muted">{formatDateTime(order.downloadedAt)}</div>}
              </td>
              <td>{order.lines.map((line) => `${line.boardName} x ${line.quantity}`).join(', ')}</td>
              <td className="buttons">
                <button type="button" onClick={() => setForm(formOf(order))}>
                  Edit
                </button>
                <button type="button" onClick={() => void downloadOrder(order.id)}>
                  Download
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      <button type="button" onClick={removeSelected} disabled={list.selected.size === 0}>
        Remove selected
      </button>

      {download && <DownloadAnswer result={download} />}
      <ProblemMessage problem={list.problem} />

      <form
        className="editor"
        onSubmit={(event) => {
          event.preventDefault();
          void save();
        }}
      >
        <h3>{form.id ? 'Edit order' : 'New order'}</h3>
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
          Order date
          <input
            type="datetime-local"
            value={form.orderDate}
            onChange={(event) => setForm({ ...form, orderDate: event.target.value })}
          />
        </label>
        <QuantityRows label="Board" rows={form.rows} options={boards} onChange={(rows) => setForm({ ...form, rows })} />
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

/** The line's answer to a download: accepted with its job reference, or every rejection reason. */
function DownloadAnswer({ result }: { result: OrderDownloadResult }) {
  return (
    <div className={result.accepted ? 'answer accepted' : 'answer rejected'} role="status">
      <strong>
        {result.accepted ? 'Accepted' : 'Rejected'} by {result.lineId}: {result.orderName}
      </strong>
      <div className="muted">Received {formatDateTime(result.receivedAt)}</div>
      {result.accepted && result.jobReference && <p>Job reference: {result.jobReference}</p>}
      {result.reasons.length > 0 && (
        <ul>
          {result.reasons.map((reason, index) => (
            <li key={index}>{reason}</li>
          ))}
        </ul>
      )}
    </div>
  );
}
