"use client";

import { useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import type { ScenarioFile } from "@/lib/api";
import { useApplyScenario, useValidateScenario } from "./hooks";
import { summarizeScenario } from "./scenario-model";
import { ValidationResultCard } from "./validation-result-card";

const MAX_BYTES = 5 * 1024 * 1024;

export function ScenarioWorkbench() {
  const inputRef = useRef<HTMLInputElement>(null);
  const [file, setFile] = useState<ScenarioFile | null>(null);
  const [readError, setReadError] = useState<string>();
  const validate = useValidateScenario();
  const apply = useApplyScenario();

  const summary = file ? summarizeScenario(file.content) : null;
  const result = validate.data;
  const hasErrors = result?.issues.some((i) => i.severity === "error") ?? false;
  // Apply chỉ mở khi đã validate và không còn lỗi (cảnh báo không chặn)
  const canApply = !!file && !!result && !hasErrors && !validate.isPending && !apply.isPending;

  async function onPick(e: React.ChangeEvent<HTMLInputElement>) {
    const f = e.target.files?.[0];
    e.target.value = ""; // cho phép chọn lại đúng file đó lần sau
    if (!f) return;
    validate.reset();
    apply.reset();
    if (f.size > MAX_BYTES) {
      setFile(null);
      return setReadError("File quá lớn (tối đa 5 MB).");
    }
    try {
      setFile({ name: f.name, content: await f.text() });
      setReadError(undefined);
    } catch {
      setFile(null);
      setReadError("Không đọc được file.");
    }
  }

  return (
    <div className="flex min-h-0 flex-1 items-start gap-4">
      <div className="flex flex-1 flex-col gap-3.5 rounded-[10px] border bg-card p-5">
        <div>
          <label htmlFor="scn-file" className="mb-1.5 block text-xs text-muted-foreground">
            Scenario file
          </label>
          <div className="flex gap-2">
            <Input
              id="scn-file"
              value={file?.name ?? ""}
              placeholder="Chưa chọn file"
              readOnly
              className="h-9 flex-1"
            />
            <Button variant="outline" className="h-9" onClick={() => inputRef.current?.click()}>
              Browse
            </Button>
            <input
              ref={inputRef}
              type="file"
              accept=".json,application/json"
              onChange={onPick}
              className="hidden"
              aria-label="Chọn file scenario"
            />
          </div>
          {readError && <p role="alert" className="mt-1.5 text-xs text-danger">{readError}</p>}
        </div>

        <div>
          <span className="mb-1.5 block text-xs text-muted-foreground">Contains</span>
          <div className="text-[12.5px] leading-[1.8]">
            {!file && <div className="text-muted-foreground">—</div>}
            {file && !summary && (
              <div className="text-muted-foreground">
                Không đọc được cấu trúc file (JSON không hợp lệ).
              </div>
            )}
            {summary && (
              <>
                <div>
                  {summary.identities} identities · {summary.tasks} tasks ·{" "}
                  {summary.ruleConfs} rule_conf entries
                </div>
                <div>
                  seed = <span className="font-mono">{summary.seed ?? "—"}</span>
                </div>
              </>
            )}
          </div>
        </div>

        <Button
          className="h-[38px] w-full"
          disabled={!file || validate.isPending}
          onClick={() => file && validate.mutate(file)}
        >
          {validate.isPending ? "Đang validate…" : "Validate"}
        </Button>
        <Button
          className="h-[38px] w-full"
          disabled={!canApply}
          onClick={() =>
            file &&
            apply.mutate(file, {
              onSuccess: () => {
                setFile(null);
                validate.reset();
              },
            })
          }
        >
          {apply.isPending ? "Đang áp dụng…" : "Apply scenario"}
        </Button>

        {apply.error && (
          <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
            {apply.error.message}
          </div>
        )}
        <p className="text-[11.5px] text-[#8b8f9a]">
          Apply is disabled until validation passes with no errors.
        </p>
      </div>

      <ValidationResultCard
        result={result}
        pending={validate.isPending}
        error={validate.error?.message}
      />
    </div>
  );
}