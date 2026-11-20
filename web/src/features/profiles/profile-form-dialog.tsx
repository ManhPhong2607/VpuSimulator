"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { useCreateProfile } from "./hooks";
import {
  NEW_DRAFT,
  parseDraft,
  validateName,
  type ProfileDraft,
} from "./profile-model";

const labelCls = "mb-1.5 block text-xs text-muted-foreground";

export function ProfileFormDialog({
  existingNames,
  onClose,
}: {
  existingNames: string[];
  onClose: () => void;
}) {
  const create = useCreateProfile();
  const [name, setName] = useState("");
  const [d, setD] = useState<ProfileDraft>(NEW_DRAFT);
  const [error, setError] = useState<string>();

  const set = <K extends keyof ProfileDraft>(k: K, v: ProfileDraft[K]) =>
    setD((x) => ({ ...x, [k]: v }));

  function field(k: keyof ProfileDraft & string, label: string, placeholder?: string) {
    return (
      <div className="flex-1">
        <label htmlFor={`np-${k}`} className={labelCls}>{label}</label>
        <Input
          id={`np-${k}`}
          value={d[k] as string}
          onChange={(e) => set(k, e.target.value as never)}
          placeholder={placeholder}
          className="font-mono"
        />
      </div>
    );
  }

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(undefined);
    const nameErr = validateName(name, existingNames);
    if (nameErr) return setError(nameErr);
    const r = parseDraft(name.trim(), d);
    if (!r.value) return setError(r.error);
    create.mutate(r.value, { onSuccess: onClose, onError: (e) => setError(e.message) });
  }

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[400px]">
        <form onSubmit={submit} className="flex flex-col gap-3">
          <DialogHeader>
            <DialogTitle>New profile</DialogTitle>
            <DialogDescription className="sr-only">
              Tạo profile mới với interval, jitter, min_gap và ramp.
            </DialogDescription>
          </DialogHeader>

          <div>
            <label htmlFor="np-name" className={labelCls}>Tên profile</label>
            <Input id="np-name" value={name} onChange={(e) => setName(e.target.value)} autoFocus />
          </div>
          <div className="flex gap-2">
            {field("interval", "interval (s)")}
            {field("minGap", "min_gap (s)")}
          </div>
          <div className="flex gap-2">
            {field("jitterMin", "jitter_min (s)")}
            {field("jitterMax", "jitter_max (s)")}
          </div>
          <div className="flex gap-4">
            <label className="flex items-center gap-1.5">
              <input type="checkbox" checked={d.bypassFrames}
                onChange={(e) => set("bypassFrames", e.target.checked)} className="size-4 accent-primary" />
              bypass_frames
            </label>
            <label className="flex items-center gap-1.5">
              <input type="checkbox" checked={d.mergeTimestamp}
                onChange={(e) => set("mergeTimestamp", e.target.checked)} className="size-4 accent-primary" />
              gộp timestamp
            </label>
          </div>
          <div className="flex gap-2">
            {field("rampFrom", "ramp_from (eps)")}
            {field("rampTo", "ramp_to (eps)")}
            {field("rampDuration", "ramp_duration (s)")}
          </div>

          <p className="text-[11.5px] leading-normal text-[#8b8f9a]">
            Quy tắc kiểm tra: interval &gt; 0 · 0 ≤ jitter_min ≤ jitter_max ·
            min_gap ≥ 0 · ramp_from, ramp_to &gt; 0 khi có ramp_duration. Lưu
            gửi đủ mọi trường. Cần endpoint Control API mới (bạn A).
          </p>

          {error && (
            <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
              {error}
            </div>
          )}

          <DialogFooter>
            <Button type="button" variant="outline" onClick={onClose} disabled={create.isPending}>Huỷ</Button>
            <Button type="submit" disabled={create.isPending}>
              {create.isPending ? "Đang tạo…" : "Tạo profile"}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}