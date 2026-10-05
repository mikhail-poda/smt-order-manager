import { useCallback, useEffect, useState } from 'react';
import type { Result } from '../api';
import type { Problem } from '../types';

/**
 * State shared by the list pages: the search text, the loaded items, the selected ids for
 * batch removal and the last problem reported by the API.
 */
export function useList<T extends { id: string }>(search: (text: string) => Promise<Result<T[]>>) {
  const [searchText, setSearchText] = useState('');
  const [items, setItems] = useState<T[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [problem, setProblem] = useState<Problem | null>(null);

  const reload = useCallback(
    async (text: string) => {
      const result = await search(text);
      if (result.ok) {
        setItems(result.value);
        setSelected(new Set());
      } else {
        setProblem(result.problem);
      }
    },
    [search],
  );

  useEffect(() => {
    void reload('');
  }, [reload]);

  const toggle = (id: string) =>
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });

  return { searchText, setSearchText, items, selected, toggle, problem, setProblem, reload };
}
