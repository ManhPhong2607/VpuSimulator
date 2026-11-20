import type { ProfileDef, ProfileInput } from "@/lib/api";

/** Ô nhập lưu dạng chuỗi để cho phép để trống (ramp) và gõ dở. */
export interface ProfileDraft {
  interval: string;
  jitterMin: string;
  jitterMax: string;
  minGap: string;
  bypassFrames: boolean;
  mergeTimestamp: boolean;
  rampFrom: string;
  rampTo: string;
  rampDuration: string;
}

const s = (n: number | null | undefined) => (n == null ? "" : String(n));

export function toDraft(p: ProfileDef | ProfileInput): ProfileDraft {
  return {
    interval: s(p.interval),
    jitterMin: s(p.jitterMin),
    jitterMax: s(p.jitterMax),
    minGap: s(p.minGap),
    bypassFrames: p.bypassFrames,
    mergeTimestamp: p.mergeTimestamp,
    rampFrom: s(p.rampFrom),
    rampTo: s(p.rampTo),
    rampDuration: s(p.rampDuration),
  };
}

/** Giá trị ban đầu cho dialog New profile (theo thiết kế). */
export const NEW_DRAFT: ProfileDraft = {
  interval: "1",
  jitterMin: "0",
  jitterMax: "0",
  minGap: "0",
  bypassFrames: false,
  mergeTimestamp: false,
  rampFrom: "",
  rampTo: "",
  rampDuration: "",
};

/** "" -> null; không phải số -> NaN. */
function num(raw: string): number | null {
  const t = raw.trim();
  if (t === "") return null;
  const n = Number(t);
  return Number.isFinite(n) ? n : NaN;
}

const bad = (n: number | null) => n !== null && Number.isNaN(n);

export function parseDraft(
  name: string,
  d: ProfileDraft,
): { value?: ProfileInput; error?: string } {
  const interval = num(d.interval);
  const jMin = num(d.jitterMin);
  const jMax = num(d.jitterMax);
  const gap = num(d.minGap);
  const rFrom = num(d.rampFrom);
  const rTo = num(d.rampTo);
  const rDur = num(d.rampDuration);

  const required: [string, number | null][] = [
    ["interval", interval],
    ["jitter_min", jMin],
    ["jitter_max", jMax],
    ["min_gap", gap],
  ];
  for (const [label, v] of required) {
    if (v === null || Number.isNaN(v)) return { error: `${label} phải là một số.` };
  }
  for (const [label, v] of [
    ["ramp_from", rFrom],
    ["ramp_to", rTo],
    ["ramp_duration", rDur],
  ] as const) {
    if (bad(v)) return { error: `${label} phải là một số.` };
  }

  // Các giá trị đã được kiểm tra ở trên nên chắc chắn là số
  const iv = interval as number;
  const lo = jMin as number;
  const hi = jMax as number;
  const mg = gap as number;

  if (!(iv > 0)) return { error: "interval phải > 0." };
  if (!(lo >= 0 && hi >= lo)) return { error: "Cần 0 ≤ jitter_min ≤ jitter_max." };
  if (!(mg >= 0)) return { error: "min_gap phải ≥ 0." };

  // Ramp: có ramp_duration thì ramp_from, ramp_to phải > 0 (theo thiết kế).
  // Mình thêm: ramp phải đủ cả ba hoặc để trống cả ba, tránh trạng thái nửa vời.
  if (rFrom !== null || rTo !== null || rDur !== null) {
    if (rFrom === null || rTo === null || rDur === null)
      return { error: "Ramp cần đủ ramp_from, ramp_to, ramp_duration (hoặc để trống cả ba)." };
    if (!(rFrom > 0 && rTo > 0)) return { error: "ramp_from và ramp_to phải > 0." };
    if (!(rDur > 0)) return { error: "ramp_duration phải > 0." };
  }

  return {
    value: {
      name,
      interval: iv,
      jitterMin: lo,
      jitterMax: hi,
      minGap: mg,
      bypassFrames: d.bypassFrames,
      mergeTimestamp: d.mergeTimestamp,
      rampFrom: rFrom,
      rampTo: rTo,
      rampDuration: rDur,
    },
  };
}

export function validateName(name: string, existing: string[]): string | null {
  const n = name.trim();
  if (!n) return "Nhập tên profile.";
  if (!/^[A-Za-z0-9][A-Za-z0-9_-]*$/.test(n))
    return "Tên chỉ gồm chữ, số, '-', '_' và không có khoảng trắng.";
  if (existing.some((e) => e.toLowerCase() === n.toLowerCase()))
    return `Profile "${n}" đã tồn tại.`;
  return null;
}