"use client";

import { useState } from "react";
import { Pill } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { useSetHeartbeat, useVpus } from "@/features/vpu/hooks";
import { timeAgo } from "@/lib/format";
import { useBulkHeartbeat, useSetHeartbeatInterval, type BulkAction } from "./hooks";
import { DEFAULT_INTERVAL_SEC, parseInterval } from "./heartbeat-model";

type Msg = { tone: "ok" | "err"; text: string };

export function HeartbeatView() {
  const { data, isLoading, isError, refetch } = useVpus();
  const toggle = useSetHeartbeat();
  const setSec = useSetHeartbeatInterval();
  const bulk = useBulkHeartbeat();

  const [selected, setSelected] = useState<Set<string>>(new Set());
  const [bulkSec, setBulkSec] = useState(String(DEFAULT_INTERVAL_SEC));
  const [rowError, setRowError] = useState<string>();
  const [msg, setMsg] = useState<Msg>();

  const vpus = data ?? [];
  // chỉ tính các VPU còn tồn tại (có thể đã bị gỡ sau khi chọn)
  const selectedIds = vpus.filter((v) => selected.has(v.id)).map((v) => v.id);
  const nameOf = (id: string) => {
    const v = vpus.find((x) => x.id === id);
    return v ? v.displayName || v.appId : id;
  };

  function toggleSelect(id: string, on: boolean) {
    setMsg(undefined);
    setSelected((prev) => {
      const next = new Set(prev);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  function runBulk(action: BulkAction, okText: string) {
    if (selectedIds.length === 0) return;
    setMsg(undefined);
    bulk.mutate(
      { ids: selectedIds, action },
      {
        onSuccess: ({ total, failed }) =>
          setMsg(
            failed.length === 0
              ? { tone: "ok", text: `${okText} (${total} VPU).` }
              : {
                  tone: "err",
                  text: `${failed.length}/${total} VPU không đổi được: ${failed.map(nameOf).join(", ")}.`,
                },
          ),
      },
    );
  }

  function applyInterval() {
    const r = parseInterval(bulkSec);
    if (r.sec === undefined) return setMsg({ tone: "err", text: r.error! });
    setBulkSec(String(r.sec));
    runBulk({ kind: "interval", sec: r.sec }, `Đã đặt chu kỳ ${r.sec}s`);
  }

  const err = toggle.error ?? setSec.error;
  const busy = bulk.isPending || selectedIds.length === 0;

  return (
    <div className="flex min-h-0 flex-1 items-stretch gap-4">
      <div className="flex flex-[2.2] flex-col overflow-hidden rounded-[10px] border bg-card">
        <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
          <span>Heartbeat theo từng VPU</span>
          <span className="font-normal text-[#8b8f9a]">chu kỳ riêng cho mỗi app_id</span>
        </div>

        {(err || rowError) && (
          <div role="alert" className="flex items-center justify-between bg-danger-soft px-4 py-2 text-xs text-danger">
            <span>{rowError ?? err?.message}</span>
            <button
              type="button"
              className="underline"
              onClick={() => { setRowError(undefined); toggle.reset(); setSec.reset(); }}
            >
              Đóng
            </button>
          </div>
        )}

        {isLoading && <p className="p-4 text-muted-foreground">Đang tải…</p>}
        {isError && (
          <div role="alert" className="flex items-center gap-3 p-4 text-danger">
            Không tải được danh sách VPU.
            <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
          </div>
        )}

        {data && (
          <table className="w-full border-collapse">
            <thead>
              <tr>
                <th className={th}></th>
                {["VPU (app_id)", "Bật", "Chu kỳ (s)", "Gửi gần nhất", "Trạng thái"].map((h) => (
                  <th key={h} className={th}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody className="font-mono">
              {vpus.map((v) => {
                const name = v.displayName || v.appId;
                return (
                  <tr key={v.id}>
                    <td className={td}>
                      <input
                        type="checkbox"
                        checked={selected.has(v.id)}
                        onChange={(e) => toggleSelect(v.id, e.target.checked)}
                        aria-label={`select ${name}`}
                        className="size-4 accent-primary"
                      />
                    </td>
                    <td className={td}>
                      {name} <span className="text-[#8b8f9a]">{v.appId}</span>
                    </td>
                    <td className={td}>
                      <input
                        type="checkbox"
                        checked={v.heartbeat.enabled}
                        onChange={(e) => toggle.mutate({ id: v.id, enabled: e.target.checked })}
                        aria-label={`enable ${name}`}
                        className="size-4 accent-primary"
                      />
                    </td>
                    <td className={td}>
                      {/* key chứa giá trị trên server: khi đổi (lưu, áp hàng loạt, rollback) ô được dựng lại */}
                      <IntervalInput
                        key={`${v.id}:${v.heartbeat.intervalSec}`}
                        label={name}
                        value={v.heartbeat.intervalSec}
                        onError={setRowError}
                        onCommit={(sec) => setSec.mutate({ id: v.id, sec })}
                      />
                    </td>
                    <td className={td}>{timeAgo(v.heartbeat.lastAt)}</td>
                    <td className={td}>
                      {v.heartbeat.enabled ? (
                        <Pill tone="ok">ok</Pill>
                      ) : (
                        <Pill tone="muted">tắt</Pill>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <div className="mt-auto flex flex-wrap items-center gap-2.5 border-t bg-[#fafbfc] px-4 py-3.5">
          <span className="text-muted-foreground">Đã chọn {selectedIds.length} VPU</span>
          <Input
            value={bulkSec}
            onChange={(e) => setBulkSec(e.target.value)}
            aria-label="interval"
            inputMode="numeric"
            className="w-20"
          />
          <span className="text-muted-foreground">giây</span>
          <Button size="sm" disabled={busy} onClick={applyInterval}>
            Áp dụng chu kỳ
          </Button>
          {/* "ngoài SRS": thao tác bật/tắt chưa có trong SRS, cần endpoint mới (bạn A) */}
          <Button size="sm" variant="outline" disabled={busy}
            onClick={() => runBulk({ kind: "enabled", enabled: true }, "Đã bật heartbeat")}>
            Bật (ngoài SRS)
          </Button>
          <Button size="sm" variant="outline" disabled={busy}
            onClick={() => runBulk({ kind: "enabled", enabled: false }, "Đã tắt heartbeat")}>
            Tắt (ngoài SRS)
          </Button>
          {msg && (
            <span role="status" className={msg.tone === "ok" ? "text-xs text-success" : "text-xs text-danger"}>
              {msg.text}
            </span>
          )}
        </div>
      </div>

      <div className="flex-1 self-start overflow-hidden rounded-[10px] border bg-card">
        <div className="border-b px-4 py-3 font-semibold">Quy tắc heartbeat</div>
        <div className="flex flex-col gap-2.5 p-4 text-[12.5px] leading-[1.55]">
          <div>Mỗi VPU (app_id) có vòng heartbeat riêng, chu kỳ riêng.</div>
          <div>Mặc định 30s. Giá trị ≤ 0 được coi là 30s.</div>
          <div>
            Bản tin: <span className="font-mono">process-unit</span> /{" "}
            <span className="font-mono">heartbeat</span>, gửi tới{" "}
            <span className="font-mono">inbound.status.vpu.&lt;app_id&gt;</span>.
          </div>
          <div>Gỡ VPU thì dừng heartbeat và đóng session broker.</div>
          <div className="text-warning">
            Lưu ý: SRS chỉ cho đặt chu kỳ lúc tạo VPU và không có thao tác tắt
            heartbeat. Sửa chu kỳ, Bật và Tắt trên màn này cần endpoint mới (bạn
            A thêm).
          </div>
        </div>
      </div>
    </div>
  );
}

/** Ô chu kỳ: sửa trực tiếp, lưu khi rời ô hoặc nhấn Enter. */
function IntervalInput({
  label,
  value,
  onCommit,
  onError,
}: {
  label: string;
  value: number;
  onCommit: (sec: number) => void;
  onError: (message?: string) => void;
}) {
  const [text, setText] = useState(String(value));

  function commit() {
    const r = parseInterval(text);
    if (r.sec === undefined) {
      onError(`${label}: ${r.error}`);
      setText(String(value));
      return;
    }
    onError(undefined);
    setText(String(r.sec));
    if (r.sec !== value) onCommit(r.sec);
  }

  return (
    <Input
      value={text}
      onChange={(e) => setText(e.target.value)}
      onBlur={commit}
      onKeyDown={(e) => e.key === "Enter" && e.currentTarget.blur()}
      aria-label={`interval ${label}`}
      inputMode="numeric"
      className="h-7 w-[70px] px-2 font-mono text-xs"
    />
  );
}