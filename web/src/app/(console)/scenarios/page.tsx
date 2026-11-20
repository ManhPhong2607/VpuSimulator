import { Topbar } from "@/components/layout/topbar";
import { ScenariosView } from "@/features/scenarios/scenarios-view";

export default function ScenariosPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Scenarios" }]} />
      <ScenariosView />
    </>
  );
}