"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { VpsStatusBadge } from "@/components/common/status-badge";
import { Topbar } from "@/components/layout/topbar";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { useVpus } from "@/features/vpu/hooks";
import { UnitTable } from "@/features/units/unit-table";
import { useUnits } from "@/features/units/hooks";
import { shortId } from "@/lib/format";
import { useRemoveVps, useToggleVps, useVps } from "./hooks";

function Info({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-2.5">
      <span className="w-[84px] shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-all">{children}</span>
    </div>
  );
}

export function VpsDetail({ vpsId }: { vpsId: string }) {
  const router = useRouter();
  const vpsQ = useVps(vpsId);
  const unitsQ = useUnits(vpsId);
  const vpusQ = useVpus();
  const toggle = useToggleVps();
  const remove = useRemoveVps();
  const [confirm, setConfirm] = useState(false);

  const vps = vpsQ.data;
  const units = unitsQ.data ?? [];
  const vpu = vpusQ.data?.find((v) => v.id === vps?.vpuId);
  const vpuName = vpu ? vpu.displayName || vpu.appId : "…";
  const backHref = vps ? `/vpu?vpu=${vps.vpuId}` : "/vpu";

  if (!vps) {
    return (
      <>
        <Topbar crumbs={[{ label: "VPU manager", href: "/vpu" }, { label: "VPS" }]} />
        {vpsQ.isError ? (
          <div role="alert" className="flex items-center gap-3 rounded-md bg-danger-soft px-3 py-2 text-danger">
            Không tìm thấy VPS này (có thể đã bị gỡ).
            <Button asChild size="sm" variant="outline"><Link href="/vpu">Về VPU manager</Link></Button>
          </div>
        ) : (
          <p className="text-muted-foreground">Đang tải…</p>
        )}
      </>
    );
  }

  const lockedByVpu = vps.offReason === "vpu_off";

  return (
    <>
      <Topbar
        crumbs={[
          { label: "VPU manager", href: backHref },
          { label: vpuName },
          { label: vps.name },
        ]}
      />

      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3.5">
          <div>
            <h1 className="text-lg font-bold">{vps.name}</h1>
            <div className="mt-0.5 font-mono text-xs text-muted-foreground">
              stream {vps.streamUuid} · VPU {vpuName}
            </div>
          </div>
          <VpsStatusBadge vps={vps} />
        </div>
        <div className="flex items-center gap-5">
          <div className="flex items-center gap-2">
            <Button
              size="sm"
              variant="outline"
              onClick={() => setConfirm(true)}
              className="mr-1.5 border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
            >
              Remove VPS
            </Button>
            <span className="text-muted-foreground">VPS</span>
            <Switch
              checked={vps.enabled}
              disabled={lockedByVpu || toggle.isPending}
              onCheckedChange={(c) => toggle.mutate({ id: vps.id, enabled: c })}
              aria-label="VPS bật"
            />
          </div>
          <div className="text-right">
            <div className="text-[11px] text-muted-foreground">eps/s</div>
            <b className="text-xl">{vps.eps}</b>
          </div>
        </div>
      </div>

      {toggle.error && (
        <p role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {toggle.error.message}
        </p>
      )}

      <div className="flex items-start gap-4">
        <div className="flex-1 self-start overflow-hidden rounded-[10px] border bg-card">
          <div className="border-b px-4 py-3 font-semibold">Thông tin VPS</div>
          <div className="flex flex-col gap-[9px] px-4 py-3.5 text-[12.5px]">
            <Info label="Tên nguồn">{vps.name}</Info>
            <Info label="Địa chỉ">{vps.address}</Info>
            <Info label="URL"><span className="font-mono">{vps.url}</span></Info>
            <Info label="Camera">
              <span className="font-mono">
                {vps.camera.width} × {vps.camera.height} · {vps.camera.fps} fps
              </span>
            </Info>
            <Info label="Task">
              <span className="font-mono">{shortId(vps.taskId)}</span> ({vps.taskType})
            </Info>
            <Info label="Profile">
              {vps.profile}{" "}
              <span className="text-[#8b8f9a]">· kế thừa từ VPU {vpuName}</span>
            </Info>
          </div>
        </div>

        <div className="flex-[2.3] overflow-hidden rounded-[10px] border bg-card">
          {unitsQ.isLoading && <p className="p-4 text-muted-foreground">Đang tải bài toán…</p>}
          {unitsQ.isError && <p className="p-4 text-danger">Không tải được danh sách bài toán.</p>}
          {unitsQ.data && <UnitTable vpsId={vps.id} units={units} />}
        </div>
      </div>

      <p className="text-[11.5px] leading-relaxed text-[#8b8f9a]">
        {units.length > 0 && (
          <>eps của VPS = {units.map((u) => u.eps).join(" + ")} = {vps.eps}. </>
        )}
        Bài toán Off không phát nên là 0. VPS Off thì mọi bài toán Off và nút
        Edit, Response bị khoá.
      </p>

      {confirm && (
        <ConfirmDialog
          title={`Gỡ VPS ${vps.name}?`}
          description={`Gỡ VPS cùng ${vps.taskCount} task và ${vps.unitCount} bài toán (gửi end-task). VPS biến mất khỏi danh sách; nếu VMS gửi lại add-task thì VPS được tạo lại.`}
          confirmLabel="Gỡ VPS"
          destructive
          pending={remove.isPending}
          error={remove.error?.message}
          onClose={() => { remove.reset(); setConfirm(false); }}
          onConfirm={() =>
            remove.mutate(vps.id, { onSuccess: () => router.push(backHref) })
          }
        />
      )}
    </>
  );
}