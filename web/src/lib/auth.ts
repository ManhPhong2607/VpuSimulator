import { cookies } from "next/headers";
import { SESSION_COOKIE, verifyToken } from "./session";

export async function getSessionUser(): Promise<string | null> {
  const jar = await cookies();
  return verifyToken(jar.get(SESSION_COOKIE)?.value);
}