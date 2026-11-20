"use client";

import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";

interface Props {
  onClose: () => void;
  title: string;
  description: React.ReactNode;
  confirmLabel: string;
  destructive?: boolean;
  pending?: boolean;
  error?: string;
  onConfirm: () => void;
}

/** Hộp xác nhận. Chỉ render khi cần (cha mount có điều kiện). */
export function ConfirmDialog({
  onClose,
  title,
  description,
  confirmLabel,
  destructive,
  pending,
  error,
  onConfirm,
}: Props) {
  return (
    <Dialog open onOpenChange={(o) => !o && onClose()}>
      <DialogContent className="sm:max-w-[380px]">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription className="leading-relaxed">
            {description}
          </DialogDescription>
        </DialogHeader>
        {error && (
          <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
            {error}
          </div>
        )}
        <DialogFooter>
          <Button variant="outline" onClick={onClose} disabled={pending}>
            Huỷ
          </Button>
          <Button
            variant={destructive ? "destructive" : "default"}
            onClick={onConfirm}
            disabled={pending}
          >
            {pending ? "Đang xử lý…" : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}