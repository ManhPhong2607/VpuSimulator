"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { useRuntimeSettings } from "@/features/system/hooks";
import type { RuntimeSettings } from "@/lib/api";
import { useUpdateRuntimeSettings } from "./hooks";

const LEVELS: RuntimeSettings["logLevel"][] = ["info", "debug", "warn", "error"];

export function RuntimeCard() {
  const { data, isLoading, isError, refetch } = useRuntimeSettings();

  return (
    <div className="col-span-2 rounded-[10px] border bg-card">
      <div className="flex justify-between border-b px-4 py-3 font-semibold">
        <span>Chỉnh được lúc runtime</span>
        {/* TODO(API-A): hai endpoint ghi này lấy từ thiết kế; xác nhận lại với bạn A. */}
        <span className="font-normal text-[#8b8f9a]">
          POST /api/v1/logs/level · /logs/verbose
        </span>
      </div>

      {isLoading && <p className="p-4 text-muted-foreground">Đang tải…</p>}
      {isError && (
        <div role="alert" className="flex items-center gap-3 p-4 text-danger">
          Không đọc được thiết lập hiện tại.
          <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
        </div>
      )}
      {data && <RuntimeForm settings={data} />}
    </div>
  );
}

function RuntimeForm({ settings }: { settings: RuntimeSettings }) {
  const update = useUpdateRuntimeSettings();
  const [level, setLevel] = useState(settings.logLevel);
  const [verbose, setVerbose] = useState(settings.verbose);
  const [saved, setSaved] = useState(false);
  const dirty = level !== settings.logLevel || verbose !== settings.verbose;

  function apply() {
    setSaved(false);
    update.mutate({ logLevel: level, verbose }, { onSuccess: () => setSaved(true) });
  }

  return (
    <div className="flex items-end gap-6 p-4">
      <div>
        <label htmlFor="log-level" className="mb-1.5 block text-xs text-muted-foreground">
          Log level
        </label>
        <select
          id="log-level"
          value={level}
          onChange={(e) => { setLevel(e.target.value as RuntimeSettings["logLevel"]); setSaved(false); }}
          className="h-[34px] w-[140px] rounded-md border border-input bg-background px-2.5 text-sm"
        >
          {LEVELS.map((l) => <option key={l} value={l}>{l}</option>)}
        </select>
      </div>

      <div>
        <span className="mb-1.5 block text-xs text-muted-foreground">
          Log từng bản tin gửi kèm payload (verbose) · cần để xem JSON ở Live log
        </span>
        <label className="flex h-[34px] items-center gap-2">
          <input
            type="checkbox"
            checked={verbose}
            onChange={(e) => { setVerbose(e.target.checked); setSaved(false); }}
            aria-label="verbose"
            className="size-4 accent-primary"
          />
          <span>Bật</span>
        </label>
      </div>

      <Button className="h-[34px]" disabled={!dirty || update.isPending} onClick={apply}>
        {update.isPending ? "Đang áp dụng…" : "Áp dụng"}
      </Button>

      {saved && <span role="status" className="pb-2 text-xs text-success">Đã áp dụng.</span>}
      {update.error && (
        <span role="alert" className="pb-2 text-xs text-danger">{update.error.message}</span>
      )}
    </div>
  );
}