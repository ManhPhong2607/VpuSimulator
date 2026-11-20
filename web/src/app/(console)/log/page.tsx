import { Topbar } from "@/components/layout/topbar";
import { LiveLogView } from "@/features/log/live-log-view";

export default function LogPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Live log" }]} />
      <LiveLogView />
    </>
  );
}