import { ApiError, type ControlApi } from "./control-api";
import { camelize, http } from "./http-client";
import { parseUnitId } from "./unit-id";
import type {
  ApplyResult,
  CorrelationStore,
  LastResponse,
  LoadedScenario,
  LogEntry,
  ProfileDef,
  ProfileInput,
  RuleCatalogItem,
  RuleConf,
  RuntimeSettings,
  ScenarioFile,
  ScenarioValidation,
  StopAllResult,
  Unit,
  UnitDetail,
  UnitLive,
  ValidationIssue,
  Vps,
  Vpu,
  VpuEps,
} from "./types";

const enc = encodeURIComponent;

// TODO(API-A): toàn bộ file này bám theo contract mục 4 (endpoint) và mục 9 (ví dụ phản hồi).
// Các endpoint đánh dấu "Mới" trả 404 cho tới khi bạn A thêm. Id của VPU là app_id; id của
// VPS là task id; id của unit là app_id/task/roi (xem contract mục 2).

/* ---------- kiểu phản hồi (sau camelize) khác với kiểu của FE ---------- */

type UnitDto = Unit & { resetTimerSec?: number | null };
type UnitDetailDto = UnitDto & { ruleConf: RuleConf; live?: UnitLive };
type ProfileDto = Omit<ProfileDef, "mergeTimestamp"> & { coalesceTimestamps: boolean };
type StatusDto = {
  eps: {
    achieved: number;
    target: number | null;
    byIdentity: { appId: string; achieved: number; target: number | null }[];
  };
};
type CorrelationDto = Omit<CorrelationStore, "items"> & {
  items: { id: string; appId: string; eventKey: string; label: string; writtenAt: string }[];
};
type ValidationDto = { issues: ValidationIssue[]; okIdentities: number; okTasks: number };

/* ---------- hàm chuyển đổi ---------- */

// Rule density (reset_timer > 0) bỏ qua profile: hiển thị theo reset_timer (mục 9.3)
const mapUnit = ({ resetTimerSec, ...u }: UnitDto): Unit => ({
  ...u,
  profile: resetTimerSec ? `reset_timer ${resetTimerSec}s` : u.profile,
});

// SRS gọi là coalesce_timestamps, FE gọi là mergeTimestamp
const toProfile = ({ coalesceTimestamps, ...p }: ProfileDto): ProfileDef => ({
  ...p,
  mergeTimestamp: coalesceTimestamps,
});
const fromProfile = ({ mergeTimestamp, ...p }: ProfileInput) => ({
  ...p,
  coalesceTimestamps: mergeTimestamp,
});

function unitRef(unitId: string) {
  const u = parseUnitId(unitId);
  return {
    path: `units/${enc(u.appId)}/${enc(u.taskId)}/${enc(u.roiId)}`,
    // Khoá unit gồm tên rule (contract 10.1); thiếu thì BE trả 409 khi nhiều unit cùng roi
    query: { rule: u.rule },
  };
}

/** File scenario là chuỗi JSON; parse ở FE, gửi lên dạng đối tượng nguyên trạng. */
function parseScenario(file: ScenarioFile): { doc?: unknown; invalid?: ValidationIssue } {
  try {
    return { doc: JSON.parse(file.content) };
  } catch (e) {
    return {
      invalid: {
        severity: "error",
        title: "File không phải JSON hợp lệ",
        detail: (e as Error).message,
      },
    };
  }
}

/** Chưa nối: ném lỗi rõ ràng để màn hình báo lỗi thay vì hiện dữ liệu sai. */
const todo = (what: string, why: string) => (): Promise<never> =>
  Promise.reject(new ApiError(501, `Chưa nối API cho "${what}": ${why}. Xem contract, mục 7.`));

