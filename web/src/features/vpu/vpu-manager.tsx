"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import type { Vpu } from "@/lib/api";
import { useVpus } from "./hooks";
import { VpuFormDialog } from "./vpu-form-dialog";
import { VpuTable } from "./vpu-table";

export function VpuManager() {
  const { data, isLoading, isError, refetch } = useVpus();
  const [form, setForm] = useState<{ vpu?: Vpu } | null>(null);
  const totalEps = data?.reduce((sum, v) => sum + v.eps, 0) ?? 0;

  return (
    <>
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-lg font-bold">VPU manager</h1>
          <p className="mt-0.5 text-xs text-muted-foreground">
            Bấm một VPU để xem VPS của nó ngay bên dưới. Bấm “Bài toán →” ở một
            VPS để mở màn chi tiết.
          </p>
        </div>
        <div className="flex items-center gap-4">
          <div className="text-right">
            <div className="text-[11px] text-muted-foreground">Tổng eps/s</div>
            <b className="text-xl">{totalEps}</b>
          </div>
          <Button onClick={() => setForm({})}>+ Add VPU</Button>
        </div>
      </div>

      {isLoading && <p className="text-muted-foreground">Đang tải…</p>}
      {isError && (
        <div role="alert" className="flex items-center gap-3 rounded-md bg-danger-soft px-3 py-2 text-danger">
          Không tải được danh sách VPU.
          <Button size="sm" variant="outline" onClick={() => refetch()}>
            Thử lại
          </Button>
        </div>
      )}
      {data && <VpuTable vpus={data} onEdit={(vpu) => setForm({ vpu })} />}

      {form && <VpuFormDialog vpu={form.vpu} onClose={() => setForm(null)} />}
    </>
  );
}