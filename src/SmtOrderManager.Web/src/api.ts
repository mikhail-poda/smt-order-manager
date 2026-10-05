import type {
  BoardDetails,
  ComponentDetails,
  CreateBoardCommand,
  CreateComponentCommand,
  CreateOrderCommand,
  CurrentUser,
  OrderDetails,
  OrderDownloadResult,
  Problem,
  RemoveResponse,
  UpdateBoardCommand,
  UpdateComponentCommand,
  UpdateOrderCommand,
} from './types';

/** Either the value of a successful call or the problem the API reported. */
export type Result<T> = { ok: true; value: T } | { ok: false; problem: Problem };

let onUnauthorized: () => void = () => {};

/** Called whenever the API answers 401, for example after the session expired. */
export function setUnauthorizedHandler(handler: () => void): void {
  onUnauthorized = handler;
}

async function request<T>(method: string, path: string, body?: unknown): Promise<Result<T>> {
  let response: Response;

  try {
    response = await fetch(path, {
      method,
      headers: body === undefined ? undefined : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
    });
  } catch {
    return { ok: false, problem: { status: 0, title: 'The server cannot be reached.' } };
  }

  if (response.status === 401 && path !== '/api/auth/login') {
    onUnauthorized();
  }

  if (response.ok) {
    const value = response.status === 204 ? undefined : await response.json();
    return { ok: true, value: value as T };
  }

  return { ok: false, problem: await readProblem(response) };
}

async function readProblem(response: Response): Promise<Problem> {
  try {
    const body = (await response.json()) as Partial<Problem>;
    return {
      status: response.status,
      title: body.title ?? response.statusText,
      detail: body.detail,
      violations: body.violations,
    };
  } catch {
    return { status: response.status, title: response.statusText || 'The request failed.' };
  }
}

function resource<TDetails, TCreate, TUpdate>(path: string) {
  return {
    search: (text: string) =>
      request<TDetails[]>('GET', text.trim() ? `${path}?search=${encodeURIComponent(text.trim())}` : path),
    create: (commands: TCreate[]) => request<TDetails[]>('POST', path, commands),
    update: (commands: TUpdate[]) => request<TDetails[]>('PUT', path, commands),
    remove: (ids: string[]) =>
      request<RemoveResponse>('DELETE', `${path}?${ids.map((id) => `id=${encodeURIComponent(id)}`).join('&')}`),
  };
}

export const api = {
  login: (username: string, password: string) =>
    request<CurrentUser>('POST', '/api/auth/login', { username, password }),
  logout: () => request<void>('POST', '/api/auth/logout'),
  me: () => request<CurrentUser>('GET', '/api/auth/me'),
  components: resource<ComponentDetails, CreateComponentCommand, UpdateComponentCommand>('/api/components'),
  boards: resource<BoardDetails, CreateBoardCommand, UpdateBoardCommand>('/api/boards'),
  orders: {
    ...resource<OrderDetails, CreateOrderCommand, UpdateOrderCommand>('/api/orders'),
    download: (id: string) => request<OrderDownloadResult>('POST', `/api/orders/${encodeURIComponent(id)}/download`),
  },
};
