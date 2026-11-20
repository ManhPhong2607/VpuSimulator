"use client";

import { Fragment, useState } from "react";
import { ConfirmDialog } from "@/components/common/confirm-dialog";
import { Pill } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ProfileDef } from "@/lib/api";
import {
  useDeleteProfile,
  useResetProfile,
  useUpdateProfile,
} from "./hooks";
import { parseDraft, toDraft, type ProfileDraft } from "./profile-model";

const COLS = [
  "Tên", "interval (s)", "jitter_min (s)", "jitter_max (s)", "min_gap (s)",
  "bypass_frames", "gộp timestamp", "ramp_from (eps)", "ramp_to (eps)",
  "ramp_duration (s)", "",
];

type Ask = { kind: "delete" | "reset"; def: ProfileDef };

export function ProfileTable({ profiles }: { profiles: ProfileDef[] }) {
  const [ask, setAsk] = useState<Ask | null>(null);
  const del = useDeleteProfile();
  const reset = useResetProfile();
  const active = ask?.kind === "reset" ? reset : del;

  function close() {
    del.reset();
    reset.reset();
    setAsk(null);
  }

  return (
    <>
      <table className="w-full border-collapse">
        <thead>
          <tr>{COLS.map((h, i) => <th key={i} className={th}>{h}</th>)}</tr>
        </thead>
        <tbody className="font-mono">
          {profiles.map((p) => (
            // key chứa giá trị trên server: khi dữ liệu đổi (lưu, khôi phục) hàng được dựng lại từ giá trị mới
            <ProfileRow
              key={p.name + JSON.stringify(toDraft(p))}
              def={p}
              onAsk={(kind) => setAsk({ kind, def: p })}
            />
          ))}
        </tbody>
      </table>

      {ask && (
        <ConfirmDialog
          title={
            ask.kind === "delete"
              ? `Xoá profile ${ask.def.name}?`
              : `Khôi phục mặc định profile ${ask.def.name}?`
          }
          description={
            ask.kind === "delete"
              ? "Profile bị xoá khỏi hệ thống. Không hoàn tác."
              : `Mọi giá trị của ${ask.def.name} trở về mặc định của hệ thống. Chỉnh sửa hiện tại sẽ mất.`
          }
          confirmLabel={ask.kind === "delete" ? "Xoá" : "Khôi phục"}
          destructive
          pending={active.isPending}
          error={active.error?.message}
          onClose={close}
          onConfirm={() => active.mutate(ask.def.name, { onSuccess: close })}
        />
      )}
    </>
  );
}

function ProfileRow({
  def,
  onAsk,
}: {
  def: ProfileDef;
  onAsk: (kind: "delete" | "reset") => void;
}) {
  const base = toDraft(def);
  const [d, setD] = useState<ProfileDraft>(base);
  const [error, setError] = useState<string>();
  const update = useUpdateProfile();
  const dirty = JSON.stringify(d) !== JSON.stringify(base);

  const set = <K extends keyof ProfileDraft>(k: K, v: ProfileDraft[K]) =>
    setD((x) => ({ ...x, [k]: v }));

  function save() {
    setError(undefined);
    const r = parseDraft(def.name, d);
    if (!r.value) return setError(r.error);
    // Gửi ĐỦ mọi trường (FR-CFG-9)
    update.mutate(
      { name: def.name, input: r.value },
      { onError: (e) => setError(e.message) },
    );
  }

  const numCell = (k: keyof ProfileDraft & string, label: string, placeholder?: string) => (
    <td className={td}>
      <Input
        value={d[k] as string}
        onChange={(e) => set(k, e.target.value as never)}
        placeholder={placeholder}
        aria-label={`${label} ${def.name}`}
        className="h-7 w-16 px-1.5 font-mono text-xs"
      />
    </td>
  );
  const boxCell = (k: "bypassFrames" | "mergeTimestamp", label: string) => (
    <td className={td}>
      <input
        type="checkbox"
        checked={d[k]}
        onChange={(e) => set(k, e.target.checked)}
        aria-label={`${label} ${def.name}`}
        className="size-4 accent-primary"
      />
    </td>
  );

  return (
    <Fragment>
      <tr>
        <td className={td}>
          {def.name}{" "}
          {def.isBuiltin ? (
            <Pill tone="muted">mặc định</Pill>
          ) : (
            <Pill tone="warn">tuỳ chỉnh</Pill>
          )}
        </td>
        {numCell("interval", "interval")}
        {numCell("jitterMin", "jitter_min")}
        {numCell("jitterMax", "jitter_max")}
        {numCell("minGap", "min_gap")}
        {boxCell("bypassFrames", "bypass_frames")}
        {boxCell("mergeTimestamp", "gộp timestamp")}
        {numCell("rampFrom", "ramp_from", "—")}
        {numCell("rampTo", "ramp_to", "—")}
        {numCell("rampDuration", "ramp_duration", "—")}
        <td className={`${td} whitespace-nowrap text-right font-sans`}>
          <Button size="sm" onClick={save} disabled={!dirty || update.isPending}>
            {update.isPending ? "Đang lưu…" : "Lưu"}
          </Button>{" "}
          {def.isBuiltin ? (
            <Button size="sm" variant="outline" onClick={() => onAsk("reset")}>
              Khôi phục mặc định
            </Button>
          ) : (
            <Button
              size="sm"
              variant="outline"
              onClick={() => onAsk("delete")}
              className="border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
            >
              Xoá
            </Button>
          )}
        </td>
      </tr>
      {error && (
        <tr>
          <td colSpan={COLS.length} className="border-b bg-danger-soft px-3.5 py-2 font-sans text-xs text-danger">
            {def.name}: {error}
          </td>
        </tr>
      )}
    </Fragment>
  );
}