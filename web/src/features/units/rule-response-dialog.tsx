"use client";

import { useState } from "react";
import { Pill } from "@/components/common/status-badge";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { formatClock } from "@/lib/format";
import type { Unit } from "@/lib/api";
import { useLastResponse } from "./hooks";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-2.5">
      <span className="w-[110px] shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-all font-mono">{children}</span>
    </div>
  );
}

export function RuleResponseDialog({
  unit,
  onClose,
}: {
  unit: Unit;
  onClose: () => void;
}) {
  const { data, isLoading, isError } = useLastResponse(unit.id);
  const [copied, setCopied] = useState(false);
  const json = data ? JSON.stringify(data.payload, null, 2) : "";

  async function copy() {
    try {
      await navigator.clipboard.writeText(json);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* trình duyệt chặn clipboard: bỏ qua */
    }
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-[480px]">
        <DialogHeader>
          <div className="flex items-center justify-between pr-6">
            <DialogTitle>Response · {unit.name}</DialogTitle>
            {data?.assumed && <Pill tone="warn">giả định</Pill>}
          </div>
          <DialogDescription className="sr-only">
            Bản tin gần nhất của bài toán {unit.name}.
          </DialogDescription>
        </DialogHeader>

        {isLoading && <p className="text-muted-foreground">Đang tải…</p>}
        {isError && <p className="text-danger">Không tải được Response.</p>}
        {data === null && (
          <p className="rounded-md bg-muted px-3 py-2 text-xs text-muted-foreground">
            Chưa có payload. Cần bật verbose (xem Config) và chờ bài toán phát
            bản tin.
          </p>
        )}

        {data && (
          <>
            <div className="flex flex-col gap-1.5 text-xs">
              <Field label="Phát lúc">{formatClock(data.emittedAt)}</Field>
              <Field label="Routing key">{data.routingKey}</Field>
              <Field label="Loại · kích thước">{data.kind} · {data.sizeBytes} B</Field>
            </div>
            <pre className="max-h-[320px] overflow-auto rounded-lg border bg-[#fafbfc] px-3.5 py-3 font-mono text-[11.5px] leading-relaxed">
              {json}
            </pre>
            <p className="text-[11.5px] leading-normal text-[#8b8f9a]">
              Bản tin gần nhất của bài toán này. Cần bật verbose để có payload
              (xem Config). Ý nghĩa nút Response đang chờ xác nhận.
            </p>
          </>
        )}

        <DialogFooter>
          {data && (
            <Button variant="outline" onClick={copy}>
              {copied ? "Đã copy" : "Copy JSON"}
            </Button>
          )}
          <Button onClick={onClose}>Đóng</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}