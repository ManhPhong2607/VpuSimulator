"use client";

import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { card } from "./styles";
import { useSystemConfig } from "./hooks";

type Style = "mono" | "ok" | "err" | "warn" | "muted";
interface Row { label: string; value: string; style?: Style }

const VALUE: Record<Style, string> = {
  mono: "font-mono",
  ok: "text-success",
  err: "text-danger",
  warn: "text-warning",
  muted: "text-[#8b8f9a]",
};

function InfoCard({ title, rows }: { title: string; rows: Row[] }) {
  return (
    <div className={cn(card, "gap-3.5")}>
      <div className="text-[13px] font-semibold">{title}</div>
      {rows.map((r) => (
        <div key={r.label} className="flex justify-between gap-4 text-[12.5px]">
          <span className="text-muted-foreground">{r.label}</span>
          <span className={cn("text-right", VALUE[r.style ?? "mono"])}>{r.value}</span>
        </div>
      ))}
    </div>
  );
}

export function InfoCards() {
  const { data: c, isLoading, isError, refetch } = useSystemConfig();

  if (isLoading) return <p className="col-span-2 text-muted-foreground">Đang tải cấu hình…</p>;
  if (isError || !c) {
    return (
      <div role="alert" className="col-span-2 flex items-center gap-3 text-danger">
        Không tải được cấu hình.
        <Button size="sm" variant="outline" onClick={() => refetch()}>Thử lại</Button>
      </div>
    );
  }

  return (
    <>
      <InfoCard
        title="Broker"
        rows={[
          { label: "Host", value: c.broker.host },
          { label: "Outbound exchange", value: c.broker.outboundExchange },
          { label: "Inbound routing", value: c.broker.inboundRouting },
          {
            label: "Status",
            value: c.broker.connected ? "connected" : "disconnected",
            style: c.broker.connected ? "ok" : "err",
          },
        ]}
      />
      <InfoCard
        title="Object storage"
        rows={[
          { label: "Backend", value: c.storage.backend },
          { label: "Access", value: c.storage.access },
          { label: "Upload delay", value: c.storage.uploadDelay },
          { label: "Retry", value: c.storage.retry },
        ]}
      />
      <InfoCard
        title="On-demand AI API"
        rows={[
          { label: "Listener", value: c.onDemandApi.listener },
          {
            label: "Auth",
            value: c.onDemandApi.auth,
            style: c.onDemandApi.unauthenticated ? "warn" : "mono",
          },
        ]}
      />
      <InfoCard
        title="AI backend"
        rows={[
          { label: "Mode", value: c.aiBackend.mode },
          {
            label: "Model service (RPC)",
            value: c.aiBackend.modelService ?? "not implemented yet",
            style: c.aiBackend.modelService ? "mono" : "muted",
          },
          { label: "Frame mode", value: c.aiBackend.frameMode },
        ]}
      />
    </>
  );
}