import { Topbar } from "@/components/layout/topbar";
import { ProfilesView } from "@/features/profiles/profiles-view";

export default function ProfilesPage() {
  return (
    <>
      <Topbar crumbs={[{ label: "Profiles" }]} />
      <ProfilesView />
    </>
  );
}