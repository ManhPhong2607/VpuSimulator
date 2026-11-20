import { ApiError, type ControlApi } from "./control-api";
import { makeUnitId } from "./unit-id";
import type {
  OffReason,
  Unit,
  UnitResult,
  Vpu,
  VpuStatus,
  Vps,
  LastResponse,
  RuleConf,
  UnitDetail,
  UnitLive,
  ApplyResult,
  ApplyScope,
  ProfileDef,
  ProfileInput,
  VpuEps,
  LoadedScenario,
  ScenarioFile,
  ScenarioValidation,
  ValidationIssue,
  LogEntry,
  LogMessage,
  RuntimeSettings,
  CorrelationItem,
  RuleCatalogItem,
  SystemConfig,
} from "./types";

/* ---------- dữ liệu gốc (raw) ---------- */
interface RawVpu {
  id: string;
  appId: string;
  displayName: string;
  enabled: boolean;
  profile: string;
  hbEnabled: boolean;
  hbIntervalSec: number;
  hbLastAt: string | null;
}
interface RawVps {
  id: string;
  vpuId: string;
  name: string;
  streamUuid: string;
  address: string;
  url: string;
  taskId: string;
  enabled: boolean;
  offReason?: OffReason;
  
}
interface RawUnit {
  id: string;
  vpsId: string;
  name: string;
  aiFlowCode: { name: string; code: number };
  enabled: boolean;
  offReason?: OffReason;
  baseEps: number;
  lastResult: UnitResult;
  timerSec?: number; // rule density: phát theo reset_timer, bỏ qua profile
  lastEmitAt?: string;
  ruleType?: number; // mã zone thật khi unit đến từ scenario
  roiId: number;
  profile?: string;  // profile áp riêng (W-10); không có = kế thừa từ identity
}

const now = () => new Date().toISOString();
const ago = (s: number) => new Date(Date.now() - s * 1000).toISOString();

let vpus: RawVpu[] = [
  { id: "1", appId: "vpu-gate-a", displayName: "Gate-A", enabled: true, profile: "normal", hbEnabled: true, hbIntervalSec: 30, hbLastAt: ago(4) },
  { id: "2", appId: "vpu-junction-b", displayName: "Junction-B", enabled: true, profile: "load-ramp", hbEnabled: false, hbIntervalSec: 30, hbLastAt: ago(180) },
  { id: "3", appId: "vpu-park-c", displayName: "Park-C", enabled: false, profile: "normal", hbEnabled: true, hbIntervalSec: 30, hbLastAt: ago(2) },
  { id: "4", appId: "vpu-depot-d", displayName: "Depot-D", enabled: true, profile: "normal", hbEnabled: true, hbIntervalSec: 30, hbLastAt: ago(1) },
];

const mkVps = (
  id: string,
  vpuId: string,
  name: string,
  streamUuid: string,
  address: string,
  enabled = true,
  offReason?: OffReason,
): RawVps => ({
  id, vpuId, name, streamUuid, address,
  url: "rtsp://10.1.100.4:8554/stream",
  taskId: `3b24c9a1-5e2d-4f3a-9c10-${id.padStart(12, "0")}`,
  enabled, offReason,
});

let vpsList: RawVps[] = [
  mkVps("11", "1", "Cam ngã tư Lê Lợi", "4ba2fa87-7516-4756-af74-dba1d7fce4c9", "Lê Lợi, Việt Trì"),
  mkVps("12", "1", "Cổng bãi xe", "7c10aa11-0000-4000-8000-0000000fa1e2", "Cổng chính"),
  mkVps("13", "1", "Nút giao 04", "e90d0000-0000-4000-8000-000000000b44", "Nút giao 04", false, "vms"),
  mkVps("21", "2", "Ngã sáu Hùng Vương", "aa210000-0000-4000-8000-000000000021", "Hùng Vương"),
  mkVps("22", "2", "Cổng phụ B2", "aa220000-0000-4000-8000-000000000022", "Cổng B2"),
  mkVps("31", "3", "Bãi xe C1", "cc310000-0000-4000-8000-000000000031", "Bãi C1"),
];

const mkUnit = (
  id: string,
  vpsId: string,
  name: string,
  flow: [string, number],
  baseEps: number,
  enabled = true,
  offReason?: OffReason,
): RawUnit => ({
  id, vpsId, name,
  aiFlowCode: { name: flow[0], code: flow[1] },
  enabled, offReason, baseEps,
  lastResult: "emitted",
  roiId: 0, // sẽ được gán lại ngay bên dưới (theo thứ tự trong mỗi VPS)
});

let units: RawUnit[] = [
  mkUnit("u1", "11", "License Plate", ["VehicleRecognition", 0], 2),
  mkUnit("u2", "11", "Traffic Signal Violation", ["RunningRedLight", 5], 3),
  mkUnit("u3", "11", "Wrong Way", ["DrivingWrongLane", 9], 4),
  mkUnit("u4", "11", "Traffic Light", ["RunningRedLight", 5], 0, false, "manual"),
  mkUnit("u5", "12", "Person Recognition", ["PersonRecognition", 1], 2),
  mkUnit("u6", "12", "Disaster Detection", ["DisasterDetection", 14], 1),
  mkUnit("u7", "13", "TRAFFIC FLOW", ["TrafficFlow", 20], 0, false, "vms"),
  mkUnit("u8", "21", "Abnormal Detection", ["AbnormalDetection", 8], 3),
  mkUnit("u9", "31", "License Plate", ["VehicleRecognition", 0], 2),
];

