export interface ScenarioSummary {
  identities: number;
  tasks: number;
  ruleConfs: number;
  seed: number | null;
}

/**
 * Đếm nhanh nội dung file để hiện ở mục "Contains" ngay khi chọn file (trước khi Validate).
 * Đọc theo schema SRS 3.1.10: identities[].tasks[].rule_conf[].
 * TODO(API-A): bỏ hàm này nếu BE trả summary khi validate.
 */
export function summarizeScenario(content: string): ScenarioSummary | null {
  try {
    const f = (JSON.parse(content) ?? {}) as { seed?: unknown; identities?: unknown };
    const identities = Array.isArray(f.identities)
      ? (f.identities as { tasks?: unknown }[])
      : [];
    let tasks = 0;
    let ruleConfs = 0;
    for (const idn of identities) {
      const ts = Array.isArray(idn?.tasks) ? (idn.tasks as { rule_conf?: unknown }[]) : [];
      tasks += ts.length;
      for (const t of ts) {
        if (Array.isArray(t?.rule_conf)) ruleConfs += t.rule_conf.length;
      }
    }
    return {
      identities: identities.length,
      tasks,
      ruleConfs,
      seed: Number.isInteger(f.seed) ? (f.seed as number) : null,
    };
  } catch {
    return null;
  }
}