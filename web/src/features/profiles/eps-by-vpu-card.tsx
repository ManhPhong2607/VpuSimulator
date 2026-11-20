"use client";

import { Pill } from "@/components/common/status-badge";
import { td, th } from "@/components/common/table-styles";
import { useEpsByVpu } from "./hooks";

const val = "bg-[#fafbfc]";

export function EpsByVpuCard() {
  const { data, isLoading, isError } = useEpsByVpu();
  const rows = data ?? [];
  const totalActual = rows.reduce((sum, r) => sum + r.actualEps, 0);
  const targets = rows.map((r) => r.targetEps).filter((n): n is number => n !== null);
  const totalTarget = targets.length ? targets.reduce((a, b) => a + b, 0) : null;

  return (
    <div className="flex-[1.3] self-start overflow-hidden rounded-[10px] border bg-card">
      <div className="flex items-center justify-between border-b px-4 py-3 font-semibold">
        <span>eps theo VPU</span>
        <Pill tone="ok">● live · chỉ đọc</Pill>
      </div>

      {isLoading && <p className="p-4 text-muted-foreground">Đang tải…</p>}
      {isError && <p className="p-4 text-danger">Không tải được eps.</p>}

      {data && (
        <table className="w-full border-collapse">
          <thead>
            <tr>
              <th className={th}>VPU</th>
              <th className={th}>Profile</th>
              <th className={th}>eps mục tiêu (tính từ ramp)</th>
              <th className={th}>eps đạt được (đo mỗi giây)</th>
            </tr>
          </thead>
          <tbody className="font-mono">
            {rows.map((r) => (
              <tr key={r.vpuId}>
                <td className={td}>
                  {r.name} {!r.enabled && <span className="text-[#8b8f9a]">Off</span>}
                </td>
                <td className={td}>{r.profile}</td>
                <td className={`${td} ${val}`}>{r.targetEps ?? "—"}</td>
                <td className={`${td} ${val}`}>{r.actualEps}</td>
              </tr>
            ))}
            <tr>
              <td className={td}><b>Tổng</b></td>
              <td className={td}></td>
              <td className={`${td} ${val}`}>{totalTarget ?? "—"}</td>
              <td className={`${td} ${val}`}>{totalActual}</td>
            </tr>
          </tbody>
        </table>
      )}

      <p className="px-4 py-2.5 text-[11.5px] leading-normal text-[#8b8f9a]">
        Chỉ đọc: đây là kết quả đo và giá trị tính từ profile, không phải cài
        đặt. Muốn đổi mục tiêu, sửa ramp_from / ramp_to ở bảng trên.
      </p>
    </div>
  );
}