units.find((u) => u.id === "u7")!.timerSec = 60;
units.find((u) => u.id === "u6")!.lastResult = "no_message";
units.find((u) => u.id === "u8")!.lastResult = "precondition";
// roiId: id của rule trong task, gán theo thứ tự trong mỗi VPS (SRS 3.1.4).
// Khoá của unit là app_id/task/roi.
{
  const next = new Map<string, number>();
  for (const u of units) {
    const n = (next.get(u.vpsId) ?? 0) + 1;
    next.set(u.vpsId, n);
    u.roiId = n;
  }
}

/* ---------- rule_conf mẫu ---------- */
const RULE_TYPE: Record<string, number> = { "Traffic Signal Violation": 22 };

const ao = (label: string, overlap: number, minArea: number) => ({
  label,
  overlap_ratio: overlap,
  dim_ratio: [0.3, 4] as [number, number],
  min_area: minArea,
  movement: ["turn-left", "turn-right"],
  direction: 131,
});

const DEFAULT_CONF: RuleConf = {
  affect_object: [
    ao("motorbike", 0.8, 900),
    ao("car", 0.3, 2500),
    ao("truck", 0.3, 9000),
    ao("bus", 0.3, 9000),
  ],
  region: {
    path: "LINESTRING(495 359, 785 361)",
    head: "POINT(640 216)",
    target_dims: [0, 0],
    anchor: "top-left",
  },
  reset_timer: 15,
  except_rules: [],
};

const confs = new Map<string, RuleConf>();
function confFor(u: RawUnit): RuleConf {
  let c = confs.get(u.id);
  if (!c) {
    c = {
      name: u.name,
      type: u.ruleType ?? RULE_TYPE[u.name] ?? 0,
      ...structuredClone(DEFAULT_CONF),
    };
    confs.set(u.id, c);
  }
  return c;
}

/* ---------- profile (mock) ---------- */
// TODO(API-A): dữ liệu giả, thay bằng API thật.
const BUILTIN: Record<string, ProfileInput> = {
  normal: {
    name: "normal", interval: 2, jitterMin: 5, jitterMax: 20, minGap: 0,
    bypassFrames: false, mergeTimestamp: false,
    rampFrom: null, rampTo: null, rampDuration: null,
  },
  storm: {
    name: "storm", interval: 0.5, jitterMin: 0, jitterMax: 0, minGap: 3,
    bypassFrames: true, mergeTimestamp: true,
    rampFrom: null, rampTo: null, rampDuration: null,
  },
};
const SYSTEM_DEFAULT = "normal";

const builtinState: Record<string, ProfileInput> = structuredClone(BUILTIN);
let customProfiles: ProfileInput[] = [
  {
    name: "load-ramp", interval: 1, jitterMin: 0, jitterMax: 0, minGap: 0,
    bypassFrames: false, mergeTimestamp: false,
    rampFrom: 10, rampTo: 100, rampDuration: 60,
  },
];

const allProfiles = (): ProfileInput[] => [
  ...Object.values(builtinState),
  ...customProfiles,
];
const findProfile = (name: string) => allProfiles().find((p) => p.name === name);
const toDef = (p: ProfileInput): ProfileDef => ({
  ...p,
  isBuiltin: p.name in BUILTIN,
  isSystemDefault: p.name === SYSTEM_DEFAULT,
});

/* ---------- scenario (mock) ---------- */
// TODO(API-A): TOÀN BỘ đoạn này là giả lập. Schema file scenario dưới đây là do mình tự đặt
// (seed, identities, tasks[].identity, tasks[].rule_conf[].name/type) cho khớp các thông báo
// lỗi trong thiết kế. Validation thật do BE làm (FR-CTL-7), thay bằng http-api.ts.
let loadedScenario: LoadedScenario | null = null;
let scenarioVpuIds: string[] = [];
let scnSeq = 0;
const sid = () => `s${++scnSeq}`;

const SUPPORTED_RULES = new Set([
  "License Plate", "Traffic Signal Violation", "Wrong Way", "Traffic Light",
  "Person Recognition", "Disaster Detection", "TRAFFIC FLOW", "Abnormal Detection",
]);

interface ScnRule { id?: unknown; name?: unknown; type?: unknown; aiFlowCode?: unknown }
interface ScnTask {
  task?: unknown; uuid?: unknown; url?: unknown;
  source_info?: { name?: unknown }; rule_conf?: unknown;
}
interface ScnIdentity { app_id?: unknown; profile?: unknown; heartbeat?: unknown; tasks?: unknown }

/** "30s", "1m", "2h", "500ms" -> giây. Không parse được -> null. Giá trị ≤ 0 nghĩa là 30s (Phụ lục A). */
function parseDurationSec(v: string): number | null {
  const m = /^(-?\d+(?:\.\d+)?)(ms|s|m|h)$/.exec(v.trim());
  if (!m) return null;
  const unit = { ms: 0.001, s: 1, m: 60, h: 3600 }[m[2] as "ms" | "s" | "m" | "h"];
  const sec = Number(m[1]) * unit;
  return sec > 0 ? Math.max(1, Math.round(sec)) : 30;
}

