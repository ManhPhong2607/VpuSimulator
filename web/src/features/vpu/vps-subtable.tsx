"use client";

import Link from "next/link";
import { useState } from "react";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { VpsStatusBadge } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Switch } from "@/components/ui/switch";
import { useRemoveVps, useSetVpsEnabled, useVpsList } from "@/features/vps/hooks";
import { shortUuid } from "@/lib/format";
import type { Vps, Vpu } from "@/lib/api";
import { cn } from "@/lib/utils";

export function VpsSubtable({ vpu }: { vpu: Vpu }) {
  const { data, isLoading, isError } = useVpsList(vpu.id);
  const toggle = useSetVpsEnabled(vpu.id);
  const remove = useRemoveVps();
  const [q, setQ] = useState("");
  const [target, setTarget] = useState<Vps | null>(null);

  const all = data ?? [];
  const list = all.filter((v) =>
    v.name.toLowerCase().includes(q.trim().toLowerCase()),
  );

  return (
    <div className="border-l-[3px] border-primary bg-[#f7f8ff]">
      <div className="flex items-center justify-between px-4 py-2.5">
        <span>
          <b>VPS (camera) của {vpu.displayName || vpu.appId}</b>{" "}
          <span className="text-[#8b8f9a]">· do VMS tạo từ add-task, không tạo tay</span>
        </span>
        <span className="flex items-center gap-2.5">
          <Input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Tìm VPS…"
            aria-label="Tìm VPS"
            className="h-[30px] w-[200px]"
          />
          <span className="text-[#8b8f9a]">{all.length} VPS</span>
        </span>
      </div>

      {toggle.error && (
        <p role="alert" className="mx-4 mb-2 rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {toggle.error.message}
        </p>
      )}

      <div className="max-h-[360px] overflow-auto px-4 pb-3">
        {isLoading && <p className="py-3 text-muted-foreground">Đang tải VPS…</p>}
        {isError && <p className="py-3 text-danger">Không tải được danh sách VPS.</p>}
        {!isLoading && !isError && all.length === 0 && (
          <p className="py-3 text-muted-foreground">
            Chưa có VPS. VPS xuất hiện khi VMS kích hoạt workspace và gửi add-task.
          </p>
        )}

        {list.length > 0 && (
          <table className="w-full border-collapse">
            <thead className="sticky top-0">
              <tr>
                {["On", "VPS (camera)", "Stream uuid", "Trạng thái", "Task", "Bài toán", "eps/s", ""].map((h) => (
                  <th key={h} className={th}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {list.map((v) => (
                <tr key={v.id} className={cn(!v.enabled && "text-[#9aa0ad]")}>
                  <td className={td}>
                    <Switch
                      checked={v.enabled}
                      onCheckedChange={(c) => toggle.mutate({ id: v.id, enabled: c })}
                      aria-label={`VPS ${v.name} bật`}
                    />
                  </td>
                  <td className={td}><b>{v.name}</b></td>
                  <td className={cn(td, "font-mono")} title={v.streamUuid}>
                    {shortUuid(v.streamUuid)}
                  </td>
                  <td className={td}><VpsStatusBadge vps={v} /></td>
                  <td className={td}>{v.taskCount}</td>
                  <td className={td}>{v.unitCount}</td>
                  <td className={cn(td, "font-mono")}><b>{v.eps}</b></td>
                  <td className={cn(td, "whitespace-nowrap text-right")}>
                    <Button asChild size="sm">
                      <Link href={`/vps/${v.id}`}>Bài toán →</Link>
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
              ))}
            </tbody>
          </table>
        )}
        {list.length === 0 && all.length > 0 && (
          <p className="py-3 text-muted-foreground">Không có VPS nào khớp “{q}”.</p>
        )}
      </div>

      {target && (
        <ConfirmDialog
          title={`Gỡ VPS ${target.name}?`}
          description={`Gỡ VPS cùng ${target.taskCount} task và ${target.unitCount} bài toán (gửi end-task). VPS biến mất khỏi danh sách; nếu VMS gửi lại add-task thì VPS được tạo lại.`}
          confirmLabel="Gỡ VPS"
          destructive
          pending={remove.isPending}
          error={remove.error?.message}
          onClose={() => { remove.reset(); setTarget(null); }}
          onConfirm={() => remove.mutate(target.id, { onSuccess: () => setTarget(null) })}
        />
      )}
    </div>
  );
}