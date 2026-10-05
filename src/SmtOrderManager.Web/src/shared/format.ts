/** A timestamp from the API in the user's locale. */
export function formatDateTime(iso: string): string {
  return new Date(iso).toLocaleString();
}

/** A timestamp from the API as the value of a datetime-local input, in local time. */
export function toLocalInput(iso: string): string {
  const date = new Date(iso);
  const local = new Date(date.getTime() - date.getTimezoneOffset() * 60_000);
  return local.toISOString().slice(0, 16);
}

/**
 * The value of a datetime-local input as an ISO timestamp with the local offset, so the API
 * stores the order date with the offset it was entered in.
 */
export function fromLocalInput(value: string): string {
  const date = new Date(value);
  const offsetMinutes = -date.getTimezoneOffset();
  const sign = offsetMinutes >= 0 ? '+' : '-';
  const hours = String(Math.floor(Math.abs(offsetMinutes) / 60)).padStart(2, '0');
  const minutes = String(Math.abs(offsetMinutes) % 60).padStart(2, '0');
  return `${value}:00${sign}${hours}:${minutes}`;
}

/** Blank text as null, for optional descriptions. */
export function optional(text: string): string | null {
  return text.trim() === '' ? null : text;
}
