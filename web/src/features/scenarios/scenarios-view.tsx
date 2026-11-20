import { LoadedScenarioCard } from "./loaded-scenario-card";
import { ScenarioWorkbench } from "./scenario-workbench";

export function ScenariosView() {
  return (
    <>
      <div>
        <div className="text-base font-semibold">Load scenario</div>
        <div className="text-xs text-muted-foreground">
          Validate toàn bộ trước khi áp (FR-CTL-7). Công cụ test khi chưa có VMS:
          khi VMS đã kết nối, lệnh từ VMS và scenario có thể chồng lên nhau
          (W-11).
        </div>
      </div>
      <LoadedScenarioCard />
      <ScenarioWorkbench />
    </>
  );
}