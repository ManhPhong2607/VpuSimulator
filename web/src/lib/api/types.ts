export type OffReason = "manual" | "vms" | "vpu_off" | "vps_off";

export type VpuStatus = "online" | "heartbeat_off" | "waiting_vms" | "disabled";
export type VpsStatus = "running" | "off";
export type UnitResult =
  | "emitted"
  | "no_message"
  | "precondition"
  | "error"
  | "off";

export interface Heartbeat {
  enabled: boolean;
  intervalSec: number;
  lastAt: string | null; // ISO
}

export interface Vpu {
  id: string;
  appId: string; // vd "vpu-gate-a", duy nhất toàn hệ thống
  displayName: string;
  enabled: boolean;
  status: VpuStatus;
  profile: string;
  vpsCount: number;
  unitCount: number;
  eps: number; // = tổng eps các VPS
  heartbeat: Heartbeat;
}

export interface Vps {
  id: string;
  vpuId: string;
  name: string;
  streamUuid: string;
  address: string;
  url: string;
  camera: { width: number; height: number; fps: number };
  taskId: string;
  taskType: string; // vd "monitor"
  taskCount: number;
  unitCount: number;
  enabled: boolean;
  status: VpsStatus;
  offReason?: OffReason;
  profile: string; // kế thừa từ VPU
  eps: number; // = tổng eps các bài toán
}

export interface Unit {
  id: string;
  vpsId: string;
  vpuId: string;
  name: string; // tên bài toán, vd "License Plate"
  aiFlowCode: { name: string; code: number };
  taskId: string;
  enabled: boolean; // công tắc On = Auto của unit
  offReason?: OffReason;
  eps: number;
  lastResult: UnitResult;
  ruleType: number;
  vpuName: string;
  vpsName: string;
  /** Profile đang áp dụng, hoặc "reset_timer 60s" với rule density. */
  profile: string;
  appId: string; // app_id của VPU 
  roiId: number; // id của rule trong task
}
export interface ApplyResult {
  unitsUpdated: number; // SRS trả { units_updated }
}

export interface StopAllResult {
  identitiesAffected: number;
  tasksStopped: number;
}

export interface CreateVpuInput {
  appId: string;
  displayName?: string;
  profile?: string; // rỗng = mặc định hệ thống
  heartbeatEnabled: boolean;
  heartbeatIntervalSec: number;
  
}

export interface SimulatorHealth {
  online: boolean;
  broker: boolean;
  storage: boolean;
}

export interface AffectObject {
  label: string;
  overlap_ratio: number;
  dim_ratio: [number, number];
  min_area: number;
  movement: string[];
  direction: number;
}

export interface RuleRegion {
  path?: string; // WKT: POLYGON, hoặc LINESTRING như add-task thật của Traffic Signal Violation
  head?: string; // WKT POINT
  target_dims?: [number, number];
  anchor?: string;
}

// TODO(API-A): cấu trúc theo SRS 3.1.4. Giữ nguyên key gốc của VMS; key lạ được giữ nguyên khi lưu.
export interface RuleConf {
  id?: number; // roiId, khoá của unit trong task
  name?: string;
  type?: number;
  aiFlowCode?: number;
  enable?: boolean;
  reset_timer?: number;
  affect_object?: AffectObject[];
  region?: RuleRegion;
  except_rules?: unknown[];
}

export interface UnitDetail {
  unit: Unit;
  vpuName: string;
  vpsName: string;
  ruleConf: RuleConf;
}

export interface LastResponse {
  emittedAt: string; // ISO
  routingKey: string;
  kind: string;
  sizeBytes: number;
  payload: unknown;
  assumed: boolean; // true = ý nghĩa nút Response còn chờ xác nhận
}

export interface UnitLive {
  nextInSec: number | null; // null = không có lần phát kế tiếp (Off)
  lastEmit: { at: string; result: UnitResult } | null;
  notice: { at: string; message: string } | null; // vd precondition
}

