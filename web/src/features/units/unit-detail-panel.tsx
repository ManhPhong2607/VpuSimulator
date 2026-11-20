"use client";

import Link from "next/link";
import { useState } from "react";
import { Pill } from "@/components/common/status-badge";
import { Button } from "@/components/ui/button";
import { formatClock } from "@/lib/format";
import type { Unit } from "@/lib/api";
import {
  useLastResponse,
  useUnitDetail,
  useUnitLive,
} from "./hooks";
import { RuleEditDialog } from "./rule-edit-dialog";

const pre =
  "m-0 max-h-[260px] overflow-auto rounded-md border border-[#eef0f2] bg-[#fafbfc] p-2 font-mono text-[11px] leading-relaxed";

export function UnitDetailPanel({ unit }: { unit: Unit | null }) {
  return (
    <div className="flex-1 self-start overflow-hidden rounded-[10px] border bg-card">
      <div className="flex items-center justify-between gap-2 border-b px-4 py-3 font-semibold">
        <span>Unit detail</span>
        {unit && (
          <span className="truncate rounded-full bg-muted px-2 py-0.5 font-mono text-[11px] font-normal text-muted-foreground">
            {unit.vpuName} · {unit.vpsName} · {unit.name}
          </span>
        )}
      </div>
      {unit ? (
        <Panel key={unit.id} unit={unit} />
      ) : (
        <p className="p-4 text-xs text-muted-foreground">
          Bấm một dòng trong bảng để xem output và rule_conf của unit.
        </p>
      )}
    </div>
  );
}

function Panel({ unit }: { unit: Unit }) {
  const [tab, setTab] = useState<"output" | "conf">("output");

  return (
    <div className="flex flex-col gap-2.5 p-4">
      <div className="flex gap-1.5">
        <Button size="sm" variant={tab === "output" ? "default" : "outline"} onClick={() => setTab("output")}>
          Output
        </Button>
        <Button size="sm" variant={tab === "conf" ? "default" : "outline"} onClick={() => setTab("conf")}>
          rule_conf
        </Button>
      </div>
      {tab === "output" ? <OutputTab unit={unit} /> : <ConfTab unit={unit} />}
    </div>
  );
}

function OutputTab({ unit }: { unit: Unit }) {
  const live = useUnitLive(unit.id);
  const resp = useLastResponse(unit.id, 4000);
  const l = live.data;

  return (
    <>
      <p className="text-[11px] leading-normal text-[#8b8f9a]">
        Output lấy từ log buffer lọc theo unit, chỉ có khi verbose bật.{" "}
        <Link href="/log" className="text-primary">Mở Live log</Link>
      </p>

      <div className="text-xs text-muted-foreground">
        Profile <b className="text-foreground">{unit.profile}</b>
        {l?.nextInSec != null && (
          <> · lần kế tiếp sau <b className="text-foreground">{l.nextInSec}s</b></>
        )}
      </div>

      {unit.offReason && (
        <Pill tone="muted">Unit đang Off, không có output mới</Pill>
      )}

      {l?.lastEmit && (
        <div className="font-mono text-[11px] text-[#8b8f9a]">
          {formatClock(l.lastEmit.at)} · {l.lastEmit.result}
        </div>
      )}

      {resp.data ? (
        <pre className={pre}>{JSON.stringify(resp.data.payload, null, 2)}</pre>
      ) : (
        !resp.isLoading && (
          <p className="text-[11.5px] text-[#8b8f9a]">
            Chưa có payload (verbose tắt hoặc bài toán chưa phát).
          </p>
        )
      )}

      {l?.notice && (
        <div className="rounded-md border border-[#f0d49a] bg-warning-soft p-2 text-[11.5px] text-warning">
          {formatClock(l.notice.at).slice(0, 8)} · {l.notice.message}
        </div>
      )}
    </>
  );
}

function ConfTab({ unit }: { unit: Unit }) {
  const { data, isLoading, isError } = useUnitDetail(unit.id);
  const [editing, setEditing] = useState(false);

  return (
    <>
      {isLoading && <p className="text-xs text-muted-foreground">Đang tải…</p>}
      {isError && <p className="text-xs text-danger">Không tải được rule_conf.</p>}
      {data && <pre className={pre}>{JSON.stringify(data.ruleConf, null, 2)}</pre>}
      <div className="flex justify-end">
        <Button size="sm" variant="outline" disabled={!!unit.offReason} onClick={() => setEditing(true)}>
          Edit rule_conf…
        </Button>
      </div>
      {editing && <RuleEditDialog unitId={unit.id} onClose={() => setEditing(false)} />}
    </>
  );
}