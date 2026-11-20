import { ApiError } from "./control-api";

const BASE = "/api/control";

// Các khoá có nội dung do VMS/bản tin quyết định: giữ nguyên, không đổi tên key bên trong
const RAW_RESPONSE_KEYS = new Set(["rule_conf", "payload"]);
const RAW_REQUEST_KEYS = new Set(["ruleConf"]);

const toCamel = (s: string) => s.replace(/_([a-z0-9])/g, (_, c: string) => c.toUpperCase());
const toSnake = (s: string) => s.replace(/[A-Z]/g, (c) => `_${c.toLowerCase()}`);

function convert(value: unknown, rename: (k: string) => string, raw: Set<string>): unknown {
  if (Array.isArray(value)) return value.map((v) => convert(v, rename, raw));
  if (value && typeof value === "object") {
    return Object.fromEntries(
      Object.entries(value as Record<string, unknown>).map(([k, v]) => [
        rename(k),
        raw.has(k) ? v : convert(v, rename, raw),
      ]),
    );
  }
  return value;
}

export const camelize = <T>(value: unknown): T =>
  convert(value, toCamel, RAW_RESPONSE_KEYS) as T;

type Query = Record<string, string | number | boolean | undefined>;

interface Opts {
  query?: Query;
  body?: unknown;
  /** true: gửi body nguyên trạng, không đổi camelCase sang snake_case (ví dụ cả rule_conf). */
  rawBody?: boolean;
  signal?: AbortSignal;
}

async function request<T>(method: string, path: string, opts: Opts = {}): Promise<T> {
  const qs = new URLSearchParams();
  for (const [k, v] of Object.entries(opts.query ?? {})) {
    if (v !== undefined) qs.set(k, String(v));
  }
  const q = qs.toString();
  const url = `${BASE}/${path.replace(/^\//, "")}${q ? `?${q}` : ""}`;
  const hasBody = opts.body !== undefined;

  let res: Response;
  try {
    res = await fetch(url, {
      method,
      headers: hasBody ? { "content-type": "application/json" } : undefined,
      body: hasBody
        ? JSON.stringify(opts.rawBody ? opts.body : convert(opts.body, toSnake, RAW_REQUEST_KEYS))
        : undefined,
      cache: "no-store",
      signal: opts.signal,
    });
  } catch {
    throw new ApiError(0, "Không kết nối được tới máy chủ console.");
  }

  const text = await res.text();
  let data: unknown;
  if (text) {
    try {
      data = JSON.parse(text);
    } catch {
      data = undefined; // phản hồi không phải JSON (ví dụ /healthz trả chữ thường)
    }
  }

  if (!res.ok) {
    const msg =
      data && typeof data === "object" && typeof (data as { error?: unknown }).error === "string"
        ? (data as { error: string }).error
        : `Lỗi ${res.status}`;
    throw new ApiError(res.status, msg);
  }
  return convert(data, toCamel, RAW_RESPONSE_KEYS) as T;
}

export const http = {
  get: <T>(path: string, query?: Query) => request<T>("GET", path, { query }),
  post: <T>(path: string, body?: unknown, query?: Query, rawBody = false) =>
    request<T>("POST", path, { body, query, rawBody }),
  put: <T>(path: string, body?: unknown, rawBody = false, query?: Query) =>
    request<T>("PUT", path, { body, rawBody, query }),
  patch: <T>(path: string, body?: unknown) => request<T>("PATCH", path, { body }),
  delete: <T>(path: string, query?: Query) => request<T>("DELETE", path, { query }),
};