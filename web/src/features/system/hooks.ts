"use client";

import { useQuery } from "@tanstack/react-query";
import { api } from "@/lib/api";

export function useSimulatorHealth() {
  return useQuery({
    queryKey: ["health"],
    queryFn: () => api.getHealth(),
    refetchInterval: 10_000,
  });
}

export function useRuntimeSettings() {
  return useQuery({
    queryKey: ["runtime-settings"],
    queryFn: () => api.getRuntimeSettings(),
  });
}