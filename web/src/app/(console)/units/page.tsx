import { Topbar } from "@/components/layout/topbar";
import { UnitsView } from "@/features/units/units-view";

export default function UnitsPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Units" }]} />
      <UnitsView />
    </>
  );
}