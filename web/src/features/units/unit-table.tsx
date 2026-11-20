"use client";

import { useState } from "react";
import { UnitResultBadge } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Switch } from "@/components/ui/switch";
import { shortId } from "@/lib/format";
import type { Unit } from "@/lib/api";
import { cn } from "@/lib/utils";
import { useSetUnitEnabled } from "./hooks";
import { RuleEditDialog } from "./rule-edit-dialog";
import { RuleResponseDialog } from "./rule-response-dialog";

type Dlg = { kind: "edit"; unitId: string } | { kind: "response"; unit: Unit };

export function UnitTable({ vpsId, units }: { vpsId: string; units: Unit[] }) {
  const toggle = useSetUnitEnabled(vpsId);
  const [dlg, setDlg] = useState<Dlg | null>(null);

  return (
    <>
      <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
        <span>
          Bài toán{" "}
          <span className="font-normal text-[#8b8f9a]">
            · Edit và Response chỉ dùng khi bài toán On
          </span>
        </span>
        <span className="font-normal text-[#8b8f9a]">{units.length} bài toán</span>
      </div>

      {toggle.error && (
        <p role="alert" className="mx-4 mt-3 rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {toggle.error.message}
        </p>
      )}

      <table className="w-full border-collapse">
        <thead>
          <tr>
            {["On", "Bài toán", "Task", "aiFlowCode", "eps/s", "Kết quả gần nhất", ""].map((h, i) => (
              <th key={i} className={th}>{h}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {units.map((u) => {
            const parentOff = u.offReason === "vpu_off" || u.offReason === "vps_off";
            const canAct = !u.offReason;
            return (
              <tr key={u.id} className={cn(!canAct && "text-[#9aa0ad]")}>
                <td className={td}>
                  <Switch
                    checked={u.enabled}
                    disabled={parentOff}
                    onCheckedChange={(c) => toggle.mutate({ id: u.id, enabled: c })}
                    aria-label={`${u.name} bật`}
                  />
                </td>
                <td className={td}><b>{u.name}</b></td>
                <td className={cn(td, "font-mono")}>{shortId(u.taskId)}</td>
                <td className={cn(td, "font-mono")}>
                  {u.aiFlowCode.name} ({u.aiFlowCode.code})
                </td>
                <td className={cn(td, "font-mono")}><b>{u.eps}</b></td>
                <td className={td}><UnitResultBadge unit={u} /></td>
                <td className={cn(td, "whitespace-nowrap text-right")}>
                  <Button size="sm" variant="outline" disabled={!canAct}
                    onClick={() => setDlg({ kind: "edit", unitId: u.id })}>
                    Edit
                  </Button>{" "}
                  <Button size="sm" variant="outline" disabled={!canAct}
                    onClick={() => setDlg({ kind: "response", unit: u })}>
                    Response
                  </Button>
                </td>
              </tr>
            );
          })}
        </tbody>
      </table>

      {dlg?.kind === "edit" && (
        <RuleEditDialog unitId={dlg.unitId} onClose={() => setDlg(null)} />
      )}
      {dlg?.kind === "response" && (
        <RuleResponseDialog unit={dlg.unit} onClose={() => setDlg(null)} />
      )}
    </>
  );
}