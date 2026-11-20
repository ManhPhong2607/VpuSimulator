"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type RuntimeSettings } from "@/lib/api";    
export function useSystemConfig() {
    return useQuery({
        queryKey: ["system-config"],
        queryFn: () => api.getSystemConfig(),
        staleTime: 30_000,
    });
}

export function useCorrelation() {
    return useQuery({
        queryKey: ["correlation"],
        queryFn: () => api.getCorrelation(),
        // todo(api): polling tạm thời, nếu BE có sự kiện thì thay bằng cập nhật theo sự kiện.
        refetchInterval: 5000,
    });
}

export function useRules() {
  return useQuery({
    queryKey: ["rules"],
    queryFn: () => api.listRules(),
    staleTime: 30_000,
  });
}

export function useUpdateRuntimeSettings() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (patch: Partial<RuntimeSettings>) => api.updateRuntimeSettings(patch),
    // Live log đọc cùng khoá này nên cập nhật ở đây là thấy ngay bên đó
    onSuccess: () => qc.invalidateQueries({ queryKey: ["runtime-settings"] }),
  });
}

export function useDeleteCorrelation() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteCorrelationEntry(id),
    onSettled: () => qc.invalidateQueries({ queryKey: ["correlation"] }),
  });
}

export function useClearCorrelation() {
  const qc = useQueryClient();
  return useMutation({
    // confirm: true cố định vì UI luôn hỏi xác nhận trước (FR-CTL-5)
    mutationFn: () => api.clearCorrelation({ confirm: true }),
    onSettled: () => qc.invalidateQueries({ queryKey: ["correlation"] }),
  });
}

