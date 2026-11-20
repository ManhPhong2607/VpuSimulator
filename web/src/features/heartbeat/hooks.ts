"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type Vpu } from "@/lib/api";
import { useOptimisticList } from "@/lib/optimistic";
import { vpuKeys } from "@/features/vpu/hooks";

/** Sửa chu kỳ của một VPU: ô đổi ngay, lỗi thì trả về giá trị cũ. */
export function useSetHeartbeatInterval() {
  const qc = useQueryClient();
  const list = useOptimisticList<Vpu>(vpuKeys.all);
  return useMutation({
    mutationFn: (v: { id: string; sec: number }) =>
      api.setHeartbeatInterval(v.id, v.sec),
    onMutate: (v) =>
      list.patch(v.id, (x) => ({
        ...x,
        heartbeat: { ...x.heartbeat, intervalSec: v.sec },
      })),
    onError: (_e, _v, ctx) => list.rollback(ctx?.prev),
    onSettled: () => qc.invalidateQueries(),
  });
}

export type BulkAction =
  | { kind: "interval"; sec: number }
  | { kind: "enabled"; enabled: boolean };

/** Áp một thao tác cho nhiều VPU; trả về danh sách VPU bị lỗi. */
export function useBulkHeartbeat() {
  const qc = useQueryClient();
  return useMutation({
    // TODO(API-A): hiện gọi lần lượt từng VPU. Hỏi bạn A có endpoint hàng loạt không.
    mutationFn: async (v: { ids: string[]; action: BulkAction }) => {
      const { ids, action } = v;
      const results = await Promise.allSettled(
        ids.map((id) =>
          action.kind === "interval"
            ? api.setHeartbeatInterval(id, action.sec)
            : api.setHeartbeatEnabled(id, action.enabled),
        ),
      );
      return {
        total: ids.length,
        failed: ids.filter((_, i) => results[i].status === "rejected"),
      };
    },
    onSettled: () => qc.invalidateQueries(),
  });
}