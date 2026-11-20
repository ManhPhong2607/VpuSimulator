import type {LogEntry, LogOutcome} from "@/lib/api";
import { shortId} from "@/lib/format";

// TODO(API-A): hai hằng này nên lấy từ cấu hình của BE (bộ đệm 2000 dòng, max_body_log 4096).
export const LOG_BUFFER = 2000;
export const MAX_BODY_LOG = 4096;

// mau chu theo outcome
export const OUTCOME_COLOR: Record<LogOutcome, string> = {
   emitted: "text-[#6fcf97]",
   status: "text-[#6fcf97]",
   precondition: "text-[#f2994a]",
   command: "text-[#eb5757]",
   error: "text-[#eb5757]",
   no_message: "text-[#8b8f9a]",
};

/** Các outcome chọn được trong ô lọc (status và command chỉ hiện ở "All outcomes"). */
export const FILTER_OUTCOME: LogOutcome[] = [
    "emitted",
    "no_message",
    "precondition",
    "error",
];

export function describe(e: LogEntry) :string {
    return [e.identity, e.taskId && `task=${shortId(e.taskId)}`, e.rule, e.detail].filter(Boolean).join(" . ");
}

export function matches(e: LogEntry, query:string, outcome: string | null): boolean{
    if(outcome && e.outcome !== outcome) return false;
    const q = query.trim().toLowerCase();
    if(!q) return true;
    return [e.identity, e.taskId, e.rule, e.detail].some((x) => x?.toLowerCase().includes(q));
}