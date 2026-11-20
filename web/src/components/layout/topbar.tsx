"use client";

import Link from "next/link";
import { Fragment } from "react";
import { cn } from "@/lib/utils";
import { useSimulatorHealth } from "@/features/system/hooks";

export interface Crumb {
  label: string;
  href?: string;
}

export function Topbar({ crumbs }: { crumbs: Crumb[] }) {
  const { data, isError } = useSimulatorHealth();
  const state = isError || data?.online === false ? "offline" : data ? "online" : "loading";

  return (
    <div className="flex items-center justify-between text-[13px] text-muted-foreground">
      <nav aria-label="Breadcrumb" className="flex items-center">
        <span>VPU Simulator</span>
        {crumbs.map((c, i) => {
          const last = i === crumbs.length - 1;
          return (
            <Fragment key={i}>
              <span className="mx-1.5">/</span>
              {last ? (
                <b className="text-foreground">{c.label}</b>
              ) : c.href ? (
                <Link href={c.href} className="hover:text-foreground">
                  {c.label}
                </Link>
              ) : (
                <span>{c.label}</span>
              )}
            </Fragment>
          );
        })}
      </nav>

      <span
        className={cn(
          "rounded-full px-2 py-0.5 text-[11px]",
          state === "online" && "bg-success-soft text-success",
          state === "offline" && "bg-danger-soft text-danger",
          state === "loading" && "bg-muted text-muted-foreground",
        )}
      >
        ● Simulator {state === "loading" ? "…" : state}
      </span>
    </div>
  );
}