function analyzeScenario(content: string) {
  const issues: ValidationIssue[] = [];
  const err = (title: string, detail: string) =>
    issues.push({ severity: "error", title, detail });
  const warn = (title: string, detail: string) =>
    issues.push({ severity: "warning", title, detail });
  const out = {
    issues,
    okMessage: null as string | null,
    identities: [] as ScnIdentity[],
    seed: null as number | null,
  };

  let raw: unknown;
  try {
    raw = JSON.parse(content);
  } catch (e) {
    err("File không phải JSON hợp lệ", (e as Error).message);
    return out;
  }
  const f = (raw && typeof raw === "object" && !Array.isArray(raw) ? raw : {}) as {
    seed?: unknown; identities?: unknown;
  };

  // Theo SRS 3.1.10 và FR-CTL-7
  if (!Array.isArray(f.identities) || f.identities.length === 0) {
    err("Danh sách identities rỗng", "identities phải là mảng có ít nhất một phần tử (FR-CTL-7).");
    return out;
  }
  // TODO(API-A): FR-CTL-7 còn yêu cầu "seed (nếu có) trùng seed đang chạy"; mock chưa có seed đang chạy.
  const seedOk = f.seed === undefined || (Number.isInteger(f.seed) && (f.seed as number) >= 0);
  if (!seedOk) err("seed không hợp lệ", `seed = ${JSON.stringify(f.seed)}: phải là số nguyên không âm.`);

  const identities = f.identities as ScnIdentity[];
  const existing = new Set(vpus.map((p) => p.appId));
  const seenApp = new Set<string>();
  const seenTask = new Set<string>();
  const badIdentities = new Set<number>();
  let okTasks = 0;

  identities.forEach((idn, i) => {
    const at = `identities[${i}]`;
    if (!idn || typeof idn !== "object") {
      badIdentities.add(i);
      err(`${at} không hợp lệ`, "Mỗi identity phải là một object.");
      return;
    }
    let bad = false;

    const appId = typeof idn.app_id === "string" ? idn.app_id.trim() : "";
    if (!appId) {
      bad = true;
      err(`app_id rỗng: ${at}`, "app_id phải là chuỗi khác rỗng.");
    } else if (existing.has(appId)) {
      bad = true;
      err(`app_id trùng: "${appId}"`, `${at}: đã tồn tại trong hệ thống.`);
    } else if (seenApp.has(appId)) {
      bad = true;
      err(`app_id trùng: "${appId}"`, `${at}: xuất hiện nhiều lần trong file.`);
    }
    if (appId) seenApp.add(appId);

    if (
      idn.heartbeat !== undefined &&
      (typeof idn.heartbeat !== "string" || parseDurationSec(idn.heartbeat) === null)
    ) {
      bad = true;
      err(`heartbeat không hợp lệ: ${at}`, `heartbeat = ${JSON.stringify(idn.heartbeat)}: cần dạng 30s, 1m hoặc 2h.`);
    }
    if (idn.profile !== undefined && (typeof idn.profile !== "string" || !findProfile(idn.profile))) {
      bad = true;
      err(`profile không tồn tại: ${at}`, `profile = ${JSON.stringify(idn.profile)}.`);
    }

    let tasks: ScnTask[] = [];
    if (idn.tasks !== undefined) {
      if (Array.isArray(idn.tasks)) tasks = idn.tasks as ScnTask[];
      else {
        bad = true;
        err(`tasks không hợp lệ: ${at}`, "tasks phải là một mảng.");
      }
    }

    tasks.forEach((t, j) => {
      const tat = `${at}.tasks[${j}]`;
      if (!t || typeof t !== "object") {
        err(`${tat} không hợp lệ`, "Mỗi task phải là một object.");
        return;
      }
      let tbad = false;
      const taskId = typeof t.task === "string" ? t.task.trim() : "";
      if (!taskId) {
        tbad = true;
        err(`task id rỗng: ${tat}`, "task phải là chuỗi khác rỗng.");
      } else if (seenTask.has(taskId)) {
        tbad = true;
        err(`task id trùng: "${taskId}"`, `${tat}: đã xuất hiện ở task khác.`);
      }
      if (taskId) seenTask.add(taskId);

      if (t.rule_conf !== undefined && !Array.isArray(t.rule_conf)) {
        tbad = true;
        err(`rule_conf không hợp lệ: ${tat}`, "rule_conf phải là một mảng.");
      }
      const rules = Array.isArray(t.rule_conf) ? (t.rule_conf as ScnRule[]) : [];
      const seenRule = new Set<string>();
      rules.forEach((r, k) => {
        const rat = `${tat}.rule_conf[${k}]`;
        // id của rule (roi id) là số nguyên. Khoá unit là (id, tên rule), nên chỉ trùng khi trùng cả hai.
        if (!Number.isInteger(r?.id)) {
          tbad = true;
          err(
            `rule_conf không hợp lệ: ${rat}`,
            `id = ${JSON.stringify(r?.id) ?? "thiếu"}: phải là số nguyên (roi id).`,
          );
        } else {
          const key = `${r.id}|${typeof r.name === "string" ? r.name : ""}`;
          if (seenRule.has(key)) {
            tbad = true;
            err(
              `rule trùng: ${rat}`,
              `id = ${r.id} và name = ${JSON.stringify(r.name) ?? "thiếu"} đã được dùng trong ${tat}.`,
            );
          }
          seenRule.add(key);
        }
        if (!r || typeof r !== "object" || !Number.isInteger(r.type)) {
          tbad = true;
          err(
            `rule_conf không hợp lệ: ${rat}`,
            `type = ${JSON.stringify(r?.type) ?? "thiếu"}: phải là số nguyên (IF-RULE-2).`,
          );
        }
        if (r && typeof r.name === "string" && !SUPPORTED_RULES.has(r.name)) {
          warn(
            `rule name chưa hỗ trợ: "${r.name}" (${tat})`,
            "Task vẫn tạo được: rule này sẽ bị bỏ qua (IF-RULE-1), không chặn Apply.",
          );
        }
      });
      if (!tbad && !bad) okTasks++;
    });

    if (bad) badIdentities.add(i);
  });

  const okIds = identities.length - badIdentities.size;
  out.okMessage =
    seedOk && okIds + okTasks > 0
      ? `${okIds} identity, ${okTasks} task và mọi seed khác đều hợp lệ.`
      : null;
  out.identities = identities;
  out.seed =
    typeof f.seed === "number" && Number.isInteger(f.seed) ? f.seed : null;
  return out;
}