// TODO(API-A): màn Profiles cần endpoint MỚI (theo ghi chú trong thiết kế). Thống nhất với bạn A.
export interface ProfileDef {
  name: string;
  isBuiltin: boolean; // normal, storm: không xoá được, có nút "Khôi phục mặc định"
  isSystemDefault: boolean; // profile mặc định của hệ thống
  interval: number;
  jitterMin: number;
  jitterMax: number;
  minGap: number;
  bypassFrames: boolean;
  mergeTimestamp: boolean; // cột "gộp timestamp"
  rampFrom: number | null;
  rampTo: number | null;
  rampDuration: number | null;
}

export type ProfileInput = Omit<ProfileDef, "isBuiltin" | "isSystemDefault">;

export type ApplyScope =
  | { kind: "all" }
  | { kind: "vpu"; vpuId: string }
  | { kind: "task"; vpuId: string; taskId: string };

export interface VpuEps {
  vpuId: string;
  name: string;
  enabled: boolean;
  profile: string;
  targetEps: number | null; // tính từ ramp; null = không có ramp
  actualEps: number; // đo mỗi giây
}

// TODO(API-A): màn Scenarios cần endpoint MỚI và một định nghĩa schema file scenario (chưa có).
export interface ScenarioFile {
  name: string;
  content: string; // nội dung JSON dạng chuỗi
}

export interface LoadedScenario {
  name: string;
  running: boolean;
  vpuCount: number;
  vpsCount: number;
  taskCount: number;
  ruleCount: number;
  seed: number | null;
  loadedAt: string; // ISO
}

export interface ValidationIssue {
  severity: "error" | "warning";
  title: string;
  detail: string;
}

export interface ScenarioValidation {
  issues: ValidationIssue[];
  okMessage: string | null; // dòng xanh "N identity, M task ... hợp lệ"
}

// TODO(API-A): Live log và Runtime settings cần endpoint + định dạng dòng log do bạn A chốt.
export type LogOutcome =
  | "emitted"
  | "status"
  | "precondition"
  | "command"
  | "error"
  | "no_message";

export interface LogMessage {
  event: string; // "message sent"
  routingKey: string;
  kind: string; // "data" | "status"
  sizeBytes: number;
  /** null khi verbose tắt lúc gửi. Nếu BE cắt ở max_body_log thì có thể là chuỗi bị cắt. */
  payload: unknown;
  truncated: boolean;
}

export interface LogEntry {
  id: string;
  at: string; // ISO
  outcome: LogOutcome;
  identity: string; // app_id
  taskId?: string;
  rule?: string;
  detail: string;
  message?: LogMessage; // không phải dòng nào cũng có bản tin kèm theo
}

export interface RuntimeSettings {
  logLevel: "info" | "debug" | "warn" | "error";
  verbose: boolean;
}

// TODO(API-A): màn Config cần endpoint đọc cấu hình đang chạy (chỉ xem), xem/xoá correlation store
// (FR-COR-5) và danh sách rule đã đăng ký. Nếu BE không lộ cấu hình ra API thì các thẻ chỉ xem
// sẽ phải hiện giá trị cố định.
export interface SystemConfig {
  broker: {
    host: string;
    outboundExchange: string;
    inboundRouting: string;
    connected: boolean;
  };
  storage: { backend: string; access: string; uploadDelay: string; retry: string };
  /** unauthenticated: true -> hiện chữ màu cảnh báo (thiết kế: "none — loopback only") */
  onDemandApi: { listener: string; auth: string; unauthenticated: boolean };
  /** modelService null -> "not implemented yet" */
  aiBackend: { mode: string; modelService: string | null; frameMode: string };
}

export interface CorrelationItem {
  id: string;
  identity: string;
  eventKey: string;
  label: string;
  writtenAt: string; // ISO
}

export interface CorrelationStore {
  total: number;
  ttl: string; // vd "1h"
  limitPerIdentity: number;
  items: CorrelationItem[]; // các entry mới nhất (có giới hạn số dòng)
}

export type RuleStatus = "registered" | "pending" | "not_registered";

export interface RuleCatalogItem {
  name: string;
  type: number;
  group: string;
  aiBackend: string;
  status: RuleStatus;
}

export type UpdateVpuInput = Partial<
  Omit<CreateVpuInput, "appId">
>;