"use client";

import { useState } from "react";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { Button } from "@/components/ui/button";
import { useVpsList } from "@/features/vps/hooks";
import { useVpus } from "@/features/vpu/hooks";
import { shortId } from "@/lib/format";
import type { ApplyScope, ProfileDef } from "@/lib/api";
import { useApplyProfile } from "./hooks";

const selectCls =
  "h-9 w-full rounded-md border border-input bg-background px-2.5 text-sm disabled:opacity-50";
const labelCls = "mb-1.5 block text-xs text-muted-foreground";

type ScopeKind = "all" | "vpu" | "task";

export function ApplyProfileCard({ profiles }: { profiles: ProfileDef[] }) {
  const { data: vpus = [] } = useVpus();
  const apply = useApplyProfile();

  const [profile, setProfile] = useState("");
  const [scope, setScope] = useState<ScopeKind>("vpu");
  const [vpuId, setVpuId] = useState("");
  const [taskId, setTaskId] = useState("");
  const [confirmAll, setConfirmAll] = useState(false);
  const [last, setLast] = useState<string>();

  const profileName = profile || profiles[0]?.name || "";
  const vpu = vpus.find((v) => v.id === vpuId) ?? vpus[0];
  const { data: vpsList = [] } = useVpsList(vpu?.id ?? "");
  const task = vpsList.find((v) => v.taskId === taskId) ?? vpsList[0];
  const vpuName = vpu ? vpu.displayName || vpu.appId : "";
  const totalUnits = vpus.reduce((sum, v) => sum + v.unitCount, 0);

  function run() {
    let s: ApplyScope | null = null;
    let label = "toàn bộ";
    if (scope === "vpu" && vpu) {
      s = { kind: "vpu", vpuId: vpu.id };
      label = vpuName;
    } else if (scope === "task" && vpu && task) {
      s = { kind: "task", vpuId: vpu.id, taskId: task.taskId };
      label = `${vpuName} / ${shortId(task.taskId)}`;
    } else if (scope === "all") {
      s = { kind: "all" };
    }
    if (!s || !profileName) return;

    apply.mutate(
      { profile: profileName, scope: s },
      {
        onSuccess: (r) => {
          setLast(`${profileName} → ${label}, ${r.unitsUpdated} units updated`);
          setConfirmAll(false);
        },
      },
    );
  }

  const radio = (k: ScopeKind, text: string) => (
    <label className="mt-1.5 flex items-center gap-1.5 first:mt-0">
      <input type="radio" name="scope" checked={scope === k}
        onChange={() => setScope(k)} className="accent-primary" />
      {text}
    </label>
  );

  return (
    <div className="flex-1 self-start overflow-hidden rounded-[10px] border bg-card">
      <div className="border-b px-4 py-3 font-semibold">Áp profile</div>
      <div className="flex flex-col gap-3 p-4">
        <div>
          <label htmlFor="ap-profile" className={labelCls}>Profile</label>
          <select id="ap-profile" value={profileName}
            onChange={(e) => setProfile(e.target.value)} className={selectCls}>
            {profiles.map((p) => <option key={p.name} value={p.name}>{p.name}</option>)}
          </select>
        </div>

        <div>
          <span className={labelCls}>Phạm vi (FR-SIM-9)</span>
          {radio("all", "Toàn bộ")}
          {radio("vpu", "Một VPU")}
          {radio("task", "Một task của VPU")}
        </div>

        <div className="flex gap-2">
          <select aria-label="VPU" value={vpu?.id ?? ""} disabled={scope === "all"}
            onChange={(e) => { setVpuId(e.target.value); setTaskId(""); }} className={selectCls}>
            {vpus.map((v) => (
              <option key={v.id} value={v.id}>{v.displayName || v.appId}</option>
            ))}
          </select>
          <select aria-label="Task" value={task?.taskId ?? ""} disabled={scope !== "task"}
            onChange={(e) => setTaskId(e.target.value)} className={selectCls}>
            {vpsList.map((v) => (
              <option key={v.id} value={v.taskId}>{shortId(v.taskId)} · {v.name}</option>
            ))}
          </select>
        </div>

        {apply.error && !confirmAll && (
          <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
            {apply.error.message}
          </div>
        )}

        <Button
          disabled={!profileName || apply.isPending || (scope !== "all" && !vpu)}
          onClick={() => (scope === "all" ? setConfirmAll(true) : run())}
        >
          {apply.isPending && !confirmAll ? "Đang áp dụng…" : "Áp dụng"}
        </Button>

        <p className="text-[11.5px] leading-normal text-[#8b8f9a]">
           Áp profile chỉ đổi các unit đang có; task thêm sau vẫn dùng profile của VPU.
          {last && <> Lần áp gần nhất: {last}.</>}
        </p>
      </div>

      {confirmAll && (
        <ConfirmDialog
          title={`Áp profile ${profileName} cho toàn bộ?`}
          description={`Đổi profile của mọi VPU và unit hiện có (${vpus.length} VPU, ${totalUnits} unit). Có thể áp lại profile cũ sau đó.`}
          confirmLabel="Áp dụng"
          destructive
          pending={apply.isPending}
          error={apply.error?.message}
          onClose={() => { apply.reset(); setConfirmAll(false); }}
          onConfirm={run}
        />
      )}
    </div>
  );
}