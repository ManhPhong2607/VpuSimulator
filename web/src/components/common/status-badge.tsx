import { cn } from "@/lib/utils";
import type { OffReason, Unit, Vps, VpuStatus } from "@/lib/api";

type Tone = "ok" | "warn" | "err" | "muted";

const TONE: Record<Tone, string> = {
  ok: "bg-success-soft text-success",
  warn: "bg-warning-soft text-warning",
  err: "bg-danger-soft text-danger",
  muted: "bg-muted text-muted-foreground",
};

export function Pill({
  tone,
  children,
}: {
  tone: Tone;
  children: React.ReactNode;
}) {
  return (
    <span
      className={cn(
        "inline-block whitespace-nowrap rounded-full px-2 py-0.5 text-[11px]",
        TONE[tone],
      )}
    >
      {children}
    </span>
  );
}

const OFF_REASON: Record<OffReason, string> = {
  manual: "thủ công",
  vms: "do VMS",
  vpu_off: "VPU Off",
  vps_off: "VPS Off",
};

export function offLabel(reason?: OffReason) {
  return reason ? `Off · ${OFF_REASON[reason]}` : "Off";
}

const VPU_STATUS: Record<VpuStatus, { tone: Tone; label: string }> = {
  online: { tone: "ok", label: "online" },
  heartbeat_off: { tone: "warn", label: "heartbeat tắt" },
  waiting_vms: { tone: "warn", label: "chờ VMS" },
  disabled: { tone: "muted", label: "Off · vô hiệu hóa" },
};

export function VpuStatusBadge({ status }: { status: VpuStatus }) {
  const s = VPU_STATUS[status];
  return <Pill tone={s.tone}>{s.label}</Pill>;
}

export function VpsStatusBadge({ vps }: { vps: Vps }) {
  return vps.status === "running" ? (
    <Pill tone="ok">running</Pill>
  ) : (
    <Pill tone="muted">{offLabel(vps.offReason)}</Pill>
  );
}

const UNIT_TONE = {
  emitted: "ok",
  no_message: "muted",
  precondition: "warn",
  error: "err",
} as const;

export function UnitResultBadge({ unit }: { unit: Unit }) {
  if (unit.lastResult === "off") {
    return <Pill tone="muted">{offLabel(unit.offReason)}</Pill>;
  }
  return <Pill tone={UNIT_TONE[unit.lastResult]}>{unit.lastResult}</Pill>;
}

