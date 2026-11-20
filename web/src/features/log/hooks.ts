"use client";

import { useEffect } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type LogEntry } from "@/lib/api";
import { LOG_BUFFER } from "./log-model";

export const logKeys = { all: ["log"] as const };

/** Lấy bộ đệm hiện tại, rồi nhận dòng mới theo thời gian thực và chèn lên đầu. */
export function useLiveLog() {
  const qc = useQueryClient();
  const query = useQuery({
    queryKey: logKeys.all,
    queryFn: () => api.listLog(),
    // Không tự làm mới: dòng mới đến qua subscribeLog. gcTime 0 để mỗi lần vào màn hình lấy bộ đệm mới.
    staleTime: Infinity,
    gcTime: 0,
    refetchOnWindowFocus: false,
  });

  const ready = query.isSuccess;
  useEffect(() => {
    if (!ready) return;
    // TODO(API-A): bản thật là EventSource tới SSE của BE (http-api.ts). Cần xử lý đứt kết nối
    // và nối lại (Last-Event-ID) để không mất dòng log.
    return api.subscribeLog((entry) => {
      qc.setQueryData<LogEntry[]>(logKeys.all, (old) =>
        old ? [entry, ...old].slice(0, LOG_BUFFER) : old,
      );
    });
  }, [ready, qc]);

  return query;
}