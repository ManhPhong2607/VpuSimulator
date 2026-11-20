"use client";

import Link from "next/link";
import { useState } from "react";
import { Button } from "@/components/ui/button";
import { formatClock } from "@/lib/format";
import type { LogEntry } from "@/lib/api";
import { MAX_BODY_LOG } from "./log-model";

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex gap-2.5">
      <span className="w-[84px] shrink-0 text-muted-foreground">{label}</span>
      <span className="min-w-0 break-all font-mono">{children}</span>
    </div>
  );
}

export function MessageDetail({ entry }: { entry: LogEntry | null }) {
  const [copied, setCopied] = useState(false);
  const m = entry?.message;
  const text =
    m && m.payload != null
      ? typeof m.payload === "string"
        ? m.payload
        : JSON.stringify(m.payload, null, 2)
      : "";

  async function copy() {
    try {
      await navigator.clipboard.writeText(text);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      /* trình duyệt chặn clipboard: bỏ qua */
    }
  }

  const body = !entry
    ? "Chọn một dòng ở bên trái để xem chi tiết bản tin."
    : !m
      ? "Dòng này không có bản tin đi kèm."
      : m.payload == null
        ? "Không có payload (verbose đang tắt khi bản tin này được gửi)."
        : text;

  return (
    <div className="flex min-w-0 flex-1 flex-col overflow-hidden rounded-[10px] border bg-card">
      <div className="flex items-center justify-between border-b px-4 py-3">
        <b>Chi tiết bản tin</b>
        <Button size="sm" variant="outline" disabled={!text} onClick={copy}>
          {copied ? "Đã copy" : "Copy JSON"}
        </Button>
      </div>

      {entry && (
        <div className="flex flex-col gap-1.5 border-b border-[#eef0f2] px-4 py-3 text-xs">
          <Field label="Sự kiện">
            {m ? m.event : entry.outcome} · {formatClock(entry.at)}
          </Field>
          {m && (
            <>
              <Field label="Routing key">{m.routingKey}</Field>
              <Field label="Loại · kích thước">
                {m.kind} · {m.sizeBytes} B{m.truncated && " · đã cắt"}
              </Field>
            </>
          )}
        </div>
      )}

      <pre className="m-0 flex-1 overflow-auto whitespace-pre-wrap break-all bg-[#fafbfc] px-4 py-3.5 font-mono text-[11.5px] leading-[1.6]">
        {body}
      </pre>

      <div className="border-t border-[#eef0f2] px-4 py-2.5 text-[11.5px] leading-normal text-[#8b8f9a]">
        Payload chỉ có khi verbose bật và bị cắt ở {MAX_BODY_LOG} ký tự
        (max_body_log). Khi verbose tắt chỉ còn routing key, loại và kích thước.{" "}
        <Link href="/config" className="text-primary">
          Mở cấu hình verbose
        </Link>
      </div>
    </div>
  );
}