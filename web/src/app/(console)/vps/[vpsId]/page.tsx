import { VpsDetail } from "@/features/vps/vps-detail";

export default async function VpsPage({
  params,
}: {
  params: Promise<{ vpsId: string }>;
}) {
  const { vpsId } = await params;
  return <VpsDetail vpsId={vpsId} />;
}