"use client";

import { useQueryClient, type QueryKey } from "@tanstack/react-query";

export function useOptimisticList<T extends { id: string }>(key: QueryKey) {
  const qc = useQueryClient();
  return {
    async patch(id: string, fn: (item: T) => T) {
      await qc.cancelQueries({ queryKey: key });
      const prev = qc.getQueryData<T[]>(key);
      qc.setQueryData<T[]>(key, (old) =>
        old?.map((x) => (x.id === id ? fn(x) : x)),
      );
      return { prev };
    },
    rollback(prev?: T[]) {
      if (prev) qc.setQueryData(key, prev);
    },
  };
}