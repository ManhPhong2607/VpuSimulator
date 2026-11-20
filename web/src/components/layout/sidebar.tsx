"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Activity,
  Cpu,
  Database,
  FileText,
  LayoutGrid,
  Radio,
  Server,
  Settings,
  SlidersHorizontal,
  type LucideIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { logout } from "@/app/login/actions";
import { useVpus } from "@/features/vpu/hooks";
import { useSimulatorHealth } from "@/features/system/hooks";

type ItemKey =
  | "vpu" | "heartbeat" | "units" | "profiles"
  | "scenarios" | "log" | "broker" | "storage" | "config";

interface NavItem {
  key: ItemKey;
  href: string;
  label: string;
  icon: LucideIcon;
  /** Các tiền tố đường dẫn làm mục này sáng lên. */
  match: string[];
}

const SECTIONS: { title: string; items: NavItem[] }[] = [
  {
    title: "QUẢN LÝ",
    items: [
      { key: "vpu", href: "/vpu", label: "VPU manager", icon: Cpu, match: ["/vpu", "/vps"] },
      { key: "heartbeat", href: "/heartbeat", label: "Heartbeat", icon: Activity, match: ["/heartbeat"] },
    ],
  },
  {
    title: "VẬN HÀNH",
    items: [
      { key: "units", href: "/units", label: "Units", icon: LayoutGrid, match: ["/units"] },
      { key: "profiles", href: "/profiles", label: "Profiles", icon: SlidersHorizontal, match: ["/profiles"] },
      { key: "scenarios", href: "/scenarios", label: "Scenarios", icon: FileText, match: ["/scenarios"] },
      { key: "log", href: "/log", label: "Live log", icon: Radio, match: ["/log"] },
    ],
  },
  {
    title: "HỆ THỐNG",
    items: [
      // Broker và Object storage cùng trỏ về /config nhưng không tự sáng
      { key: "broker", href: "/config", label: "Message broker", icon: Server, match: [] },
      { key: "storage", href: "/config", label: "Object storage", icon: Database, match: [] },
      { key: "config", href: "/config", label: "Configuration", icon: Settings, match: ["/config"] },
    ],
  },
];

function Trailing({ k }: { k: ItemKey }) {
  const { data: vpus } = useVpus();
  const { data: health } = useSimulatorHealth();

  if (k === "vpu" && vpus) {
    return <span className="ml-auto text-[11px] text-[#8b95a7]">{vpus.length}</span>;
  }
  if (k === "heartbeat" && vpus) {
    const off = vpus.filter((v) => !v.heartbeat.enabled).length;
    return off > 0 ? (
      <span className="ml-auto text-[11px] text-[#fbbf24]">{off}</span>
    ) : null;
  }
  if (k === "log") {
    return <span className="ml-auto size-[7px] rounded-full bg-sidebar-accent" />;
  }
  if ((k === "broker" || k === "storage") && health) {
    const up = health[k];
    return (
      <span className={cn("ml-auto text-[11px]", up ? "text-[#34d399]" : "text-[#f87171]")}>
        {up ? "online" : "offline"}
      </span>
    );
  }
  return null;
}

export function Sidebar({ user }: { user: string }) {
  const pathname = usePathname();
  const isActive = (item: NavItem) =>
    item.match.some((p) => pathname === p || pathname.startsWith(p + "/"));

  return (
    <aside className="sticky top-0 flex h-screen w-60 shrink-0 flex-col gap-[3px] bg-sidebar px-3.5 py-5 text-[13px] text-sidebar-foreground">
      <div className="flex items-center gap-2.5 px-1.5 pb-3.5">
        <div className="size-[34px] rounded-[9px] bg-primary" />
        <div>
          <b className="text-[15px] text-white">VPU Simulator</b>
          <div className="text-[11px] text-[#7c869a]">VMS data emulator</div>
        </div>
      </div>

      {SECTIONS.map((section) => (
        <div key={section.title} className="flex flex-col gap-[3px]">
          <div className="px-2 pb-1.5 pt-3.5 text-[10.5px] tracking-[.12em] text-[#64748b]">
            {section.title}
          </div>
          {section.items.map((item) => {
            const active = isActive(item);
            return (
              <Link
                key={item.key}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "flex items-center gap-2.5 rounded-lg border-l-2 border-transparent px-2.5 py-[9px] transition-colors hover:text-white",
                  active && "border-sidebar-accent bg-sidebar-active text-white",
                )}
              >
                <item.icon className="size-[17px]" strokeWidth={1.8} />
                <span>{item.label}</span>
                <Trailing k={item.key} />
              </Link>
            );
          })}
        </div>
      ))}

      <div className="mt-auto flex items-center gap-2.5 border-t border-[#1e2740] px-2 pt-3">
        <div className="flex size-[30px] items-center justify-center rounded-full bg-[#26324d] font-semibold text-white">
          {user.charAt(0).toUpperCase()}
        </div>
        <div className="leading-snug">
          <div className="text-white">{user}</div>
          <form action={logout}>
            <button type="submit" className="text-[11.5px] text-[#7c869a] hover:text-white">
              Sign out
            </button>
          </form>
        </div>
      </div>
    </aside>
  );
}