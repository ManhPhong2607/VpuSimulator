"use client";

import { useState } from "react";
import { Pill } from "@/components/common/status-badge";
import { Button } from "@/components/ui/button";
import { formatHm } from "@/lib/format";
import { useLoadedScenario, useRemoveScenario } from "./hooks";

export function LoadedScenarioCard() {
  const { data, isLoading, isError } = useLoadedScenario();
  const remove = useRemoveScenario();
  const [confirming, setConfirming] = useState(false);

  function close() {
    remove.reset();
    setConfirming(false);
  }

  return (
    <>
      <div className="flex items-center justify-between rounded-[10px] border bg-card px-4 py-3.5">
        {isLoading && <span className="text-muted-foreground">Đang tải…</span>}
        {isError && <span className="text-danger">Không tải được scenario đang nạp.</span>}

        {!isLoading && !isError && !data && (
          <div>
            <div className="text-xs text-muted-foreground">Scenario đang nạp</div>
            <div className="text-muted-foreground">Chưa nạp scenario nào</div>
          </div>
        )}

        {data && (
          <>
            <div className="flex items-center gap-4">
              <div>
                <div className="text-xs text-muted-foreground">Scenario đang nạp</div>
                <div className="font-mono font-semibold">{data.name}</div>
              </div>
              {data.running ? (
                <Pill tone="ok">đang chạy</Pill>
              ) : (
                <Pill tone="muted">đã dừng</Pill>
              )}
              <span className="text-xs text-muted-foreground">
                {data.vpuCount} VPU · {data.vpsCount} VPS · {data.taskCount} task ·{" "}
                {data.ruleCount} rule
                {data.seed !== null && <> · seed {data.seed}</>} · nạp lúc{" "}
                {formatHm(data.loadedAt)}
              </span>
            </div>
            <Button
              variant="outline"
              onClick={() => setConfirming(true)}
              className="border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
            >
              Gỡ scenario
            </Button>
          </>
        )}
      </div>

      {data && confirming && (
        <div
          role="alertdialog"
          aria-label="Xác nhận gỡ scenario"
          className="flex items-center justify-between rounded-lg border border-[#f2b8b8] bg-[#fdf1f1] px-3.5 py-2.5"
        >
          <span className="text-[#8a1f1f]">
            {remove.error
              ? remove.error.message
              : "Gỡ scenario đang nạp? Không hoàn tác (gọi API DELETE /scenario với confirm=true)."}
          </span>
          <div className="flex gap-2">
            <Button
              size="sm"
              variant="outline"
              disabled={remove.isPending}
              onClick={() => remove.mutate(undefined, { onSuccess: close })}
              className="border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
            >
              {remove.isPending ? "Đang gỡ…" : "Xác nhận gỡ"}
            </Button>
            <Button size="sm" variant="outline" onClick={close}>
              Huỷ
            </Button>
          </div>
        </div>
      )}
    </>
  );
}