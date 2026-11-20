export const SESSION_COOKIE = "vpu_session";
export const SESSION_TTL_MS = 8 * 60 * 60 * 1000; // 8 giờ

const enc = new TextEncoder();

function secret(): string {
  const s = process.env.SESSION_SECRET;
  if (!s) throw new Error("Thiếu SESSION_SECRET trong .env.local");
  return s;
}

async function hmac(data: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    enc.encode(secret()),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const sig = await crypto.subtle.sign("HMAC", key, enc.encode(data));
  return Array.from(new Uint8Array(sig))
    .map((b) => b.toString(16).padStart(2, "0"))
    .join("");
}

/** So sánh chuỗi không phụ thuộc vị trí khác biệt. */
export function safeEqual(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diff = 0;
  for (let i = 0; i < a.length; i++) diff |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diff === 0;
}

export async function createToken(user: string): Promise<string> {
  const payload = `${user}|${Date.now() + SESSION_TTL_MS}`;
  return `${payload}|${await hmac(payload)}`;
}

/** Trả về username nếu token hợp lệ và còn hạn, ngược lại null. */
export async function verifyToken(token?: string): Promise<string | null> {
  if (!token) return null;
  const [user, exp, sig] = token.split("|");
  if (!user || !exp || !sig) return null;
  if (Number(exp) < Date.now()) return null;
  const expected = await hmac(`${user}|${exp}`);
  return safeEqual(expected, sig) ? user : null;
}