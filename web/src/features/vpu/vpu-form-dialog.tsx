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
import type { Vpu } from "@/lib/api";
import { useCreateVpu, useProfileNames, useUpdateVpu } from "./hooks";

const labelCls = "mb-1.5 block text-xs text-muted-foreground";

export function VpuFormDialog({
  vpu,
  onClose,
}: {
  vpu?: Vpu; // có = Edit, không = Add
  onClose: () => void;
}) {
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[400px]">
        <VpuForm vpu={vpu} onClose={onClose} />
      </DialogContent>
    </Dialog>
  );
}

function VpuForm({ vpu, onClose }: { vpu?: Vpu; onClose: () => void }) {
  const isEdit = !!vpu;
  const { data: profiles = [] } = useProfileNames();
  const create = useCreateVpu();
  const update = useUpdateVpu();

  const [displayName, setDisplayName] = useState(vpu?.displayName ?? "");
  const [appId, setAppId] = useState(vpu?.appId ?? "");
  const [profile, setProfile] = useState(vpu?.profile ?? "");
  const [hbOn, setHbOn] = useState(vpu?.heartbeat.enabled ?? true);
  const [hbSec, setHbSec] = useState(String(vpu?.heartbeat.intervalSec ?? 30));
  const [error, setError] = useState<string>();

  const pending = create.isPending || update.isPending;
  const fail = (e: unknown) =>
    setError(e instanceof Error ? e.message : "Không lưu được, thử lại.");

  function submit(e: React.FormEvent) {
    e.preventDefault();
    setError(undefined);

    const id = appId.trim();
    if (!isEdit && !/^[A-Za-z0-9][A-Za-z0-9_.-]*$/.test(id)) {
      return setError(
        "app_id chỉ gồm chữ, số, '-', '_', '.' và không có khoảng trắng.",
      );
    }
    const sec = Number(hbSec);
    if (hbOn && (!Number.isInteger(sec) || sec < 1)) {
      return setError("Chu kỳ heartbeat phải là số nguyên ≥ 1 (giây).");
    }
    const intervalSec = Number.isInteger(sec) && sec >= 1 ? sec : 30;

    if (isEdit) {
      update.mutate(
        {
          id: vpu.id,
          input: {
            displayName: displayName.trim(),
            profile: profile || undefined,
            heartbeatEnabled: hbOn,
            heartbeatIntervalSec: intervalSec,
            
          },
        },
        { onSuccess: onClose, onError: fail },
      );
    } else {
      create.mutate(
        {
          appId: id,
          displayName: displayName.trim() || undefined,
          profile: profile || undefined,
          heartbeatEnabled: hbOn,
          heartbeatIntervalSec: intervalSec,
          
        },
        { onSuccess: onClose, onError: fail },
      );
    }
  }

  return (
    <form onSubmit={submit} className="flex flex-col gap-3">
      <DialogHeader>
        <DialogTitle>{isEdit ? "Edit VPU" : "Add VPU"}</DialogTitle>
        <DialogDescription className="sr-only">
          Thông tin VPU: tên, app_id, profile, heartbeat.
        </DialogDescription>
      </DialogHeader>

      <div>
        <label htmlFor="v-name" className={labelCls}>Tên hiển thị (tuỳ chọn)</label>
        <Input id="v-name" value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
      </div>

      <div>
        <label htmlFor="v-appid" className={labelCls}>
          app_id (duy nhất toàn hệ thống{isEdit ? " · chỉ đọc khi Edit" : ""})
        </label>
        <Input
          id="v-appid"
          value={appId}
          onChange={(e) => setAppId(e.target.value)}
          readOnly={isEdit}
          required
          className="font-mono"
        />
      </div>

      <div>
        <label htmlFor="v-profile" className={labelCls}>Profile</label>
        <select
          id="v-profile"
          value={profile}
          onChange={(e) => setProfile(e.target.value)}
          className="h-9 w-full rounded-md border border-input bg-background px-2.5 text-sm"
        >
          <option value="">Mặc định hệ thống (normal)</option>
          {profiles.map((p) => (
            <option key={p} value={p}>{p}</option>
          ))}
        </select>
      </div>

      <div className="flex items-center gap-2.5">
        <input
          type="checkbox"
          checked={hbOn}
          onChange={(e) => setHbOn(e.target.checked)}
          aria-label="Bật heartbeat"
          className="size-4 accent-primary"
        />
        <span>Bật heartbeat, chu kỳ</span>
        <Input
          value={hbSec}
          onChange={(e) => setHbSec(e.target.value)}
          disabled={!hbOn}
          aria-label="Chu kỳ heartbeat (giây)"
          inputMode="numeric"
          className="h-8 w-16 font-mono"
        />
        <span>giây</span>
      </div>

      <p className="text-[11.5px] leading-normal text-[#8b8f9a]">
         Tạo VPU: kết nối broker, báo trạng thái running và bắt đầu heartbeat. VPS và
         bài toán do VMS tạo sau khi kích hoạt workspace. app_id trùng thì API trả 409.
      </p>

      {error && (
        <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {error}
        </div>
      )}

      <DialogFooter>
        <Button type="button" variant="outline" onClick={onClose} disabled={pending}>
          Huỷ
        </Button>
        <Button type="submit" disabled={pending}>
          {pending ? "Đang lưu…" : "Lưu"}
        </Button>
      </DialogFooter>
    </form>
  );
}