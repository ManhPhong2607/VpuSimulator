import { Topbar } from "@/components/layout/topbar";
import { ConfigView } from "@/features/config/config-view";

export default function ConfigPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Configuration" }]} />
      <ConfigView />
    </>
  );
}