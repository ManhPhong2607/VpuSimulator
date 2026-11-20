"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Vps } from "@/lib/api";
import { useOptimisticList } from "@/lib/optimistic";

export const vpsKeys = {
  byVpu: (vpuId: string) => ["vps", "by-vpu", vpuId] as const,
};

export function useVpsList(vpuId: string) {
  return useQuery({
    queryKey: vpsKeys.byVpu(vpuId),
    queryFn: () => api.listVps(vpuId),
    refetchInterval: 5000,
  });
}

export function useSetVpsEnabled(vpuId: string) {
  const qc = useQueryClient();
  const list = useOptimisticList<Vps>(vpsKeys.byVpu(vpuId));
  return useMutation({
    mutationFn: (v: { id: string; enabled: boolean }) =>
      api.setVpsEnabled(v.id, v.enabled),
    onMutate: (v) => list.patch(v.id, (x) => ({ ...x, enabled: v.enabled })),
    onError: (_e, _v, ctx) => list.rollback(ctx?.prev),
    onSettled: () => qc.invalidateQueries(),
  });
}

export function useRemoveVps() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.removeVps(id),
    onSettled: () => qc.invalidateQueries(),
  });
}

export function useVps(vpsId: string) {
  return useQuery({
    queryKey: ["vps", "detail", vpsId] as const,
    queryFn: () => api.getVps(vpsId),
    refetchInterval: 5000,
  });
}

/** Dùng ở màn chi tiết (không cần biết vpuId). */
export function useToggleVps() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (v: { id: string; enabled: boolean }) =>
      api.setVpsEnabled(v.id, v.enabled),
    onSettled: () => qc.invalidateQueries(),
  });
}