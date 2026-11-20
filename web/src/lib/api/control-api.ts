import type {
  CreateVpuInput,
  UpdateVpuInput,
  SimulatorHealth,
  Unit,
  Vpu,
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
  ScenarioValidation ,
  LogEntry,
  RuntimeSettings,
  CorrelationStore,
  RuleCatalogItem,
  SystemConfig,
  StopAllResult,
} from "./types";

export interface ControlApi {
  // VPU
  listVpus(): Promise<Vpu[]>;
  createVpu(input: CreateVpuInput): Promise<Vpu>; // trùng app_id -> ném lỗi 409
  updateVpu(id: string, input: UpdateVpuInput): Promise<Vpu>;
  setVpuEnabled(id: string, enabled: boolean): Promise<void>;
  setHeartbeatEnabled(id: string, enabled: boolean): Promise<void>;
  setHeartbeatInterval(vpuId: string, intervalSec: number): Promise<void>;
  removeVpu(id: string): Promise<void>;

  // VPS
  listVps(vpuId: string): Promise<Vps[]>;
  getVps(vpsId: string): Promise<Vps>;
  setVpsEnabled(vpsId: string, enabled: boolean): Promise<void>;
  removeVps(vpsId: string): Promise<void>;
    
  // Bài toán (unit)
  listUnits(filter?: { vpuId?: string; vpsId?: string }): Promise<Unit[]>;
  setUnitEnabled(unitId: string, enabled: boolean): Promise<void>;
  
  getUnitDetail(unitId: string): Promise<UnitDetail>;
  updateRuleConf(unitId: string, conf: RuleConf): Promise<void>;
  /** null = chưa có payload (verbose tắt hoặc bài toán chưa phát). */
  getLastResponse(unitId: string): Promise<LastResponse | null>;

  // hệ thống
  getHealth(): Promise<SimulatorHealth>;
  listProfileNames(): Promise<string[]>;

  getUnitLive(unitId: string): Promise<UnitLive>;
  forceEmit(unitId: string): Promise<void>;
  /** DELETE /tasks?confirm=true (FR-CTL-5, FR-CTL-8): dừng mọi task của mọi VPU, VPU vẫn giữ kết nối. */
  stopAllTasks(opts: { confirm: true }): Promise<StopAllResult>;

  // ===== Profiles =====
  // listProfileNames (dùng ở form VPU) có thể bỏ và lấy tên từ listProfiles.
  listProfiles(): Promise<ProfileDef[]>;
  createProfile(input: ProfileInput): Promise<ProfileDef>; // trùng tên -> ném ApiError 409
  /** Gửi ĐỦ mọi trường, không gửi một phần (tránh lỗi FR-CFG-9: trường còn lại về 0). */
  updateProfile(name: string, input: ProfileInput): Promise<ProfileDef>;
  deleteProfile(name: string, opts: { confirm: true }): Promise<void>; // FR-CTL-5
  resetProfileToDefault(name: string, opts: { confirm: true }): Promise<ProfileDef>;
  /** Phạm vi "all" bắt buộc confirm: true. Áp cho VPU cũng đổi profile của chính VPU (lỗi W-10). */
  /** SRS không yêu cầu confirm cho thao tác này; giao diện vẫn hỏi xác nhận khi áp cho toàn bộ. */
  applyProfile(profile: string, scope: ApplyScope): Promise<ApplyResult>;
  /** Chỉ đọc: eps mục tiêu (từ ramp) và eps đo được của từng VPU. */
  getEpsByVpu(): Promise<VpuEps[]>;

    // ===== Scenario =====
 // TODO(API-A): endpoint MỚI. Hỏi bạn A: (1) schema file scenario; (2) gửi file dạng multipart 
  // hay JSON body; (3) nạp scenario thứ hai thì thay thế, chồng lên hay bị từ chối (W-11).
  getLoadedScenario(): Promise<LoadedScenario | null>;
  /** Validate toàn bộ, KHÔNG thay đổi hệ thống (FR-CTL-7). */
  validateScenario(file: ScenarioFile): Promise<ScenarioValidation>;
  /** Chỉ gọi sau khi validate không còn lỗi; BE vẫn nên tự validate lại. */
  applyScenario(file: ScenarioFile): Promise<LoadedScenario>;
  /** DELETE /scenario, bắt buộc confirm: true (FR-CTL-5). */
  removeScenario(opts: { confirm: true }): Promise<void>;

    // ===== Live log =====
  // TODO(API-A): giả định BE có (1) lấy bộ đệm hiện tại (2000 dòng, FR-OBS-4) và (2) luồng SSE dòng mới.
  // Hỏi bạn A: định dạng dòng log; cách tránh mất dòng giữa lúc lấy snapshot và lúc nối SSE (Last-Event-ID).
  listLog(): Promise<LogEntry[]>; // mới nhất ở đầu
  /** Đăng ký nhận dòng log mới, trả về hàm huỷ. Bản thật dùng EventSource trong http-api.ts. */
  subscribeLog(onEntry: (entry: LogEntry) => void): () => void;

  // ===== Runtime settings (dùng ở Live log và màn Config) =====
  // TODO(API-A): thiết kế ghi POST /api/v1/logs/level và /logs/verbose. Hỏi bạn A có GET để đọc
  // giá trị hiện tại không (nếu không thì FE phải tự nhớ giá trị đã đặt).
  getRuntimeSettings(): Promise<RuntimeSettings>;
  updateRuntimeSettings(patch: Partial<RuntimeSettings>): Promise<RuntimeSettings>;

    // ===== Config (chỉ xem) =====
  // TODO(API-A): endpoint đọc cấu hình đang chạy (broker, storage, AI backend...). Hỏi bạn A.
  getSystemConfig(): Promise<SystemConfig>;

  // TODO(API-A): FR-COR-5 "view & clear via Control API". Hỏi bạn A: phân trang danh sách
  // (hiện chỉ lấy các entry mới nhất); TTL và giới hạn theo identity lấy từ correlation store.
  getCorrelation(): Promise<CorrelationStore>;
  deleteCorrelationEntry(id: string): Promise<void>;
  /** Xoá toàn bộ, bắt buộc confirm: true (FR-CTL-5). */
  clearCorrelation(opts: { confirm: true }): Promise<void>;

  // TODO(API-A): thiết kế ghi GET /api/v1/rules, đúng danh sách IRuleHandlerRegistry đã đăng ký.
  listRules(): Promise<RuleCatalogItem[]>;
}

export class ApiError extends Error {
  constructor(
    public status: number,
    message: string,
  ) {
    super(message);
  }
}