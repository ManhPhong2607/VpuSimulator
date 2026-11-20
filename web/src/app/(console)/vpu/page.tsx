import { Suspense } from "react";
import { Topbar } from "@/components/layout/topbar";
import { VpuManager } from "@/features/vpu/vpu-manager";

export default function VpuPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "VPU manager" }]} />
      {/* useSearchParams trong VpuTable cần Suspense */}
      <Suspense fallback={null}>
        <VpuManager />
      </Suspense>
    </>
  );
}