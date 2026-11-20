import type { ControlApi } from "./control-api";
import { httpApi } from "./http-api";
import { mockApi } from "./mock-api";

// NEXT_PUBLIC_USE_MOCK=false -> gọi Control API thật qua proxy /api/control (xem http-api.ts).
// Không đặt hoặc true -> dữ liệu giả. Đổi biến này phải khởi động lại (hoặc build lại) Next.js.
// TODO(API-A): http-api.ts hiện mới nối các thao tác SRS đã định nghĩa rõ; phần còn lại chờ chốt DTO.
export const api: ControlApi =
  process.env.NEXT_PUBLIC_USE_MOCK === "false" ? httpApi : mockApi;

export * from "./types";
export { ApiError } from "./control-api";