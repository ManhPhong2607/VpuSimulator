"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type RuleConf, type Unit } from "@/lib/api";
import { useOptimisticList } from "@/lib/optimistic";

export const unitKeys = {
  all: ["units", "all"] as const,
  byVps: (vpsId: string) => ["units", "by-vps", vpsId] as const,
  detail: (id: string) => ["unit-detail", id] as const,
  response: (id: string) => ["unit-response", id] as const,
};

export function useUnits(vpsId: string) {
  return useQuery({
    queryKey: unitKeys.byVps(vpsId),
    queryFn: () => api.listUnits({ vpsId }),
    refetchInterval: 5000,
  });
}

export function useSetUnitEnabled(vpsId: string) {
  const qc = useQueryClient();
  const list = useOptimisticList<Unit>(unitKeys.byVps(vpsId));
  return useMutation({
    mutationFn: (v: { id: string; enabled: boolean }) =>
      api.setUnitEnabled(v.id, v.enabled),
    onMutate: (v) => list.patch(v.id, (x) => ({ ...x, enabled: v.enabled })),
    onError: (_e, _v, ctx) => list.rollback(ctx?.prev),
    onSettled: () => qc.invalidateQueries(),
  });
}

// gcTime: 0 để mỗi lần mở dialog luôn lấy dữ liệu mới, không dùng bản cache cũ
export function useUnitDetail(unitId: string) {
  return useQuery({
    queryKey: unitKeys.detail(unitId),
    queryFn: () => api.getUnitDetail(unitId),
    gcTime: 0,
    refetchOnWindowFocus: false,
  });
}

export function useUpdateRuleConf() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { unitId: string; conf: RuleConf }) =>
      api.updateRuleConf(v.unitId, v.conf),
    onSettled: () => qc.invalidateQueries(),
  });
}

export function useLastResponse(unitId: string, pollMs?: number) {
  return useQuery({
    queryKey: unitKeys.response(unitId),
    queryFn: () => api.getLastResponse(unitId),
    gcTime: 0,
    refetchInterval: pollMs,
  });
}

export function useAllUnits() {
  return useQuery({
    queryKey: unitKeys.all,
    queryFn: () => api.listUnits(),
    refetchInterval: 3000, // khi có SSE thật thì thay bằng cập nhật từ luồng sự kiện
  });
}

export function useUnitLive(unitId: string) {
  return useQuery({
    queryKey: ["unit-live", unitId] as const,
    queryFn: () => api.getUnitLive(unitId),
    refetchInterval: 2000,
    gcTime: 0,
  });
}

export function useForceEmit() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (unitId: string) => api.forceEmit(unitId),
    onSettled: () => qc.invalidateQueries(),
  });
}

export function useStopAllTasks() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => api.stopAllTasks({ confirm: true }),
    onSettled: () => qc.invalidateQueries(),
  });
}