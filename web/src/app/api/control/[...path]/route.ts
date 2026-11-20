import type { NextRequest } from "next/server";
import { SESSION_COOKIE, verifyToken } from "@/lib/session";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

// Chỉ chạy ở phía server
const BASE = (process.env.CONTROL_API_URL ?? "http://127.0.0.1:18080").replace(/\/+$/, "");
const TOKEN = process.env.CONTROL_API_TOKEN;

function upstreamUrl(path: string[], search: string): string | null {
  // chặn đường dẫn rỗng và ".." để không thoát khỏi /api/v1
  if (path.length === 0 || path.some((s) => s === "" || s === "." || s === "..")) return null;
  // /healthz và /readyz nằm ngoài /api/v1 (SRS 3.1.10)
  if (path.length === 1 && (path[0] === "healthz" || path[0] === "readyz")) {
    return `${BASE}/${path[0]}${search}`;
  }
  return `${BASE}/api/v1/${path.map(encodeURIComponent).join("/")}${search}`;
}

async function forward(
  req: NextRequest,
  ctx: { params: Promise<{ path: string[] }> },
) {
  // Chỉ người đã đăng nhập console mới được gọi proxy
  const user = await verifyToken(req.cookies.get(SESSION_COOKIE)?.value);
  if (!user) return Response.json({ error: "Chưa đăng nhập." }, { status: 401 });

  const { path } = await ctx.params;
  const url = upstreamUrl(path, req.nextUrl.search);
  if (!url) return Response.json({ error: "Đường dẫn không hợp lệ." }, { status: 404 });

  const headers = new Headers();
  for (const h of ["content-type", "accept", "last-event-id"]) {
    const v = req.headers.get(h);
    if (v) headers.set(h, v);
  }
  if (TOKEN) headers.set("authorization", `Bearer ${TOKEN}`);

  const hasBody = ["POST", "PUT", "PATCH"].includes(req.method);
  let upstream: Response;
  try {
    upstream = await fetch(url, {
      method: req.method,
      headers,
      body: hasBody ? await req.arrayBuffer() : undefined,
      redirect: "manual",
      cache: "no-store",
      signal: req.signal, // trình duyệt ngắt thì đóng luôn kết nối phía sau (cần cho SSE)
    });
  } catch {
    return Response.json({ error: "Không kết nối được Control API." }, { status: 502 });
  }

  const out = new Headers();
  const ct = upstream.headers.get("content-type");
  if (ct) out.set("content-type", ct);
  out.set("cache-control", "no-store");
  return new Response(upstream.body, { status: upstream.status, headers: out });
}

export { forward as GET, forward as POST, forward as PUT, forward as PATCH, forward as DELETE };