/* ---------- tính giá trị suy ra ---------- */
function toUnit(u: RawUnit): Unit {
  const vps = vpsList.find((v) => v.id === u.vpsId)!;
  const vpu = vpus.find((p) => p.id === vps.vpuId)!;

  let offReason: OffReason | undefined;
  if (!vpu.enabled) offReason = "vpu_off";
  else if (!vps.enabled) offReason = "vps_off";
  else if (!u.enabled) offReason = u.offReason ?? "manual";

  const running = !offReason;
  return {
    id: makeUnitId(vpu.appId, vps.taskId, u.roiId, u.name),
    appId: vpu.appId,
    roiId: u.roiId,
    vpsId: vps.id,
    vpuId: vpu.id,
    name: u.name,
    aiFlowCode: u.aiFlowCode,
    ruleType: u.ruleType ?? RULE_TYPE[u.name] ?? 0,
    taskId: vps.taskId,
    vpuName: vpu.displayName || vpu.appId,
    vpsName: vps.name,
    profile: u.timerSec ? `reset_timer ${u.timerSec}s` : (u.profile ?? vpu.profile),
    enabled: u.enabled,
    offReason,
    eps: running ? u.baseEps : 0,
    lastResult: running ? u.lastResult : "off",
  };
}
const findUnit = (unitId: string) =>
  units.find((u) => {
    const vps = vpsList.find((v) => v.id === u.vpsId);
    const vpu = vps && vpus.find((p) => p.id === vps.vpuId);
    return !!vpu && makeUnitId(vpu.appId, vps!.taskId, u.roiId, u.name) === unitId;
  });

function toVps(v: RawVps): Vps {
  const vpu = vpus.find((p) => p.id === v.vpuId)!;
  const us = units.filter((u) => u.vpsId === v.id).map(toUnit);
  const unitProfiles = [
    ...new Set(us.filter((u) => !u.profile.startsWith("reset_timer")).map((u) => u.profile)),
  ];
  return {
    id: v.id,
    vpuId: v.vpuId,
    name: v.name,
    streamUuid: v.streamUuid,
    address: v.address,
    url: v.url,
    camera: { width: 1280, height: 720, fps: 13 },
    taskId: v.taskId,
    taskType: "monitor",
    taskCount: 1,
    unitCount: us.length,
    enabled: v.enabled,
    status: v.enabled && vpu.enabled ? "running" : "off",
    offReason: !vpu.enabled
      ? "vpu_off"
      : v.enabled
        ? undefined
        : v.offReason ?? "manual",
    profile:
      unitProfiles.length === 0
        ? vpu.profile
        : unitProfiles.length === 1
          ? unitProfiles[0]
          : "nhiều profile",
    eps: us.reduce((sum, u) => sum + u.eps, 0),
  };
}

function toVpu(p: RawVpu): Vpu {
  const vs = vpsList.filter((v) => v.vpuId === p.id).map(toVps);
  let status: VpuStatus = "online";
  if (!p.enabled) status = "disabled";
  else if (vs.length === 0) status = "waiting_vms";
  else if (!p.hbEnabled) status = "heartbeat_off";

  return {
    id: p.id,
    appId: p.appId,
    displayName: p.displayName,
    enabled: p.enabled,
    status,
    profile: p.profile,
    vpsCount: vs.length,
    unitCount: vs.reduce((sum, v) => sum + v.unitCount, 0),
    eps: vs.reduce((sum, v) => sum + v.eps, 0),
    heartbeat: {
      enabled: p.hbEnabled,
      intervalSec: p.hbIntervalSec,
      lastAt: p.hbEnabled ? ago(1 + Math.floor(Math.random() * 5)) : p.hbLastAt,
    },
  };
}

/* ---------- live log + runtime settings (mock) ---------- */
// TODO(API-A): giả lập. Thật: BE giữ bộ đệm 2000 dòng và đẩy dòng mới qua SSE.
const LOG_BUFFER = 2000;
let logBuffer: LogEntry[] = [];
let logSeq = 0;
let runtime: RuntimeSettings = { logLevel: "info", verbose: true };
const logListeners = new Set<(e: LogEntry) => void>();
let logTimer: ReturnType<typeof setInterval> | null = null;

const pickOf = <T,>(a: T[]): T => a[Math.floor(Math.random() * a.length)];

function logMessage(kind: string, routingKey: string, payload: unknown): LogMessage {
  return {
    event: "message sent",
    routingKey,
    kind,
    sizeBytes: new TextEncoder().encode(JSON.stringify(payload)).length,
    // verbose tắt: chỉ còn routing key, loại và kích thước
    payload: runtime.verbose ? payload : null,
    truncated: false,
  };
}

