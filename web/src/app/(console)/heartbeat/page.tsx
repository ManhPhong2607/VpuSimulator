import { Topbar } from "@/components/layout/topbar";
import { HeartbeatView } from "@/features/heartbeat/heartbeat-view";

export default function HeartbeatPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Heartbeat" }]} />
      <HeartbeatView />
    </>
  );
}