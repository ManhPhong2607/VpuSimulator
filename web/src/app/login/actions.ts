"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import {
  SESSION_COOKIE,
  SESSION_TTL_MS,
  createToken,
  safeEqual,
} from "@/lib/session";

export interface LoginState {
  error?: string;
}

export async function login(
  _prev: LoginState,
  formData: FormData,
): Promise<LoginState> {
  const username = String(formData.get("username") ?? "").trim();
  const password = String(formData.get("password") ?? "");

  const okUser = process.env.CONSOLE_USER;
  const okPass = process.env.CONSOLE_PASSWORD;
  if (!okUser || !okPass) {
    return { error: "Console chưa cấu hình CONSOLE_USER / CONSOLE_PASSWORD." };
  }
  if (!safeEqual(username, okUser) || !safeEqual(password, okPass)) {
    return { error: "Sai tên đăng nhập hoặc mật khẩu." };
  }

  const jar = await cookies();
  jar.set(SESSION_COOKIE, await createToken(username), {
    httpOnly: true,
    sameSite: "lax",
    path: "/",
    maxAge: SESSION_TTL_MS / 1000,
    // Khi triển khai sau HTTPS, thêm: secure: true
  });
  redirect("/vpu");
}

export async function logout() {
  const jar = await cookies();
  jar.delete(SESSION_COOKIE);
  redirect("/login");
}