function dataPayload(vps: RawVps, at: number) {
  return {
    appid: "",
    scope: "task-unit",
    type: "data",
    timestamp: at,
    uuid: vps.taskId,
    detail: {
      data: [
        {
          class: 1, type: 2, version: 2, roi_id: 5,
          event_key: crypto.randomUUID(),
          event_state: 2,
          source_id: vps.streamUuid,
          source_name: vps.name,
          task: vps.taskId,
          start_time: at - 100,
          end_time: at,
          details: {
            event_type: 4,
            object_type: 0,
            object_label: pickOf(["car", "motorbike", "truck", "bus"]),
            tracker: Math.floor(Math.random() * 400),
            bbox: { x: 512, y: 300, w: 200, h: 160 },
            image_url: null, video_url: null, image_ctxt_url: null,
            attributes: null, ref_id: "", state: 3,
            vio_type_str: "INVERSEDIRECTION",
          },
        },
      ],
    },
  };
}

function makeLogEntry(at: number): LogEntry {
  const base = { id: `l${++logSeq}`, at: new Date(at).toISOString() };
  const running = units.map(toUnit).filter((u) => !u.offReason);
  const unit = running.length ? pickOf(running) : null;
  const vps = unit ? vpsList.find((v) => v.id === unit.vpsId) : undefined;
  const vpu =
    (unit && vpus.find((p) => p.id === unit.vpuId)) ||
    (vpus.length ? pickOf(vpus) : undefined);
  const identity = vpu?.appId ?? "unknown";
  const r = Math.random();

  // Không có bài toán đang chạy, hoặc tới lượt dòng status (heartbeat)
  if (!unit || !vps || (r >= 0.5 && r < 0.62)) {
    const payload = { appid: identity, scope: "process-unit", type: "heartbeat", timestamp: at };
    return {
      ...base, outcome: "status", identity,
      detail: pickOf(["running", "heartbeat"]),
      message: logMessage("status", `inbound.status.vpu.${identity}`, payload),
    };
  }
  if (r < 0.5) {
    const payload = dataPayload(vps, at);
    return {
      ...base, outcome: "emitted", identity, taskId: vps.taskId, rule: unit.name,
      detail: `event_key=${payload.detail.data[0].event_key.slice(0, 8)}…`,
      message: logMessage("data", `inbound.data.vpu.${vps.taskId}`, payload),
    };
  }
  if (r < 0.72)
    return { ...base, outcome: "no_message", identity, rule: unit.name, detail: "không phát (FR-GEN-4)" };
  if (r < 0.82)
    return { ...base, outcome: "precondition", identity, rule: unit.name, detail: "affect_object empty" };
  if (r < 0.9)
    return { ...base, outcome: "error", identity, rule: unit.name, detail: "publish failed (broker timeout)" };
  return { ...base, outcome: "command", identity, detail: "add-task rejected: rule_conf.type not int" };
}

/* ---------- config / correlation / rules (mock) ---------- */
// TODO(API-A): dữ liệu cố định hoặc giả lập (lấy đúng số liệu trong bản thiết kế); thay bằng API thật.
const SYSTEM_CONFIG: SystemConfig = {
  broker: {
    host: "rabbitmq.internal:5672",
    outboundExchange: "outbound.vpu",
    inboundRouting: "inbound.data.vpu.*",
    connected: true,
  },
  storage: {
    backend: "SeaweedFS",
    access: "S3 Gateway",
    uploadDelay: "60–300s (intentional)",
    retry: "max 3 attempts",
  },
  onDemandApi: { listener: "127.0.0.1:18001", auth: "none — loopback only", unauthenticated: true },
  aiBackend: { mode: "simulated", modelService: null, frameMode: "bypass" },
};

const RULE_CATALOG: RuleCatalogItem[] = [
  { name: "License Plate", type: 23, group: "recognition", aiBackend: "simulated", status: "registered" },
  { name: "TRAFFIC FLOW", type: 2, group: "density", aiBackend: "simulated", status: "registered" },
  { name: "Traffic Metering", type: 19, group: "density", aiBackend: "simulated", status: "registered" },
  { name: "Wrong Way", type: 16, group: "violation", aiBackend: "simulated", status: "pending" },
  { name: "Illegal Stop", type: 18, group: "violation", aiBackend: "simulated", status: "not_registered" },
];

const CORR_TTL_MS = 60 * 60 * 1000; // TTL 1h
let corr: CorrelationItem[] | null = null;

function seedCorrelation(): CorrelationItem[] {
  const ids = vpus.length ? vpus.map((p) => p.appId) : ["cam-gate-01"];
  return Array.from({ length: 142 }, () => ({
    id: crypto.randomUUID(),
    identity: pickOf(ids),
    eventKey: crypto.randomUUID(),
    label: pickOf(["car", "motorbike", "truck", "bus"]),
    writtenAt: new Date(Date.now() - Math.floor(Math.random() * CORR_TTL_MS)).toISOString(),
  }));
}

/* ---------- API ---------- */
const delay = (ms = 150) => new Promise((r) => setTimeout(r, ms));
const need = <T,>(x: T | undefined, what: string): T => {
  if (!x) throw new ApiError(404, `${what} không tồn tại`);
  return x;
};

