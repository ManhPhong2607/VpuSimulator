"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  api,
  type CreateVpuInput,
  type UpdateVpuInput,
  type Vpu,
} from "@/lib/api";
import { useOptimisticList } from "@/lib/optimistic";

export const vpuKeys = { all: ["vpus"] as const };

export function useVpus() {
  return useQuery({
    queryKey: vpuKeys.all,
    queryFn: () => api.listVpus(),
    refetchInterval: 5000,
  });
}

export function useProfileNames() {
  return useQuery({
    queryKey: ["profile-names"],
    queryFn: () => api.listProfileNames(),
    staleTime: 60_000,
  });
}

/** Tổng eps, số lượng... phụ thuộc nhau nên sau mỗi thay đổi làm mới tất cả. */
function useRefreshAll() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries();
}

export function useSetVpuEnabled() {
  const list = useOptimisticList<Vpu>(vpuKeys.all);
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (v: { id: string; enabled: boolean }) =>
      api.setVpuEnabled(v.id, v.enabled),
    onMutate: (v) => list.patch(v.id, (x) => ({ ...x, enabled: v.enabled })),
    onError: (_e, _v, ctx) => list.rollback(ctx?.prev),
    onSettled: refresh,
  });
}

export function useSetHeartbeat() {
  const list = useOptimisticList<Vpu>(vpuKeys.all);
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (v: { id: string; enabled: boolean }) =>
      api.setHeartbeatEnabled(v.id, v.enabled),
    onMutate: (v) =>
      list.patch(v.id, (x) => ({
        ...x,
        heartbeat: { ...x.heartbeat, enabled: v.enabled },
      })),
    onError: (_e, _v, ctx) => list.rollback(ctx?.prev),
    onSettled: refresh,
  });
}

export function useCreateVpu() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (input: CreateVpuInput) => api.createVpu(input),
    onSettled: refresh,
  });
}

export function useUpdateVpu() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (v: { id: string; input: UpdateVpuInput }) =>
      api.updateVpu(v.id, v.input),
    onSettled: refresh,
  });
}

export function useRemoveVpu() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (id: string) => api.removeVpu(id),
    onSettled: refresh,
  });
}