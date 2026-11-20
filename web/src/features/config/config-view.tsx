import { CorrelationCard } from "./correlation-card";
import { InfoCards } from "./info-cards";
import { RulesCatalogCard } from "./rules-catalog-card";
import { RuntimeCard } from "./runtime-card";

export function ConfigView() {
  return (
    <>
      <div>
        <div className="text-base font-semibold">Configuration</div>
        <div className="text-xs text-muted-foreground">
          Chỉ xem, trừ mục runtime bên dưới (FR-CTL-3, FR-CFG-1)
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <RuntimeCard />
        <div className="col-span-2 text-xs text-[#8b8f9a]">
          Các khối bên dưới chỉ xem: đặt trong file cấu hình hoặc biến môi
          trường (FR-CFG-1), đổi cần khởi động lại.
        </div>
        <InfoCards />
        <CorrelationCard />
        <RulesCatalogCard />
      </div>
    </>
  );
} 