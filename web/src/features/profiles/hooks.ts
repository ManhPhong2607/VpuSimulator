"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type ApplyScope, type ProfileInput } from "@/lib/api";

export const profileKeys = {
  all: ["profiles"] as const,
  eps: ["eps-by-vpu"] as const,
};

export function useProfiles() {
  return useQuery({
    queryKey: profileKeys.all,
    queryFn: () => api.listProfiles(),
  });
}

// TODO(API-A): làm mới mọi query vì đổi profile kéo theo eps/profile của VPU, VPS, unit.
function useRefreshAll() {
  const qc = useQueryClient();
  return () => qc.invalidateQueries();
}

export function useCreateProfile() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (input: ProfileInput) => api.createProfile(input),
    onSettled: refresh,
  });
}

export function useUpdateProfile() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (v: { name: string; input: ProfileInput }) =>
      api.updateProfile(v.name, v.input),
    onSettled: refresh,
  });
}

export function useDeleteProfile() {
  const refresh = useRefreshAll();
  return useMutation({
    // confirm: true cố định vì UI luôn hỏi xác nhận trước khi gọi (FR-CTL-5)
    mutationFn: (name: string) => api.deleteProfile(name, { confirm: true }),
    onSettled: refresh,
  });
}

export function useResetProfile() {
  const refresh = useRefreshAll();
  return useMutation({
    mutationFn: (name: string) =>
      api.resetProfileToDefault(name, { confirm: true }),
    onSettled: refresh,
  });
}

export function useApplyProfile() {
  const refresh = useRefreshAll();
  return useMutation({
        mutationFn: (v: { profile: string; scope: ApplyScope }) =>
      api.applyProfile(v.profile, v.scope),
    onSettled: refresh,
  });
}

export function useEpsByVpu() {
  return useQuery({
    queryKey: profileKeys.eps,
    queryFn: () => api.getEpsByVpu(),
    // TODO(API-A): polling 1s tạm thời ("đo mỗi giây"); nên dùng SSE/metrics của BE.
    refetchInterval: 1000,
  });
}