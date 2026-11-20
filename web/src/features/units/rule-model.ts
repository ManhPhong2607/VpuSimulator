import type { AffectObject, RuleConf } from "@/lib/api";

export interface Row {
  key: number; // khoá ổn định để xoá dòng không lệch ô nhập
  obj: AffectObject;
}

export interface FormState {
  rows: Row[];
  /** rule rỗng affect_object thì không tự thêm key này khi lưu (SRS: key này không bắt buộc). */
  hasAffectObject: boolean;
  /** Mọi trường còn lại của rule_conf (kể cả trường lạ) được giữ nguyên. */
  extra: Omit<RuleConf, "affect_object">;
  exceptText: string;
}

export const OBJECT_LABELS = [
  "car", "motorbike", "truck", "bus", "person", "bicycle",
];

let seq = 0;
const nextKey = () => ++seq;

export function newObject(): AffectObject {
  return {
    label: "car",
    overlap_ratio: 0.3,
    dim_ratio: [0.3, 4],
    min_area: 0,
    movement: [],
    direction: 0,
  };
}

export function newRow(): Row {
  return { key: nextKey(), obj: newObject() };
}

export function toFormState(conf: RuleConf): FormState {
  const { affect_object, ...extra } = conf;
  return {
    rows: (affect_object ?? []).map((obj) => ({ key: nextKey(), obj })),
    hasAffectObject: affect_object !== undefined,
    extra,
    exceptText: JSON.stringify(extra.except_rules ?? []),
  };
}

export function buildConf(s: FormState): { conf?: RuleConf; error?: string } {
  let except: unknown;
  try {
    except = JSON.parse(s.exceptText.trim() || "[]");
  } catch {
    return { error: "except_rules không phải JSON hợp lệ." };
  }
  if (!Array.isArray(except)) {
    return { error: "except_rules phải là một mảng, ví dụ []." };
  }
  const keepExcept = except.length > 0 || "except_rules" in s.extra;
  const keepAffect = s.hasAffectObject || s.rows.length > 0;
  return {
    conf: {
      ...s.extra, // id, name, type, region... đứng trước cho dễ đọc ở tab JSON
      ...(keepAffect ? { affect_object: s.rows.map((r) => r.obj) } : {}),
      ...(keepExcept ? { except_rules: except } : {}),
    },
  };
}

export function parseJson(text: string): { conf?: RuleConf; error?: string } {
  try {
    const v: unknown = JSON.parse(text);
    if (typeof v !== "object" || v === null || Array.isArray(v)) {
      return { error: "rule_conf phải là một object JSON." };
    }
    return { conf: v as RuleConf };
  } catch (e) {
    return { error: `JSON không hợp lệ: ${(e as Error).message}` };
  }
}

export function validateConf(c: RuleConf): string | null {
  // IF-RULE-2: type phải là số nguyên
  if (c.type !== undefined && !Number.isInteger(c.type))
    return "type phải là số nguyên.";

  if (c.affect_object !== undefined) {
    if (!Array.isArray(c.affect_object)) return "affect_object phải là một mảng.";
    for (const [i, o] of c.affect_object.entries()) {
      const n = i + 1;
      if (typeof o !== "object" || o === null) return `Dòng ${n}: không hợp lệ.`;
      if (!o.label) return `Dòng ${n}: thiếu label.`;
      if (!(o.overlap_ratio >= 0 && o.overlap_ratio <= 1))
        return `Dòng ${n}: overlap phải trong khoảng 0 đến 1.`;
      const dr: unknown[] = Array.isArray(o.dim_ratio) ? o.dim_ratio : [];
      const [lo, hi] = dr;
      if (typeof lo !== "number" || typeof hi !== "number" || !(lo >= 0 && hi >= lo))
        return `Dòng ${n}: dim_ratio cần 0 ≤ min ≤ max.`;
      if (!(o.min_area >= 0)) return `Dòng ${n}: min_area phải ≥ 0.`;
    }
  }

  if (c.reset_timer !== undefined && !(c.reset_timer >= 0))
    return "reset_timer phải ≥ 0.";

  // SRS 3.1.4: region là object { path, head, target_dims, anchor }
  const r = c.region;
  if (r !== undefined) {
    if (typeof r !== "object" || r === null || Array.isArray(r))
      return "region phải là một object { path, head, target_dims, anchor }.";
    if (r.head !== undefined && r.head !== "") {
      if (typeof r.head !== "string" || !/^POINT\s*\(.+\)$/i.test(r.head.trim()))
        return "region.head phải có dạng POINT(x y).";
    }
  }
  return null;
}