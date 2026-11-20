"use client";

import Link from "next/link";
import { useMemo, useState } from "react";
import { Pill } from "@/components/common/status-badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useRuntimeSettings } from "@/features/system/hooks";
import { useLiveLog } from "./hooks";
import { FILTER_OUTCOME, LOG_BUFFER, matches } from "./log-model";
import { LogTerminal } from "./log-terminal";
import { MessageDetail } from "./message-detail";

export function LiveLogView() {
  const log = useLiveLog();
  const settings = useRuntimeSettings();

  const [q, setQ] = useState("");
  const [outcome, setOutcome] = useState("");
  const [picked, setPicked] = useState<string | null>(null);
  const [firstId, setFirstId] = useState<string | null>(null);

  const entries = useMemo(
    () => (log.data ?? []).filter((e) => matches(e, q, outcome)),
    [log.data, q, outcome],
  );

  // Chọn sẵn dòng đầu tiên MỘT lần khi có dữ liệu (giống thiết kế). Sau đó giữ nguyên,
  // không nhảy theo dòng mới để người dùng đọc JSON không bị đổi giữa chừng.
  if (firstId === null && entries[0]) setFirstId(entries[0].id);

  const selectedId = picked ?? firstId;
  // Tìm trong toàn bộ bộ đệm để dòng đang xem vẫn hiện dù bị bộ lọc ẩn khỏi danh sách
  const selected = log.data?.find((e) => e.id === selectedId) ?? null;

  const verbose = settings.data?.verbose;

  return (
    <>
      <div className="flex items-center justify-between">
        <div>
          <div className="text-base font-semibold">Live log</div>
          <div className="text-xs text-muted-foreground">
            In-memory buffer, last {LOG_BUFFER} lines (FR-OBS-4)
          </div>
        </div>
        <div className="flex items-center gap-2">
          {verbose === undefined ? (
            <Pill tone="muted">verbose: …</Pill>
          ) : verbose ? (
            <Pill tone="ok">verbose: bật</Pill>
          ) : (
            <Pill tone="muted">verbose: tắt</Pill>
          )}
          <Button asChild size="sm" variant="outline">
            <Link href="/config">Đổi</Link>
          </Button>
          <Input
            value={q}
            onChange={(e) => setQ(e.target.value)}
            placeholder="Filter by identity, task or rule…"
            aria-label="Filter log"
            className="h-8 w-[280px] text-[12.5px]"
          />
          <select
            value={outcome}
            onChange={(e) => setOutcome(e.target.value)}
            aria-label="Outcome"
            className="h-8 rounded-md border border-input bg-background px-2.5 text-[12.5px]"
          >
            <option value="">All outcomes</option>
            {FILTER_OUTCOME.map((o) => (
              <option key={o} value={o}>{o}</option>
            ))}
          </select>
        </div>
      </div>

      <div className="flex min-h-0 flex-1 gap-4">
        <LogTerminal
          entries={entries}
          selectedId={selectedId}
          onSelect={setPicked}
          loading={log.isLoading}
          error={log.isError}
          onRetry={() => log.refetch()}
          filtering={!!q.trim() || !!outcome}
        />
        <MessageDetail entry={selected} />
      </div>
    </>
  );
}