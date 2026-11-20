"use client";

import { useState } from "react";
import { Input } from "@/components/ui/input";
import { cn } from "@/lib/utils";

export function NumInput({
  value,
  onChange,
  label,
  className,
}: {
  value: number;
  onChange: (n: number) => void;
  label: string;
  className?: string;
}) {
  const [text, setText] = useState(String(value));
  const bad = text.trim() === "" || !Number.isFinite(Number(text));

  return (
    <Input
      value={text}
      inputMode="decimal"
      aria-label={label}
      aria-invalid={bad}
      onChange={(e) => {
        const t = e.target.value;
        setText(t);
        if (t.trim() !== "" && Number.isFinite(Number(t))) onChange(Number(t));
      }}
      onBlur={() => bad && setText(String(value))}
      className={cn("h-7 px-1.5 font-mono text-xs", className)}
    />
  );
}