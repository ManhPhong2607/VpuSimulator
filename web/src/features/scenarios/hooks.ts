"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type ScenarioFile } from "@/lib/api";

export const scenarioKeys = { loaded: ["scenario", "loaded"] as const };

export function useLoadedScenario() {
  return useQuery({
    queryKey: scenarioKeys.loaded,
    queryFn: () => api.getLoadedScenario(),
  });
}

/** Validate không đổi hệ thống nên không cần làm mới dữ liệu. */
export function useValidateScenario() {
  return useMutation({
    mutationFn: (file: ScenarioFile) => api.validateScenario(file),
  });
}

// TODO(API-A): nạp/gỡ scenario tạo hoặc xoá VPU, VPS, unit nên làm mới mọi query.
export function useApplyScenario() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (file: ScenarioFile) => api.applyScenario(file),
    onSettled: () => qc.invalidateQueries(),
  });
}

export function useRemoveScenario() {
  const qc = useQueryClient();
  return useMutation({
    // confirm: true cố định vì UI luôn hỏi xác nhận trước (FR-CTL-5)
    mutationFn: () => api.removeScenario({ confirm: true }),
    onSettled: () => qc.invalidateQueries(),
  });
}