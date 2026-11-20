import { ApiError } from "./control-api";

/**
 * Id của unit ở FE: `${appId}/${taskId}/${roiId}/${rule}` (rule mã hoá URL).
 * Khoá unit gồm tên rule vì một task có thể có hai rule cùng roi id nhưng khác tên
 * (FR-SIM-2, contract mục 10.1).
 * TODO(API-A): giả định app_id và task id không chứa dấu "/".
 */
export const makeUnitId = (
  appId: string,
  taskId: string,
  roiId: number | string,
  rule: string,
) => `${appId}/${taskId}/${roiId}/${encodeURIComponent(rule)}`;

export function parseUnitId(id: string): {
  appId: string;
  taskId: string;
  roiId: string;
  rule: string;
} {
  const parts = id.split("/");
  if (parts.length !== 4 || parts.some((p) => !p)) {
    throw new ApiError(400, `Id unit không hợp lệ: ${id}`);
  }
  try {
    return {
      appId: parts[0],
      taskId: parts[1],
      roiId: parts[2],
      rule: decodeURIComponent(parts[3]),
    };
  } catch {
    throw new ApiError(400, `Id unit không hợp lệ: ${id}`);
  }
}