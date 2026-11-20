"use client";

import { useState } from "react";
import { td, th } from "@/components/common/table-styles";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import type { AffectObject, UnitDetail, RuleRegion } from "@/lib/api";
import { useUnitDetail, useUpdateRuleConf } from "./hooks";
import { NumInput } from "./num-input";
import {
  OBJECT_LABELS,
  buildConf,
  newRow,
  parseJson,
  toFormState,
  validateConf,
  type FormState,
} from "./rule-model";
import { cn } from "@/lib/utils";

const labelCls = "mb-1.5 block text-xs text-muted-foreground";
const note = "text-[11.5px] leading-normal text-[#8b8f9a]";
const readOnly = "bg-background/60";

export function RuleEditDialog({
  unitId,
  onClose,
}: {
  unitId: string;
  onClose: () => void;
}) {
  const { data, isLoading, isError } = useUnitDetail(unitId);

  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="max-h-[92vh] overflow-y-auto sm:max-w-[700px]">
        {data ? (
          <RuleEditor detail={data} onClose={onClose} />
        ) : (
          <DialogHeader>
            <DialogTitle>Edit bài toán</DialogTitle>
            <DialogDescription>
              {isLoading && "Đang tải…"}
              {isError && "Không tải được cấu hình bài toán."}
            </DialogDescription>
          </DialogHeader>
        )}
      </DialogContent>
    </Dialog>
  );
}

