"use client";

import { Pill } from "@/components/common/status-badge";
import { Button } from "@/components/ui/button";
import type { RuleStatus } from "@/lib/api";
import { cn } from "@/lib/utils";
import { card, ctd, cth } from "./styles";
import { useRules } from "./hooks";

const STATUS: Record<RuleStatus, { tone: "ok" | "warn" | "muted"; label: string }> = {
  registered: { tone: "ok", label: "registered" },
  pending: { tone: "warn", label: "pending" },
  not_registered: { tone: "muted", label: "not registered" },
};

export function RulesCatalogCard() {
  const { data, isLoading, isError, refetch } = useRules();

  return (
    <div className={cn(card, "col-span-2 gap-2.5")}>
      <div className="flex items-center justify-between">
        <div className="text-[13px] font-semibold">Rules catalog</div>
        {/* TODO(API-A): xác nhận endpoint với bạn A; danh sách phản ánh IRuleHandlerRegistry đã đăng ký */}
        <span className="text-[11px] text-[#8b8f9a]">
          GET /api/v1/rules — đúng danh sách IRuleHandlerRegistry đã đăng ký
        </span>
      </div>

      {isLoading && <p className="text-muted-foreground">Đang tải…</p>}
      {isError && (
        <div role="alert" className="flex items-center gap-3 text-danger">
          Không tải được danh sách rule.
          <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
        </div>
      )}

      {data && (
        <table className="w-full border-collapse">
          <thead>
            <tr>
              {["name", "type", "nhóm", "AI backend", "trạng thái"].map((h) => (
                <th key={h} className={cth}>{h}</th>
              ))}
            </tr>
          </thead>
          <tbody className="font-mono">
            {data.map((r) => (
              <tr key={r.name}>
                <td className={ctd}>{r.name}</td>
                <td className={ctd}>{r.type}</td>
                <td className={ctd}>{r.group}</td>
                <td className={ctd}>{r.aiBackend}</td>
                <td className={ctd}>
                  <Pill tone={STATUS[r.status].tone}>{STATUS[r.status].label}</Pill>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </div>
  );
}