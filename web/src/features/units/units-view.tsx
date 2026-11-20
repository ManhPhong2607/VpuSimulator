"use client";

import Link from "next/link";
import { useState } from "react";
import { UnitResultBadge } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import type { Unit, UnitResult } from "@/lib/api";
import { cn } from "@/lib/utils";
import {
  useAllUnits,
  useForceEmit,
  useSetUnitEnabled,
  useStopAllTasks,
} from "./hooks";
import { UnitDetailPanel } from "./unit-detail-panel";

const selectCls =
  "h-9 rounded-md border border-input bg-background px-2.5 text-sm";
const RESULTS: UnitResult[] = ["emitted", "no_message", "precondition", "error", "off"];
const HEADERS = ["VPU", "VPS", "Bài toán", "Profile", "eps/s", "Kết quả", "On", ""];
const stop = (e: React.SyntheticEvent) => e.stopPropagation();

export function UnitsView() {
  const { data, isLoading, isError, refetch } = useAllUnits();
  const forceEmit = useForceEmit();
  const stopAll = useStopAllTasks();

  const [stopMsg, setStopMsg] = useState<string>();
  const [vpuId, setVpuId] = useState("");
  const [vpsId, setVpsId] = useState("");
  const [result, setResult] = useState("");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [confirmStop, setConfirmStop] = useState(false);

  const all = data ?? [];
  const vpuOptions = [...new Map(all.map((u) => [u.vpuId, u.vpuName])).entries()];
  const vpsOptions = [
    ...new Map(
      all.filter((u) => !vpuId || u.vpuId === vpuId).map((u) => [u.vpsId, u.vpsName]),
    ).entries(),
  ];

  const list = all.filter(
    (u) =>
      (!vpuId || u.vpuId === vpuId) &&
      (!vpsId || u.vpsId === vpsId) &&
      (!result || u.lastResult === result),
  );
  const selected = all.find((u) => u.id === selectedId) ?? null;

  return (
    <>
      <div className="flex items-center gap-2.5">
        <select
          aria-label="VPU"
          value={vpuId}
          onChange={(e) => { setVpuId(e.target.value); setVpsId(""); }}
          className={selectCls}
        >
          <option value="">Tất cả VPU</option>
          {vpuOptions.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
        </select>
        <select aria-label="VPS" value={vpsId} onChange={(e) => setVpsId(e.target.value)} className={selectCls}>
          <option value="">Tất cả VPS</option>
          {vpsOptions.map(([id, name]) => <option key={id} value={id}>{name}</option>)}
        </select>
        <select aria-label="Kết quả" value={result} onChange={(e) => setResult(e.target.value)} className={selectCls}>
          <option value="">Kết quả: tất cả</option>
          {RESULTS.map((r) => <option key={r} value={r}>{r === "off" ? "Off" : r}</option>)}
        </select>
        <span className="text-xs text-[#8b8f9a]">live · làm mới 3s</span>
        {stopMsg && <span role="status" className="text-xs text-success">{stopMsg}</span>}
        <Button
          variant="outline"
          onClick={() => { setStopMsg(undefined); setConfirmStop(true); }}
          className="ml-auto border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
        >
          Stop all tasks
        </Button>
      </div>

      {confirmStop && (
        <div role="alertdialog" aria-label="Xác nhận dừng mọi task"
          className="flex items-center justify-between rounded-lg border border-[#f2b8b8] bg-[#fdf1f1] px-3.5 py-2.5">
          <span className="text-[#8a1f1f]">
            {stopAll.error
              ? stopAll.error.message
              : "Dừng mọi task của mọi VPU? Các VPS (task) và bài toán sẽ bị gỡ, VPU vẫn giữ kết nối. Không hoàn tác (gọi API với confirm=true)."}
          </span>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              disabled={stopAll.isPending}
              onClick={() => stopAll.mutate(undefined, 
              { onSuccess: (r) => {
                setConfirmStop(false);
                 setStopMsg(`Đã dừng ${r.tasksStopped} task của ${r.identitiesAffected} VPU.`);
              } })}
              className="border-[#f2b8b8] bg-white text-danger hover:bg-[#fdeceb] hover:text-danger"
            >
              {stopAll.isPending ? "Đang dừng…" : "Xác nhận dừng"}
            </Button>
            <Button size="sm" variant="outline" onClick={() => { stopAll.reset(); setConfirmStop(false); }}>
              Huỷ
            </Button>
          </div>
        </div>
      )}

      {forceEmit.error && (
        <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {forceEmit.error.message}
        </div>
      )}

      <div className="flex items-start gap-4">
        <div className="flex-[2.3] overflow-hidden rounded-[10px] border bg-card">
          <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
            <span>Units (VPU · VPS · bài toán)</span>
            <Button asChild size="sm" variant="outline">
              <Link href="/profiles">Đổi profile…</Link>
            </Button>
          </div>

          {isLoading && <p className="p-4 text-muted-foreground">Đang tải…</p>}
          {isError && (
            <div role="alert" className="flex items-center gap-3 p-4 text-danger">
              Không tải được danh sách unit.
              <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
            </div>
          )}

          {data && (
            <table className="w-full border-collapse font-mono">
              <thead>
                <tr>{HEADERS.map((h, i) => <th key={i} className={cn(th, "font-sans")}>{h}</th>)}</tr>
              </thead>
              <tbody>
                {list.map((u) => (
                  <UnitRow
                    key={u.id}
                    unit={u}
                    selected={u.id === selectedId}
                    onSelect={() => setSelectedId(u.id)}
                    onEmit={() => forceEmit.mutate(u.id)}
                    emitting={forceEmit.isPending && forceEmit.variables === u.id}
                  />
                ))}
                {list.length === 0 && (
                  <tr>
                    <td colSpan={HEADERS.length} className="p-4 text-center font-sans text-muted-foreground">
                      Không có unit nào khớp bộ lọc.
                    </td>
                  </tr>
                )}
              </tbody>
            </table>
          )}

          <p className="px-4 py-2.5 text-[11.5px] leading-relaxed text-[#8b8f9a]">
            Hiện {list.length} / {all.length} unit, lọc để thu hẹp. Công tắc On
            chính là Auto của unit. Off kèm lý do: thủ công, do VMS (enable:
            false) hoặc do VPU hoặc VPS Off. Rule density (reset_timer &gt; 0)
            phát theo reset_timer, bỏ qua profile (FR-SIM-13).
          </p>
        </div>

        <UnitDetailPanel unit={selected} />
      </div>
    </>
  );
}

function UnitRow({
  unit: u,
  selected,
  onSelect,
  onEmit,
  emitting,
}: {
  unit: Unit;
  selected: boolean;
  onSelect: () => void;
  onEmit: () => void;
  emitting: boolean;
}) {
  const toggle = useSetUnitEnabled(u.vpsId);
  const parentOff = u.offReason === "vpu_off" || u.offReason === "vps_off";

  return (
    <tr
      onClick={onSelect}
      className={cn(
        "cursor-pointer hover:bg-[#fafbff]",
        selected && "bg-accent hover:bg-accent",
        u.offReason && "text-[#9aa0ad]",
      )}
    >
      <td className={td}>{u.vpuName}</td>
      <td className={cn(td, "font-sans")}>{u.vpsName}</td>
      <td className={td}>{u.name}</td>
      <td className={td}>{u.profile}</td>
      <td className={td}><b>{u.eps}</b></td>
      <td className={cn(td, "font-sans")}><UnitResultBadge unit={u} /></td>
      <td className={td} onClick={stop}>
        <Switch
          checked={u.enabled}
          disabled={parentOff}
          onCheckedChange={(c) => toggle.mutate({ id: u.id, enabled: c })}
          aria-label={`${u.name} On`}
        />
      </td>
      <td className={td} onClick={stop}>
        <Button size="sm" variant="outline" aria-label="Force emit"
          disabled={!!u.offReason || emitting} onClick={onEmit}>
          ⚡
        </Button>
      </td>
    </tr>
  );
}