function RuleEditor({
  detail,
  onClose,
}: {
  detail: UnitDetail;
  onClose: () => void;
}) {
  const { unit } = detail;
  const save = useUpdateRuleConf();
  const [mode, setMode] = useState<"form" | "json">("form");
  const [form, setForm] = useState<FormState>(() => toFormState(detail.ruleConf));
  const [jsonText, setJsonText] = useState("");
  const [error, setError] = useState<string>();

  const patchRow = (key: number, patch: Partial<AffectObject>) =>
    setForm((f) => ({
      ...f,
      rows: f.rows.map((r) =>
        r.key === key ? { ...r, obj: { ...r.obj, ...patch } } : r,
      ),
    }));
  const patchExtra = (patch: Partial<FormState["extra"]>) =>
    setForm((f) => ({ ...f, extra: { ...f.extra, ...patch } }));

  function switchTo(next: "form" | "json") {
    if (next === mode) return;
    setError(undefined);
    if (next === "json") {
      const r = buildConf(form);
      if (!r.conf) return setError(r.error);
      setJsonText(JSON.stringify(r.conf, null, 2));
    } else {
      const r = parseJson(jsonText);
      if (!r.conf) return setError(r.error);
      const bad = validateConf(r.conf);
      if (bad) return setError(bad);
      setForm(toFormState(r.conf));
    }
    setMode(next);
  }

  function submit() {
    setError(undefined);
    const r = mode === "form" ? buildConf(form) : parseJson(jsonText);
    if (!r.conf) return setError(r.error);
    const bad = validateConf(r.conf);
    if (bad) return setError(bad);
    save.mutate(
      { unitId: unit.id, conf: r.conf },
      { onSuccess: onClose, onError: (e) => setError(e.message) },
    );
  }

  const ex = form.extra;
    const region = ex.region ?? {};
  const patchRegion = (patch: Partial<RuleRegion>) =>
    patchExtra({ region: { ...region, ...patch } });

  return (
    <div className="flex flex-col gap-3">
      <DialogHeader>
        <div className="flex items-center justify-between pr-6">
          <DialogTitle>Edit bài toán</DialogTitle>
          <div className="flex gap-1.5">
            <Button size="sm" variant={mode === "form" ? "default" : "outline"} onClick={() => switchTo("form")}>
              Form
            </Button>
            <Button size="sm" variant={mode === "json" ? "default" : "outline"} onClick={() => switchTo("json")}>
              JSON rule_conf
            </Button>
          </div>
        </div>
        <DialogDescription className="sr-only">
          Sửa rule_conf của bài toán {unit.name}.
        </DialogDescription>
      </DialogHeader>

      <div className="flex flex-wrap gap-1.5 text-[11px]">
        {[`VPU ${detail.vpuName}`, `VPS ${detail.vpsName}`].map((t) => (
          <span key={t} className="rounded-full bg-muted px-2 py-0.5 text-muted-foreground">{t}</span>
        ))}
        <span className="rounded-full bg-muted px-2 py-0.5 font-mono text-muted-foreground">
          task {unit.taskId.slice(0, 4)}…{unit.taskId.slice(-4)}
        </span>
      </div>

      <div className="flex items-end gap-2.5">
        <div className="flex-[2]">
          <label className={labelCls}>Bài toán (rule name)</label>
          <Input value={unit.name} readOnly className={readOnly} />
        </div>
        <div className="w-[70px]">
          <label className={labelCls}>Type</label>
          <Input value={unit.ruleType} readOnly className={cn(readOnly, "font-mono")} />
        </div>
        <div className="flex-[2]">
          <label className={labelCls}>aiFlowCode</label>
          <Input value={`${unit.aiFlowCode.name} (${unit.aiFlowCode.code})`} readOnly className={readOnly} />
        </div>
      </div>
      <p className={note}>
        Tên, type và aiFlowCode do VMS gửi, không sửa ở đây. Bật/tắt bài toán
        dùng công tắc On ở màn chi tiết VPS.
      </p>

      {mode === "json" ? (
        <Textarea
          value={jsonText}
          onChange={(e) => setJsonText(e.target.value)}
          spellCheck={false}
          aria-label="rule_conf JSON"
          className="h-[360px] font-mono text-xs"
        />
      ) : (
        <>
          <div>
            <div className="flex items-center justify-between">
              <label className={cn(labelCls, "mb-0")}>affect_object</label>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setForm((f) => ({ ...f, rows: [...f.rows, newRow()] }))}
              >
                + Add object
              </Button>
            </div>
            <table className="mt-1.5 w-full border-collapse">
              <thead>
                <tr>
                  {["label", "overlap", "dim_ratio", "min_area", "movement", "direction", ""].map((h) => (
                    <th key={h} className={th}>{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {form.rows.map(({ key, obj }) => {
                  const labels = OBJECT_LABELS.includes(obj.label)
                    ? OBJECT_LABELS
                    : [obj.label, ...OBJECT_LABELS];
                  return (
                    <tr key={key}>
                      <td className={td}>
                        <select
                          value={obj.label}
                          aria-label="label"
                          onChange={(e) => patchRow(key, { label: e.target.value })}
                          className="h-7 w-24 rounded-md border border-input bg-background px-1.5 text-xs"
                        >
                          {labels.map((l) => (
                            <option key={l} value={l}>{l}</option>
                          ))}
                        </select>
                      </td>
                      <td className={td}>
                        <NumInput label="overlap_ratio" value={obj.overlap_ratio} className="w-[54px]"
                          onChange={(n) => patchRow(key, { overlap_ratio: n })} />
                      </td>
                      <td className={td}>
                        <div className="flex gap-1">
                          <NumInput label="dim_min" value={obj.dim_ratio[0]} className="w-11"
                            onChange={(n) => patchRow(key, { dim_ratio: [n, obj.dim_ratio[1]] })} />
                          <NumInput label="dim_max" value={obj.dim_ratio[1]} className="w-11"
                            onChange={(n) => patchRow(key, { dim_ratio: [obj.dim_ratio[0], n] })} />
                        </div>
                      </td>
                      <td className={td}>
                        <NumInput label="min_area" value={obj.min_area} className="w-16"
                          onChange={(n) => patchRow(key, { min_area: n })} />
                      </td>
                      <td className={cn(td, "text-[11.5px]")}>
                        {obj.movement.join(", ") || "—"}
                      </td>
                      <td className={td}>
                        <NumInput label="direction" value={obj.direction} className="w-[54px]"
                          onChange={(n) => patchRow(key, { direction: n })} />
                      </td>
                      <td className={td}>
                        <Button
                          size="sm"
                          variant="outline"
                          aria-label="Remove object"
                          onClick={() => setForm((f) => ({ ...f, rows: f.rows.filter((r) => r.key !== key) }))}
                          className="border-[#f2b8b8] bg-[#fdf1f1] text-danger hover:bg-[#fdeceb] hover:text-danger"
                        >
                          ×
                        </Button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
            <p className={note}>
              Danh sách affect_object rỗng thì bài toán không phát (precondition).
              Muốn sửa movement thì dùng tab JSON.
            </p>
          </div>

         <div>
            <label htmlFor="r-region" className={labelCls}>Region</label>
            <Textarea
              id="r-region"
              value={region.path ?? ""}
              onChange={(e) => patchRegion({ path: e.target.value })}
              className="h-[46px] font-mono text-xs"
            />
            <div className="mt-2 flex gap-2.5">
              <div className="flex-1">
                <label htmlFor="r-head" className={labelCls}>head (POINT)</label>
                <Input
                  id="r-head"
                  value={region.head ?? ""}
                  onChange={(e) => patchRegion({ head: e.target.value })}
                  className="font-mono"
                />
              </div>
              <div>
                <label className={labelCls}>target_dims</label>
                <div className="flex items-center gap-1.5">
                  <NumInput label="target width" value={region.target_dims?.[0] ?? 0} className="h-9 w-14"
                    onChange={(n) => patchRegion({ target_dims: [n, region.target_dims?.[1] ?? 0] })} />
                  <span>×</span>
                  <NumInput label="target height" value={region.target_dims?.[1] ?? 0} className="h-9 w-14"
                    onChange={(n) => patchRegion({ target_dims: [region.target_dims?.[0] ?? 0, n] })} />
                </div>
              </div>
              <div className="w-28">
                <label htmlFor="r-anchor" className={labelCls}>anchor</label>
                <Input
                  id="r-anchor"
                  value={region.anchor ?? ""}
                  onChange={(e) => patchRegion({ anchor: e.target.value })}
                />
              </div>
            </div>
          </div>

          <div>
            <label className={labelCls}>Tham số riêng của bài toán này</label>
            <div className="flex gap-2.5">
              <div>
                <label className="mb-1 block text-[11px] text-muted-foreground">reset_timer (s)</label>
                <NumInput label="reset_timer" value={ex.reset_timer ?? 0} className="h-9 w-[90px]"
                  onChange={(n) => patchExtra({ reset_timer: n })} />
              </div>
              <div className="flex-1">
                <label htmlFor="r-except" className="mb-1 block text-[11px] text-muted-foreground">except_rules</label>
                <Input id="r-except" value={form.exceptText} className="font-mono"
                  onChange={(e) => setForm((f) => ({ ...f, exceptText: e.target.value }))} />
              </div>
            </div>
          </div>
        </>
      )}

      <p className={note}>
        Save áp dụng ngay cho unit đang chạy và giữ nguyên trạng thái chạy (giống
        cntrl-task, FR-CMD-6/7). Nếu VMS gửi lại rule_conf (add-task hoặc
        update-task) thì giá trị của VMS ghi đè (FR-CMD-13).
      </p>

      {error && (
        <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {error}
        </div>
      )}

      <DialogFooter>
        <Button variant="outline" onClick={onClose} disabled={save.isPending}>Huỷ</Button>
        <Button onClick={submit} disabled={save.isPending}>
          {save.isPending ? "Đang lưu…" : "Save"}
        </Button>
      </DialogFooter>
    </div>
  );
}