"use client";

import { Fragment, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { VpuStatusBadge } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { timeAgo } from "@/lib/format";
import type { Vpu } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useRemoveVpu, useSetHeartbeat, useSetVpuEnabled } from "./hooks";
import { VpsSubtable } from "./vps-subtable";

const HEADERS = [
  "", "On", "VPU", "Trạng thái", "Profile", "VPS", "Bài toán",
  "eps/s", "Heartbeat", "Heartbeat gần nhất", "",
];

const stop = (e: React.SyntheticEvent) => e.stopPropagation();

export function VpuTable({
  vpus,
  onEdit,
}: {
  vpus: Vpu[];
  onEdit: (vpu: Vpu) => void;
}) {
  const router = useRouter();
  const params = useSearchParams();
  const openId = params.get("vpu");

  const setOpen = (id: string | null) =>
    router.replace(id ? `/vpu?vpu=${encodeURIComponent(id)}` : "/vpu", {
      scroll: false,
    });

  const setEnabled = useSetVpuEnabled();
  const setHb = useSetHeartbeat();
  const remove = useRemoveVpu();
  const [target, setTarget] = useState<Vpu | null>(null);

  const err = setEnabled.error ?? setHb.error;

  function toggleVpu(v: Vpu, enabled: boolean) {
    setEnabled.mutate({ id: v.id, enabled });
    if (!enabled && openId === v.id) setOpen(null); // VPU Off thì thu lại
  }

  return (
    <>
      {err && (
        <div role="alert" className="flex items-center justify-between rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          <span>{err.message}</span>
          <button
            type="button"
            onClick={() => { setEnabled.reset(); setHb.reset(); }}
            className="underline"
          >
            Đóng
          </button>
        </div>
      )}

      <div className="overflow-hidden rounded-[10px] border bg-card">
        <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
          <span>
            VPU{" "}
            <span className="font-normal text-[#8b8f9a]">
              · mỗi VPU là một app_id, tạo ở đây. Bấm một hàng để bung VPS ngay bên dưới.
            </span>
          </span>
          <span className="font-normal text-[#8b8f9a]">{vpus.length} VPU</span>
        </div>

        <table className="w-full border-collapse">
          <thead>
            <tr>
              {HEADERS.map((h, i) => (
                <th key={i} className={th}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {vpus.map((v) => {
              const open = v.enabled && v.id === openId;
              const name = v.displayName || v.appId;
              return (
                <Fragment key={v.id}>
                  <tr
                    onClick={() => v.enabled && setOpen(open ? null : v.id)}
                    className={cn(
                      v.enabled ? "cursor-pointer hover:bg-[#fafbff]" : "text-[#9aa0ad]",
                      open && "bg-accent hover:bg-accent",
                    )}
                  >
                    <td className={cn(td, "w-[22px] pr-0")}>
                      <button
                        type="button"
                        disabled={!v.enabled}
                        aria-expanded={open}
                        aria-label={
                          v.enabled
                            ? `${open ? "Thu" : "Mở"} VPS của ${name}`
                            : `${name} đang Off, bấm On để dùng`
                        }
                        className="text-muted-foreground"
                      >
                        {!v.enabled ? "⊘" : open ? "▾" : "▸"}
                      </button>
                    </td>
                    <td className={td} onClick={stop}>
                      <Switch
                        checked={v.enabled}
                        onCheckedChange={(c) => toggleVpu(v, c)}
                        aria-label={`VPU ${name} bật`}
                      />
                    </td>
                    <td className={td}>
                      <b>{name}</b>
                      <div className="font-mono text-[11px] text-[#8b8f9a]">
                        {v.appId}{!v.enabled && " · bấm On để dùng"}
                      </div>
                    </td>
                    <td className={td}><VpuStatusBadge status={v.status} /></td>
                    <td className={td}>{v.profile}</td>
                    <td className={td}>{v.vpsCount}</td>
                    <td className={td}>{v.unitCount}</td>
                    <td className={cn(td, "font-mono")}><b>{v.eps}</b></td>
                    <td className={td} onClick={stop}>
                      <Switch
                        checked={v.heartbeat.enabled}
                        onCheckedChange={(c) => setHb.mutate({ id: v.id, enabled: c })}
                        aria-label={`Heartbeat ${name}`}
                      />
                    </td>
                    <td className={cn(td, "font-mono", !v.heartbeat.enabled && "text-danger")}>
                      {timeAgo(v.heartbeat.lastAt)}
                    </td>
                    <td className={cn(td, "whitespace-nowrap text-right")} onClick={stop}>
                      <Button size="sm" variant="outline" onClick={() => onEdit(v)}>
                        Edit
                      </Button>{" "}
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => setTarget(v)}
                        className="border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
                      >
                        Remove
                      </Button>
                    </td>
                  </tr>

                  {open && (
                    <tr>
                      <td colSpan={HEADERS.length} className="border-b p-0">
                        <VpsSubtable vpu={v} />
                      </td>
                    </tr>
                  )}
                </Fragment>
              );
            })}
          </tbody>
        </table>
      </div>

      <p className="text-[11.5px] leading-relaxed text-[#8b8f9a]">
        Chỉ một VPU mở cùng lúc. VPU Off không bung được, bấm On để kích hoạt lại;
        heartbeat có công tắc riêng. Trạng thái mở lưu trên URL (?vpu=…) nên quay
        lại từ màn chi tiết vẫn giữ nguyên.
      </p>

      {target && (
        <ConfirmDialog
          title={`Gỡ VPU ${target.displayName || target.appId}?`}
          description={
            <>
              Gỡ VPU <span className="font-mono">{target.appId}</span> cùng mọi
              VPS và bài toán của nó. Heartbeat dừng và session broker được đóng.
              Không hoàn tác.
            </>
          }
          confirmLabel="Gỡ VPU"
          destructive
          pending={remove.isPending}
          error={remove.error?.message}
          onClose={() => { remove.reset(); setTarget(null); }}
          onConfirm={() =>
            remove.mutate(target.id, {
              onSuccess: () => {
                if (openId === target.id) setOpen(null);
                setTarget(null);
              },
            })
          }
        />
      )}
    </>
  );
}