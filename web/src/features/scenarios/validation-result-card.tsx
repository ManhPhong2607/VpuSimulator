import { CheckCircle2, TriangleAlert, XCircle } from "lucide-react";
import type { ScenarioValidation, ValidationIssue } from "@/lib/api";

const STYLE = {
  error: {
    box: "border-[#f2b8b8] bg-[#fdeceb]",
    title: "text-[#8a1f1f]",
    detail: "text-[#a8453f]",
    icon: "text-danger",
    Icon: XCircle,
  },
  warning: {
    box: "border-[#f0d49a] bg-[#fdf3e0]",
    title: "text-[#7a5205]",
    detail: "text-[#8f6a1f]",
    icon: "text-warning",
    Icon: TriangleAlert,
  },
} as const;

function Issue({ issue }: { issue: ValidationIssue }) {
  const s = STYLE[issue.severity];
  return (
    <div className={`flex gap-2.5 rounded-lg border px-3 py-2.5 ${s.box}`}>
      <s.Icon className={`mt-0.5 size-4 shrink-0 ${s.icon}`} />
      <div className="text-[12.5px]">
        <div className={`font-semibold ${s.title}`}>{issue.title}</div>
        <div className={s.detail}>{issue.detail}</div>
      </div>
    </div>
  );
}

export function ValidationResultCard({
  result,
  pending,
  error,
}: {
  result?: ScenarioValidation;
  pending: boolean;
  error?: string;
}) {
  return (
    <div className="flex flex-[1.3] flex-col gap-2.5 overflow-auto rounded-[10px] border bg-card p-5">
      <div className="text-[13px] font-semibold">Validation result</div>

      {pending && <p className="text-muted-foreground">Đang validate…</p>}
      {error && (
        <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {error}
        </div>
      )}
      {!pending && !error && !result && (
        <p className="text-xs text-muted-foreground">
          Chưa validate. Chọn file rồi bấm Validate.
        </p>
      )}

      {result && !pending && (
        <>
          {result.issues.map((issue, i) => (
            <Issue key={i} issue={issue} />
          ))}
          {result.okMessage && (
            <div className="flex gap-2.5 rounded-lg border border-[#bfe3c4] bg-[#eef7ef] px-3 py-2.5">
              <CheckCircle2 className="mt-0.5 size-4 shrink-0 text-success" />
              <div className="text-[12.5px] text-[#175c34]">{result.okMessage}</div>
            </div>
          )}
          {result.issues.length === 0 && !result.okMessage && (
            <p className="text-xs text-muted-foreground">Không có gì để kiểm tra.</p>
          )}
        </>
      )}
    </div>
  );
}