import { Button } from "@/components/ui/button";
import { formatClock } from "@/lib/format";
import type { LogEntry } from "@/lib/api";
import { cn } from "@/lib/utils";
import { OUTCOME_COLOR, describe } from "./log-model";

export function LogTerminal({
  entries,
  selectedId,
  onSelect,
  loading,
  error,
  onRetry,
  filtering,
}: {
  entries: LogEntry[];
  selectedId: string | null;
  onSelect: (id: string) => void;
  loading: boolean;
  error: boolean;
  onRetry: () => void;
  filtering: boolean;
}) {
  const muted = "text-[#8b8f9a]";

  return (
    <div className="flex-[1.35] overflow-auto rounded-[10px] bg-[#0f1117] px-[18px] py-3.5 font-mono">
      <div
        role="log"
        aria-live="off"
        aria-label="Live log"
        className="text-[12.5px] leading-[2.1] text-[#c7c9d1]"
      >
        {loading && <div className={muted}>Đang tải log…</div>}
        {error && (
          <div className="flex items-center gap-3 text-[#eb5757]">
            Không tải được log.
            <Button size="sm" variant="outline" onClick={onRetry} className="h-7 bg-transparent text-xs">
              Thử lại
            </Button>
          </div>
        )}
        {!loading && !error && entries.length === 0 && (
          <div className={muted}>
            {filtering ? "Không có dòng log nào khớp bộ lọc." : "Chưa có dòng log nào."}
          </div>
        )}

        {entries.map((e) => {
          const selected = e.id === selectedId;
          return (
            <button
              key={e.id}
              type="button"
              onClick={() => onSelect(e.id)}
              aria-pressed={selected}
              className={cn(
                "-mx-2 block w-[calc(100%+1rem)] rounded-md px-2 text-left break-words",
                selected && "bg-[#1c2540]",
              )}
            >
              <span className="text-[#5b8def]">{formatClock(e.at)}</span>{" "}
              <span className={cn("inline-block w-[13ch]", OUTCOME_COLOR[e.outcome])}>
                {e.outcome}
              </span>
              {describe(e)}
            </button>
          );
        })}
      </div>
    </div>
  );
}