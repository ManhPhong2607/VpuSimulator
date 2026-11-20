"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { ApplyProfileCard } from "./apply-profile-card";
import { EpsByVpuCard } from "./eps-by-vpu-card";
import { useProfiles } from "./hooks";
import { ProfileFormDialog } from "./profile-form-dialog";
import { ProfileTable } from "./profile-table";

export function ProfilesView() {
  const { data, isLoading, isError, refetch } = useProfiles();
  const [creating, setCreating] = useState(false);
  const systemDefault = data?.find((p) => p.isSystemDefault);

  return (
    <>
      <div className="flex items-center gap-2.5">
        <span className="text-muted-foreground">Mặc định hệ thống</span>
        {systemDefault && (
          <span className="rounded-full bg-muted px-2 py-0.5 font-mono text-[11px] text-muted-foreground">
            {systemDefault.name}
          </span>
        )}
        <span className="text-xs text-[#8b8f9a]">
          · thứ tự kế thừa: unit → VPU → mặc định hệ thống
        </span>
      </div>

      <div className="overflow-hidden rounded-[10px] border bg-card">
        <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
          <span>
            Định nghĩa profile{" "}
            <span className="font-normal text-[#8b8f9a]">
              · sửa trực tiếp trong ô rồi bấm Lưu
            </span>
          </span>
          <Button size="sm" onClick={() => setCreating(true)}>+ New profile</Button>
        </div>

        {isLoading && <p className="p-4 text-muted-foreground">Đang tải…</p>}
        {isError && (
          <div role="alert" className="flex items-center gap-3 p-4 text-danger">
            Không tải được danh sách profile.
            <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
          </div>
        )}
        {data && <ProfileTable profiles={data} />}

        <p className="px-4 py-2.5 text-[11.5px] leading-relaxed text-[#8b8f9a]">
          Quy tắc kiểm tra: interval &gt; 0 · 0 ≤ jitter_min ≤ jitter_max ·
          min_gap ≥ 0 · ramp_from, ramp_to &gt; 0 khi có ramp_duration.
          <br />
          storm: tick 0,5s và min_gap 3s là chủ ý (CON-2), sửa được nhưng có nút
          Khôi phục mặc định. Lưu gửi đủ mọi trường (tránh lỗi FR-CFG-9: ghi đè
          một phần làm trường còn lại về 0). Cần endpoint Control API mới, thống
          nhất với bạn A.
        </p>
      </div>

      {data && (
        <div className="flex items-start gap-4">
          <ApplyProfileCard profiles={data} />
          <EpsByVpuCard />
        </div>
      )}

      {creating && (
        <ProfileFormDialog
          existingNames={data?.map((p) => p.name) ?? []}
          onClose={() => setCreating(false)}
        />
      )}
    </>
  );
}