export const mockApi: ControlApi = {
  async getHealth() {
    await delay(50);
    return { online: true, broker: true, storage: true };
  },
  async listVpus() {
    await delay();
    return vpus.map(toVpu);
  },
  
  async createVpu(input) {
    await delay();
    if (vpus.some((p) => p.appId === input.appId))
      throw new ApiError(409, `app_id "${input.appId}" đã tồn tại`);
    const raw: RawVpu = {
      id: crypto.randomUUID(),
      appId: input.appId,
      displayName: input.displayName || input.appId,
      enabled: true,
      profile: input.profile || "normal",
      hbEnabled: input.heartbeatEnabled,
      hbIntervalSec: input.heartbeatIntervalSec,
      hbLastAt: now(),
    };
    vpus = [...vpus, raw];
    return toVpu(raw);
  },

  async updateVpu(id, input) {
    await delay();
    const p = need(vpus.find((x) => x.id === id), "VPU");
    if (input.displayName !== undefined) p.displayName = input.displayName;
    if (input.profile) p.profile = input.profile;
    if (input.heartbeatEnabled !== undefined) p.hbEnabled = input.heartbeatEnabled;
    if (input.heartbeatIntervalSec) p.hbIntervalSec = input.heartbeatIntervalSec;
    return toVpu(p);
  },

  async setVpuEnabled(id, enabled) {
    await delay(80);
    need(vpus.find((x) => x.id === id), "VPU").enabled = enabled;
  },

   async setHeartbeatEnabled(id, enabled) {
    await delay(80);
    const p = need(vpus.find((x) => x.id === id), "VPU");
    if (!enabled) p.hbLastAt = now(); // chốt lần gửi cuối khi tắt
    p.hbEnabled = enabled;
  },

    async setHeartbeatInterval(id, intervalSec) {
    await delay(80);
    // TODO(API-A): quy tắc "chu kỳ ≤ 0 được coi là 30s" (thiết kế) thuộc về BE;
    // FE cũng đã chuẩn hoá trước khi gửi (heartbeat-model.ts).
    need(vpus.find((x) => x.id === id), "VPU").hbIntervalSec =
      intervalSec > 0 ? intervalSec : 30;
  },
  
  async removeVpu(id) {
    await delay();
    const ids = vpsList.filter((v) => v.vpuId === id).map((v) => v.id);
    units = units.filter((u) => !ids.includes(u.vpsId));
    vpsList = vpsList.filter((v) => v.vpuId !== id);
    vpus = vpus.filter((p) => p.id !== id);
  },

  async listVps(vpuId) {
    await delay();
    return vpsList.filter((v) => v.vpuId === vpuId).map(toVps);
  },

  async getVps(vpsId) {
    await delay();
    return toVps(need(vpsList.find((v) => v.id === vpsId), "VPS"));
  },

  async setVpsEnabled(vpsId, enabled) {
    await delay(80);
    const v = need(vpsList.find((x) => x.id === vpsId), "VPS");
    v.enabled = enabled;
    v.offReason = enabled ? undefined : "manual";
  },

  async removeVps(vpsId) {
    await delay();
    units = units.filter((u) => u.vpsId !== vpsId);
    vpsList = vpsList.filter((v) => v.id !== vpsId);
  },

  async listUnits(filter) {
    await delay();
    return units
      .map(toUnit)
      .filter((u) => !filter?.vpsId || u.vpsId === filter.vpsId)
      .filter((u) => !filter?.vpuId || u.vpuId === filter.vpuId);
  },

  async setUnitEnabled(unitId, enabled) {
    await delay(80);
    const u = need(findUnit(unitId), "Bài toán");
    u.enabled = enabled;
    u.offReason = enabled ? undefined : "manual";
  },

    async getUnitDetail(unitId) {
    await delay();
    const u = need(findUnit(unitId), "Bài toán");
    const vps = vpsList.find((v) => v.id === u.vpsId)!;
    const vpu = vpus.find((p) => p.id === vps.vpuId)!;
    return {
      unit: toUnit(u),
      vpuName: vpu.displayName || vpu.appId,
      vpsName: vps.name,
      ruleConf: structuredClone(confFor(u)),
    } satisfies UnitDetail;
  },

  async updateRuleConf(unitId, conf) {
    await delay();
    const u = need(findUnit(unitId), "Bài toán");
       if (conf.affect_object !== undefined && !Array.isArray(conf.affect_object))
      throw new ApiError(400, "affect_object phải là mảng");
    confs.set(u.id, structuredClone(conf));
  },

  async getLastResponse(unitId) {
    await delay();
    const u = need(findUnit(unitId), "Bài toán");
    const unit = toUnit(u);
    if (unit.eps === 0) return null;
    const vps = vpsList.find((v) => v.id === u.vpsId)!;
    const payload = {
      details: {
        event_type: 4,
        object_type: 0,
        object_label: "car",
        tracker: 118,
        bbox: { x: 512, y: 300, w: 340, h: 260 },
        attributes: { identity: { value: "29A12345", prob: 0.91 } },
        ref_id: "e1f0c2a4-7b3d-4c1e-9a52-0d8f6b21c7aa",
        state: 2,
        vio_type_str: "REDLIGHT",
      },
    };
    return {
      emittedAt: new Date(Date.now() - 3000).toISOString(),
      routingKey: `inbound.data.vpu.${vps.taskId}`,
      kind: "data",
      sizeBytes: new TextEncoder().encode(JSON.stringify(payload)).length,
      payload,
      assumed: true,
    } satisfies LastResponse;
  },

    async listProfileNames() {
    await delay(50);
    return allProfiles().map((p) => p.name);
  },

  async listProfiles() {
    await delay();
    return allProfiles().map(toDef);
  },

  async createProfile(input) {
    await delay();
    if (findProfile(input.name))
      throw new ApiError(409, `Profile "${input.name}" đã tồn tại`);
    customProfiles = [...customProfiles, structuredClone(input)];
    return toDef(input);
  },

  async updateProfile(name, input) {
    await delay();
    need(findProfile(name), "Profile");
    const next: ProfileInput = { ...input, name }; // không đổi tên qua update
    if (name in builtinState) builtinState[name] = next;
    else customProfiles = customProfiles.map((p) => (p.name === name ? next : p));
    return toDef(next);
  },

  async deleteProfile(name, opts) {
    await delay();
    if (!opts.confirm) throw new ApiError(400, "Thiếu confirm=true");
    if (name in BUILTIN)
      throw new ApiError(409, "Không xoá được profile mặc định của hệ thống");
    // TODO(API-A): quy tắc "đang được dùng thì không xoá" là mock tự đặt để khỏi treo tham chiếu.
    // Hỏi bạn A: BE xử lý thế nào (chặn, hay tự chuyển về mặc định)?
    if (vpus.some((p) => p.profile === name) || units.some((u) => u.profile === name))
      throw new ApiError(409, `Profile "${name}" đang được dùng, hãy áp profile khác trước.`);
    customProfiles = customProfiles.filter((p) => p.name !== name);
  },

  async resetProfileToDefault(name, opts) {
    await delay();
    if (!opts.confirm) throw new ApiError(400, "Thiếu confirm=true");
    const def = BUILTIN[name];
    if (!def)
      throw new ApiError(409, "Chỉ khôi phục được profile mặc định của hệ thống");
    builtinState[name] = structuredClone(def);
    return toDef(builtinState[name]);
  },

  async applyProfile(profile, scope: ApplyScope): Promise<ApplyResult> {
    await delay(200);
    need(findProfile(profile), "Profile");
    // W-10: chỉ đổi profile của các unit ĐANG CÓ; task thêm sau vẫn dùng profile của identity.
    // SRS không yêu cầu confirm cho thao tác này (FR-CTL-5 chỉ liệt kê thao tác phá huỷ).
    let target: RawUnit[];
    if (scope.kind === "all") {
      target = units;
    } else {
      const vpuId = scope.vpuId;
      const taskId = scope.kind === "task" ? scope.taskId : undefined;
      const vpu = need(vpus.find((p) => p.id === vpuId), "VPU");
      const vs = vpsList.filter(
        (v) => v.vpuId === vpu.id && (taskId === undefined || v.taskId === taskId),
      );
      if (taskId !== undefined && vs.length === 0) throw new ApiError(404, "Task không tồn tại");
      target = units.filter((u) => vs.some((v) => v.id === u.vpsId));
    }
    target.forEach((u) => (u.profile = profile));
    return { unitsUpdated: target.length };
  },

  async getEpsByVpu() {
    await delay(60);
    return vpus.map(toVpu).map((v): VpuEps => {
      const p = findProfile(v.profile);
      let target: number | null = null;
      // TODO(API-A): mock tự "chạy" ramp theo đồng hồ; thật thì BE tính và trả về.
      if (v.enabled && p && p.rampFrom != null && p.rampTo != null && p.rampDuration) {
        const t = ((Date.now() / 1000) % p.rampDuration) / p.rampDuration;
        target = Math.round(p.rampFrom + (p.rampTo - p.rampFrom) * t);
      }
      return {
        vpuId: v.id,
        name: v.displayName || v.appId,
        enabled: v.enabled,
        profile: v.profile,
        targetEps: target,
        actualEps: v.eps,
      };
    });
  },

    async getUnitLive(unitId) {
    await delay(60);
    const u = need(findUnit(unitId), "Bài toán");
    const unit = toUnit(u);
    const running = !unit.offReason;
    return {
      nextInSec: running ? 10 - (Math.floor(Date.now() / 1000) % 10) : null,
      lastEmit: running ? { at: u.lastEmitAt ?? ago(3), result: u.lastResult } : null,
      notice:
        running && u.lastResult === "precondition"
          ? { at: ago(33), message: "precondition: affect_object rỗng" }
          : null,
    } satisfies UnitLive;
  },

  async forceEmit(unitId) {
    await delay(120);
    const u = need(findUnit(unitId), "Bài toán");
    if (toUnit(u).offReason)
      throw new ApiError(409, "Bài toán đang Off, không phát được.");
    u.lastResult = "emitted";
    u.lastEmitAt = now();
  },

   async stopAllTasks(opts) {
    await delay(200);
    if (!opts.confirm) throw new ApiError(400, "Thiếu confirm=true");
    // SRS FR-CTL-8: dừng mọi task của mọi identity, identity vẫn kết nối.
    // VPS = task nên các VPS và bài toán biến mất, VPU còn lại (trạng thái chờ VMS).
    // TODO(API-A): xác nhận "dừng task" nghĩa là gỡ task (như end-task) chứ không phải tạm dừng.
    const result = {
      identitiesAffected: new Set(vpsList.map((v) => v.vpuId)).size,
      tasksStopped: vpsList.length,
    };
    vpsList = [];
    units = [];
    return result;
  },

    async getLoadedScenario() {
    await delay(60);
    return loadedScenario;
  },

  async validateScenario(file: ScenarioFile): Promise<ScenarioValidation> {
    await delay(250);
    const { issues, okMessage } = analyzeScenario(file.content);
    // lỗi lên trước, cảnh báo sau (giống thiết kế)
    issues.sort((a, b) => Number(b.severity === "error") - Number(a.severity === "error"));
    return { issues, okMessage };
  },

   async applyScenario(file: ScenarioFile): Promise<LoadedScenario> {
    await delay(300);
    // TODO(API-A): "đã có scenario thì từ chối" là quy tắc mock tự đặt (W-11). Hỏi bạn A.
    if (loadedScenario)
      throw new ApiError(409, "Đang có scenario nạp, hãy gỡ scenario hiện tại trước.");
    const r = analyzeScenario(file.content);
    if (r.issues.some((i) => i.severity === "error"))
      throw new ApiError(422, "Scenario còn lỗi, hãy Validate và sửa trước khi Apply.");

    // Mỗi identity -> một VPU; mỗi task -> một VPS; mỗi rule_conf -> một bài toán
    scenarioVpuIds = [];
    let taskCount = 0;
    let ruleCount = 0;
    for (const idn of r.identities) {
      const appId = (idn.app_id as string).trim();
      const vpuId = sid();
      const hb = typeof idn.heartbeat === "string" ? parseDurationSec(idn.heartbeat) : null;
      vpus.push({
        id: vpuId,
        appId,
        displayName: appId,
        enabled: true,
        profile: typeof idn.profile === "string" ? idn.profile : SYSTEM_DEFAULT,
        hbEnabled: true,
        hbIntervalSec: hb ?? 30,
        hbLastAt: now(),
      });
      scenarioVpuIds.push(vpuId);

      ((idn.tasks as ScnTask[] | undefined) ?? []).forEach((t, j) => {
        const vpsId = sid();
        const name =
          typeof t.source_info?.name === "string" ? t.source_info.name : `Task ${j + 1}`;
        const vps = mkVps(
          vpsId,
          vpuId,
          name,
          typeof t.uuid === "string" ? t.uuid : crypto.randomUUID(),
          `Scenario · ${appId}`,
        );
        vps.taskId = (t.task as string).trim();
        if (typeof t.url === "string") vps.url = t.url;
        vpsList.push(vps);
        taskCount++;

         ((t.rule_conf as ScnRule[] | undefined) ?? []).forEach((x) => {
          const ruleName = typeof x.name === "string" ? x.name : "Rule";
          const unit = mkUnit(
            sid(),
            vpsId,
            ruleName,
            [ruleName, typeof x.aiFlowCode === "number" ? x.aiFlowCode : 0],
            2,
          );
          unit.ruleType = x.type as number;
          unit.roiId = x.id as number; // đã được validate là số nguyên, duy nhất trong task
          units.push(unit);
          // lưu nguyên rule_conf VMS gửi để mở Edit bài toán thấy đúng nội dung
          confs.set(unit.id, structuredClone(x) as unknown as RuleConf);
          ruleCount++;
        });
      });
    }

    loadedScenario = {
      name: file.name,
      running: true,
      vpuCount: r.identities.length,
      vpsCount: taskCount,
      taskCount,
      ruleCount,
      seed: r.seed,
      loadedAt: now(),
    };
    return loadedScenario;
  },

  async removeScenario(opts) {
    await delay(200);
    if (!opts.confirm) throw new ApiError(400, "Thiếu confirm=true");
    for (const id of scenarioVpuIds) {
      const vs = vpsList.filter((v) => v.vpuId === id).map((v) => v.id);
      units = units.filter((u) => !vs.includes(u.vpsId));
      vpsList = vpsList.filter((v) => v.vpuId !== id);
      vpus = vpus.filter((p) => p.id !== id);
    }
    scenarioVpuIds = [];
    loadedScenario = null;
  },

    async listLog() {
    await delay();
    if (logBuffer.length === 0) {
      // lịch sử giả: 12 dòng cách nhau ~6s, mới nhất ở đầu
      logBuffer = Array.from({ length: 12 }, (_, i) => makeLogEntry(Date.now() - (i + 1) * 6000));
    }
    return [...logBuffer];
  },

  subscribeLog(onEntry) {
    logListeners.add(onEntry);
    if (!logTimer) {
      // Chỉ phát sinh dòng mới khi có người đang xem (giả lập SSE)
      logTimer = setInterval(() => {
        const e = makeLogEntry(Date.now());
        logBuffer = [e, ...logBuffer].slice(0, LOG_BUFFER);
        logListeners.forEach((l) => l(e));
      }, 1800);
    }
    return () => {
      logListeners.delete(onEntry);
      if (logListeners.size === 0 && logTimer) {
        clearInterval(logTimer);
        logTimer = null;
      }
    };
  },

  async getRuntimeSettings() {
    await delay(60);
    return { ...runtime };
  },

  async updateRuntimeSettings(patch) {
    await delay(120);
    runtime = { ...runtime, ...patch };
    return { ...runtime };
  },

    async getSystemConfig() {
    await delay(80);
    return SYSTEM_CONFIG;
  },

  async getCorrelation() {
    await delay(80);
    corr ??= seedCorrelation();
    // TTL: entry cũ hơn 1h tự hết hạn
    const cutoff = Date.now() - CORR_TTL_MS;
    corr = corr.filter((c) => new Date(c.writtenAt).getTime() > cutoff);
    const newest = [...corr].sort((a, b) => b.writtenAt.localeCompare(a.writtenAt));
    return {
      total: corr.length,
      ttl: "1h",
      limitPerIdentity: 100000,
      items: newest.slice(0, 50),
    };
  },

  async deleteCorrelationEntry(id) {
    await delay(80);
    corr = (corr ?? []).filter((c) => c.id !== id);
  },

  async clearCorrelation(opts) {
    await delay(150);
    if (!opts.confirm) throw new ApiError(400, "Thiếu confirm=true");
    corr = [];
  },

  async listRules() {
    await delay(80);
    return RULE_CATALOG;
  },
};