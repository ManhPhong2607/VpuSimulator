export function timeAgo(iso: string | null, now = Date.now()): string {
  if (!iso) return "—";
  const s = Math.max(0, Math.round((now - new Date(iso).getTime()) / 1000));
  if (s < 60) return `${s}s trước`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m} phút trước`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h} giờ trước`;
  return `${Math.floor(h / 24)} ngày trước`;
}

export function shortUuid(id: string): string {
  return id.length > 13 ? `${id.slice(0, 8)}…` : id;
}

/** "3b24c9a1-...-0011" -> "3b24…0011" */
export function shortId(id: string): string {
  return id.length > 10 ? `${id.slice(0, 4)}…${id.slice(-4)}` : id;
}

/** ISO -> "12:04:31.902" (giờ máy) */
export function formatClock(iso: string): string {
  const d = new Date(iso);
  const p = (n: number, l = 2) => String(n).padStart(l, "0");
  return `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}.${p(d.getMilliseconds(), 3)}`;
}

/** ISO -> "12:01" (giờ máy) */
export function formatHm(iso: string): string {
  const d = new Date(iso);
  return `${String(d.getHours()).padStart(2, "0")}:${String(d.getMinutes()).padStart(2, "0")}`;
}

export function timeAgoEn(iso: string, now = Date.now()): string {
  const s = Math.max(0, Math.round((now - new Date(iso).getTime()) / 1000));
  if (s < 60) return `${s}s ago`;
  const m = Math.floor(s / 60);
  if (m < 60) return `${m}m ago`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h ago`;
  return `${Math.floor(h / 24)}d ago`;
}