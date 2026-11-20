export const DEFAULT_INTERVAL_SEC = 30;

/**
 * Đọc chu kỳ người dùng nhập.
 * Quy tắc theo thiết kế: "Mặc định 30s. Giá trị ≤ 0 được coi là 30s."
 * TODO(API-A): nếu BE tự chuẩn hoá giá trị ≤ 0 thì có thể bỏ nhánh này, gửi nguyên giá trị.
 */
export function parseInterval(raw: string): { sec?: number; error?: string } {
  const t = raw.trim();
  if (!/^-?\d+$/.test(t) || !Number.isSafeInteger(Number(t))) {
    return { error: "Chu kỳ phải là số nguyên (giây)." };
  }
  const n = Number(t);
  return { sec: n > 0 ? n : DEFAULT_INTERVAL_SEC };
}