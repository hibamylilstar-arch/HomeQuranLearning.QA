import { NextResponse } from "next/server";
import {
  clearAuthCookies,
  getAccessToken,
  refreshDashboardSession,
  setAuthCookies,
  type RefreshedSession,
} from "@/lib/server-auth";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ??
  "http://localhost:5100";

async function getProfile(
  token: string
) {
  return fetch(
    `${backendBaseUrl}/api/auth/me`,
    {
      headers: {
        Authorization:
          `Bearer ${token}`,
      },
      cache: "no-store",
    }
  );
}

export async function GET(
  request: Request
) {
  let token =
    getAccessToken(request);

  let refreshed:
    RefreshedSession | null =
      null;

  let res =
    token
      ? await getProfile(token)
      : null;

  if (
    !res ||
    res.status === 401
  ) {
    refreshed =
      await refreshDashboardSession(
        request,
        backendBaseUrl
      );

    if (!refreshed) {
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

    token = refreshed.token;
    res = await getProfile(token);
  }

  if (!res.ok) {
    const response =
      NextResponse.json(
        {
          error:
            res.status === 401
              ? "Not authenticated"
              : "Authentication service unavailable",
        },
        {
          status: res.status,
        }
      );

    if (res.status === 401) {
      clearAuthCookies(
        response,
        request
      );
    }

    return response;
  }

  const data =
    await res.json();

  const response =
    NextResponse.json(data);

  if (refreshed) {
    setAuthCookies(
      response,
      request,
      refreshed
    );
  }

  return response;
}