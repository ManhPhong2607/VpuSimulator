"use client";

import { useState } from "react";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { Button } from "@/components/ui/button";
import { timeAgoEn } from "@/lib/format";
import { card, ctd, cth } from "./styles";
import { useClearCorrelation, useCorrelation, useDeleteCorrelation } from "./hooks";
import { cn } from "@/lib/utils";

const group = (n: number) => String(n).replace(/\B(?=(\d{3})+(?!\d))/g, " ");

export function CorrelationCard() {
  const { data, isLoading, isError, refetch } = useCorrelation();
  const del = useDeleteCorrelation();
  const clear = useClearCorrelation();
  const [confirm, setConfirm] = useState(false);

  function closeConfirm() {
    clear.reset();
    setConfirm(false);
  }

  return (
    <div className={cn(card, "col-span-2 gap-3")}>
      <div className="flex items-center justify-between">
        <div className="text-[13px] font-semibold">Correlation store</div>
        <span className="text-[11px] text-[#8b8f9a]">
          FR-COR-5 — view &amp; clear via Control API
        </span>
      </div>

      {isLoading && <p className="text-muted-foreground">Đang tải…</p>}
      {isError && (
        <div role="alert" className="flex items-center gap-3 text-danger">
          Không tải được correlation store.
          <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
        </div>
      )}

      {data && (
        <>
          <div className="flex gap-6 text-[12.5px]">
            <div><span className="text-muted-foreground">Entries</span> <span className="font-mono">{group(data.total)}</span></div>
            <div><span className="text-muted-foreground">TTL</span> <span className="font-mono">{data.ttl}</span></div>
            <div><span className="text-muted-foreground">Limit / identity</span> <span className="font-mono">{group(data.limitPerIdentity)}</span></div>
          </div>

          {del.error && (
            <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
              {del.error.message}
            </div>
          )}

          <table className="w-full border-collapse">
            <thead>
              <tr>
                {["Identity", "event_key", "label", "written"].map((h) => (
                  <th key={h} className={cth}>{h}</th>
                ))}
                <th className={cn(cth, "text-right")}></th>
              </tr>
            </thead>
            <tbody className="font-mono">
              {data.items.map((it) => (
                <tr key={it.id}>
                  <td className={ctd}>{it.identity}</td>
                  <td className={ctd}>{it.eventKey.slice(0, 8)}…</td>
                  <td className={ctd}>{it.label}</td>
                  <td className={ctd}>{timeAgoEn(it.writtenAt)}</td>
                  <td className={cn(ctd, "text-right")}>
                    <button
                      type="button"
                      onClick={() => del.mutate(it.id)}
                      disabled={del.isPending}
                      aria-label={`Xoá entry ${it.eventKey.slice(0, 8)}`}
                      className="text-[11px] text-danger hover:underline disabled:opacity-50"
                    >
                      delete
                    </button>
                  </td>
                </tr>
              ))}
              {data.items.length === 0 && (
                <tr>
                  <td colSpan={5} className={cn(ctd, "text-center font-sans text-muted-foreground")}>
                    Không có entry nào.
                  </td>
                </tr>
              )}
            </tbody>
          </table>
          {data.total > data.items.length && (
            <p className="text-[11.5px] text-[#8b8f9a]">
              Hiện {data.items.length} / {group(data.total)} entry mới nhất.
            </p>
          )}

          <Button
            variant="outline"
            size="sm"
            disabled={data.total === 0}
            onClick={() => setConfirm(true)}
            className="self-start border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
          >
            Clear all correlation (confirm=true)
          </Button>
        </>
      )}

      {confirm && data && (
        <ConfirmDialog
          title="Xoá toàn bộ correlation?"
          description={`Xoá toàn bộ ${group(data.total)} entry trong correlation store (gọi API với confirm=true). Không hoàn tác.`}
          confirmLabel="Xoá tất cả"
          destructive
          pending={clear.isPending}
          error={clear.error?.message}
          onClose={closeConfirm}
          onConfirm={() => clear.mutate(undefined, { onSuccess: closeConfirm })}
        />
      )}
    </div>
  );
}