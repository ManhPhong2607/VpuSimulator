"use client";

import { useActionState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { login, type LoginState } from "./actions";

export function LoginForm() {
  const [state, action, pending] = useActionState<LoginState, FormData>(
    login,
    {},
  );

  return (
    <form action={action} className="flex flex-col gap-4">
      <div>
        <label htmlFor="username" className="mb-1.5 block text-xs text-muted-foreground">
          Username
        </label>
        <Input
          id="username"
          name="username"
          placeholder="admin"
          autoComplete="username"
          autoFocus
          required
          className="h-[34px]"
        />
      </div>
      <div>
        <label htmlFor="password" className="mb-1.5 block text-xs text-muted-foreground">
          Password
        </label>
        <Input
          id="password"
          name="password"
          type="password"
          placeholder="••••••••"
          autoComplete="current-password"
          required
          className="h-[34px]"
        />
      </div>

      {state.error && (
        <div role="alert" className="rounded-md bg-danger-soft px-3 py-2 text-xs text-danger">
          {state.error}
        </div>
      )}

      <Button type="submit" disabled={pending} className="h-[38px] w-full">
        {pending ? "Đang đăng nhập…" : "Sign in"}
      </Button>
    </form>
  );
}