export const httpApi: ControlApi = {
  /* ===== Hệ thống ===== */

  async getHealth() {
    // TODO(API-A) câu hỏi 11: SRS chưa định nghĩa thân phản hồi của /readyz và chưa báo object storage.
    // Tạm: online = /healthz trả 200; broker và storage = /readyz trả 200.
    const ok = (p: string) => http.get(p).then(() => true, () => false);
    const online = await ok("healthz");
    const ready = online && (await ok("readyz"));
    return { online, broker: ready, storage: ready };
  },

  getSystemConfig: todo("cấu hình hệ thống", "GET /config chưa có ví dụ phản hồi, câu hỏi 11"),

  async getRuntimeSettings() {
    // TODO(API-A) câu hỏi 8: giả định GET /config trả object lồng nhau theo khoá `log.level`, `log.verbose` (Phụ lục A).
    const cfg = await http.get<{ log?: { level?: string; verbose?: boolean } }>("config");
    const log = cfg.log;
    const levels = ["info", "debug", "warn", "error"];
    if (!log || !log.level || !levels.includes(log.level) || typeof log.verbose !== "boolean") {
      throw new ApiError(
        502,
        "GET /config không có log.level và log.verbose như dự kiến (contract, câu hỏi 8).",
      );
    }
    return { logLevel: log.level as RuntimeSettings["logLevel"], verbose: log.verbose };
  },

  async updateRuntimeSettings(patch) {
    // Tên trường body của hai POST này SRS chưa nêu; đây là đề xuất trong contract (mục 4.1).
    if (patch.logLevel !== undefined) await http.post("logs/level", { level: patch.logLevel });
    if (patch.verbose !== undefined) await http.post("logs/verbose", { enabled: patch.verbose });
    return httpApi.getRuntimeSettings();
  },

  /* ===== VPU (identity) ===== */
   // TODO(API-A): tên trường thật của BE khác mục 9 (tasks, units, last_heartbeat_at, task, roi, rule,
  // auto, next_due_at...). CHỜ JSON THẬT rồi mới viết lại hàm chuyển đổi này.
  listVpus: () => http.get<Vpu[]>("identities"),

  async listProfileNames() {
    return (await httpApi.listProfiles()).map((p) => p.name);
  },

  async createVpu(input) {
    // BE chỉ nhận app_id, profile, heartbeat (chuỗi như 30s). KHÔNG gửi ai_config (A: câu 5)
    // và display_name (A: S5, BE không lưu). TODO(API-A): giữ tên hiển thị ở FE nếu team quyết định vậy.
    const created = await http.post<Vpu>("identities", {
      appId: input.appId,
      profile: input.profile,
      heartbeat: `${input.heartbeatIntervalSec}s`,
    });
    if (!input.heartbeatEnabled) {
      // SRS chỉ cho đặt chu kỳ lúc tạo; tắt heartbeat là thao tác mới (PATCH), còn chờ mentor
      await httpApi.setHeartbeatEnabled(created.id, false);
      return { ...created, heartbeat: { ...created.heartbeat, enabled: false } };
    }
    return created;
  },

   async updateVpu(id, input) {
    // PATCH /identities/{appID} là endpoint MỚI. Không gửi displayName và aiConfig (BE không nhận).
    const heartbeat =
      input.heartbeatEnabled !== undefined || input.heartbeatIntervalSec !== undefined
        ? {
            enabled: input.heartbeatEnabled,
            interval:
              input.heartbeatIntervalSec !== undefined
                ? `${input.heartbeatIntervalSec}s`
                : undefined,
          }
        : undefined;
    return http.patch<Vpu>(`identities/${enc(id)}`, { profile: input.profile, heartbeat });
  },

  async setVpuEnabled(id, enabled) {
    await http.patch(`identities/${enc(id)}`, { enabled }); // Mới; cần FR-CMD-14 (câu hỏi 2)
  },

  async setHeartbeatEnabled(id, enabled) {
    await http.patch(`identities/${enc(id)}`, { heartbeat: { enabled } }); // Mới (câu hỏi 6)
  },

   async setHeartbeatInterval(id, intervalSec) {
    // Chu kỳ dạng chuỗi như 45s (contract 10.1)
    await http.patch(`identities/${enc(id)}`, { heartbeat: { interval: `${intervalSec}s` } });
  },

  async removeVpu(id) {
    // DELETE /api/v1/identities/{appID}?confirm=true (FR-CTL-5)
    await http.delete(`identities/${enc(id)}`, { confirm: true });
  },

  /* ===== VPS (= task) ===== */
  // TODO(API-A): tên trường thật của BE khác mục 9 (tasks, units, last_heartbeat_at, task, roi, rule,
  // auto, next_due_at...). CHỜ JSON THẬT rồi mới viết lại hàm chuyển đổi này.
  listVps: (vpuId) => http.get<Vps[]>("tasks", { app_id: vpuId }),

  async getVps(vpsId) {
    // Không cần endpoint riêng: VPS = task nên lấy từ danh sách (contract 4.3).
    const all = await http.get<Vps[]>("tasks");
    const v = all.find((x) => x.id === vpsId);
    if (!v) throw new ApiError(404, "VPS không tồn tại (có thể đã bị gỡ).");
    return v;
  },

  async setVpsEnabled(vpsId, enabled) {
    // PATCH /tasks/{appID}/{task} là endpoint MỚI (câu hỏi 1 và 2)
    const v = await httpApi.getVps(vpsId);
    await http.patch(`tasks/${enc(v.vpuId)}/${enc(v.taskId)}`, { enabled });
  },

  async removeVps(vpsId) {
    // DELETE /tasks/{appID}/{task}?confirm=true là endpoint MỚI (câu hỏi 1)
    const v = await httpApi.getVps(vpsId);
    await http.delete(`tasks/${enc(v.vpuId)}/${enc(v.taskId)}`, { confirm: true });
  },

  /* ===== Bài toán (unit) ===== */
  
  async listUnits(filter) {
    // SRS lọc theo app_id; lọc theo VPS (= task) làm ở FE.
    // TODO(API-A): nếu BE hỗ trợ lọc theo task thì truyền thẳng để khỏi tải cả danh sách.
    const list = await http.get<UnitDto[]>("units", { app_id: filter?.vpuId });
    const units = list.map(mapUnit);
    return filter?.vpsId ? units.filter((u) => u.vpsId === filter.vpsId) : units;
  },

  async setUnitEnabled(unitId, enabled) {
    const { path, query } = unitRef(unitId);
    await http.post(`${path}/auto`, { auto: enabled }, query);
  },

  async getUnitDetail(unitId): Promise<UnitDetail> {
    const { path, query } = unitRef(unitId);
    const d = await http.get<UnitDetailDto>(path, query); // Mới (câu hỏi 3)
    return { unit: mapUnit(d), vpuName: d.vpuName, vpsName: d.vpsName, ruleConf: d.ruleConf };
  },

  async updateRuleConf(unitId, conf) {
    // rule_conf gửi nguyên trạng (rawBody); id và name trong body phải khớp đường dẫn, sai thì 400.
    const { path, query } = unitRef(unitId);
    await http.put(`${path}/rule-conf`, conf, true, query);
  },

  async getUnitLive(unitId) {
    const { path, query } = unitRef(unitId);
    const d = await http.get<UnitDetailDto>(path, query);
    return d.live ?? { nextInSec: null, lastEmit: null, notice: null };
  },

  async getLastResponse(unitId) {
    // 204 khi chưa từng phát hoặc đã bị gỡ (contract 10.2 mục 4)
    const { path, query } = unitRef(unitId);
    const r = await http.get<Omit<LastResponse, "assumed"> | undefined>(
      `${path}/last-response`,
      query,
    );
    return r ? { ...r, assumed: true } : null; // vẫn gắn nhãn giả định cho tới khi chốt ý nghĩa
   },

  async forceEmit(unitId) {
    const { path, query } = unitRef(unitId);
    await http.post(`${path}/trigger`, undefined, query);
  },

  async stopAllTasks(opts) {
    // DELETE /api/v1/tasks?confirm=true -> { identities_affected, tasks_stopped }
    return http.delete<StopAllResult>("tasks", { confirm: opts.confirm });
  },

  /* ===== Profile ===== */

  async listProfiles() {
    return (await http.get<ProfileDto[]>("profiles")).map(toProfile);
  },

  async createProfile(input) {
    // Mới (câu hỏi 7)
    return toProfile(await http.post<ProfileDto>("profiles", fromProfile(input)));
  },

  async updateProfile(name, input) {
    // Mới (câu hỏi 7). Gửi ĐỦ mọi trường (FR-CFG-9).
    return toProfile(await http.put<ProfileDto>(`profiles/${enc(name)}`, fromProfile(input)));
  },

  async deleteProfile(name, opts) {
    await http.delete(`profiles/${enc(name)}`, { confirm: opts.confirm }); // Mới (câu hỏi 7)
  },

  async resetProfileToDefault(name, opts) {
    // Mới (câu hỏi 7)
    return toProfile(
      await http.post<ProfileDto>(`profiles/${enc(name)}/reset`, undefined, {
        confirm: opts.confirm,
      }),
    );
  },

  async applyProfile(profile, scope) {
    // POST /api/v1/profiles/apply  { profile, app_id?, task? } -> { units_updated }
    // Không có app_id và task nghĩa là toàn bộ.
    return http.post<ApplyResult>("profiles/apply", {
      profile,
      ...(scope.kind !== "all" ? { appId: scope.vpuId } : {}),
      ...(scope.kind === "task" ? { task: scope.taskId } : {}),
    });
  },

  async getEpsByVpu() {
    // TODO(API-A) câu hỏi 10: ghép GET /status với danh sách identity (mục 9.6).
    const [status, vpus] = await Promise.all([
      http.get<StatusDto>("status"),
      httpApi.listVpus(),
    ]);
    return vpus.map((v): VpuEps => {
      const e = status.eps.byIdentity.find((x) => x.appId === v.appId);
      return {
        vpuId: v.id,
        name: v.displayName || v.appId,
        enabled: v.enabled,
        profile: v.profile,
        targetEps: e?.target ?? null,
        actualEps: e?.achieved ?? 0,
      };
    });
  },

  /* ===== Scenario ===== */

  async getLoadedScenario() {
    // 204 nếu chưa nạp
    return (await http.get<LoadedScenario | undefined>("scenario")) ?? null;
  },

  async validateScenario(file): Promise<ScenarioValidation> {
    const { doc, invalid } = parseScenario(file);
    if (invalid) return { issues: [invalid], okMessage: null };
    // validate_only là MỚI (câu hỏi 4). Body là file scenario nguyên trạng (rawBody).
    const r = await http.post<ValidationDto>("scenario", doc, { validate_only: true }, true);
    return {
      issues: r.issues,
      okMessage:
        r.okIdentities + r.okTasks > 0
          ? `${r.okIdentities} identity, ${r.okTasks} task và mọi seed khác đều hợp lệ.`
          : null,
    };
  },

  async applyScenario(file) {
    const { doc, invalid } = parseScenario(file);
    if (invalid) throw new ApiError(400, invalid.detail);
    return http.post<LoadedScenario>("scenario", doc, undefined, true);
  },

  async removeScenario(opts) {
    // DELETE /api/v1/scenario?confirm=true
    await http.delete("scenario", { confirm: opts.confirm });
  },

  /* ===== Live log ===== */

  listLog: () => http.get<LogEntry[]>("logs", { n: 2000 }),

  subscribeLog(onEntry) {
    // TODO(API-A) câu hỏi 8 và 14: event `log` trong luồng SSE là đề xuất mới. EventSource tự nối lại
    // và gửi Last-Event-ID; nếu BE không phát event này thì sẽ không có dòng mới nào đến.
    const es = new EventSource("/api/control/stream");
    es.addEventListener("log", (e) => {
      try {
        onEntry(camelize<LogEntry>(JSON.parse((e as MessageEvent<string>).data)));
      } catch {
        /* bỏ qua dòng hỏng */
      }
    });
    return () => es.close();
  },

  /* ===== Correlation, rules ===== */

  async getCorrelation() {
    const d = await http.get<CorrelationDto>("correlation", { limit: 50 });
    return {
      ...d,
      items: d.items.map(({ appId, ...i }) => ({ ...i, identity: appId })),
    };
  },

  async deleteCorrelationEntry(id) {
    await http.delete(`correlation/${enc(id)}`); // Mới (câu hỏi 13)
  },

  async clearCorrelation(opts) {
    // DELETE /api/v1/correlation?confirm=true
    await http.delete("correlation", { confirm: opts.confirm });
  },

  listRules: () => http.get<RuleCatalogItem[]>("rules"),
};