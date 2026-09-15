import { NextResponse } from "next/server";
import {
  clearAuthCookies,
  refreshDashboardSession,
  setAuthCookies,
} from "@/lib/server-auth";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ??
  "http://localhost:5100";

export async function POST(
  request: Request
) {
  const session =
    await refreshDashboardSession(
      request,
      backendBaseUrl
    );

  if (!session) {
    const response =
      NextResponse.json(
        {
          error:
            "Not authenticated",
        },
        {
          status: 401,
        }
      );

    clearAuthCookies(
      response,
      request
    );

    return response;
  }

  const response =
    NextResponse.json({
      ok: true,
    });

  setAuthCookies(
    response,
    request,
    session
  );

  return response;
}