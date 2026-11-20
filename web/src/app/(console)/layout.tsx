import { redirect } from "next/navigation";
import { Sidebar } from "@/components/layout/sidebar";
import { getSessionUser } from "@/lib/auth";

export default async function ConsoleLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const user = await getSessionUser();
  if (!user) redirect("/login");

  return (
    <div className="flex">
      <Sidebar user={user} />
      <main className="flex h-screen min-w-0 flex-1 flex-col gap-4 overflow-auto px-8 py-5">
        {children}
      </main>
    </div>
  );
}