import { NextResponse } from "next/server";

const accessCookieName = "qa_auth_token";
const refreshCookieName = "qa_refresh_token";

const accessMaxAgeSeconds =
  60 * 60 * 2;

const refreshMaxAgeSeconds =
  60 * 60 * 24 * 400;

export interface RefreshedSession {
  token: string;
  refreshToken: string;
}

function getCookie(
  request: Request,
  name: string
): string | null {
  const cookieHeader =
    request.headers.get("cookie");

  if (!cookieHeader) {
    return null;
  }

  const prefix = `${name}=`;

  const pair = cookieHeader
    .split(";")
    .map((item) => item.trim())
    .find((item) =>
      item.startsWith(prefix)
    );

  if (!pair) {
    return null;
  }

  return decodeURIComponent(
    pair.slice(prefix.length)
  );
}

function requestIsHttps(
  request: Request
): boolean {
  const forwardedProto =
    request.headers
      .get("x-forwarded-proto")
      ?.split(",")[0]
      .trim()
      .toLowerCase();

  return forwardedProto
    ? forwardedProto === "https"
    : new URL(request.url).protocol ===
        "https:";
}

export function getAccessToken(
  request: Request
): string | null {
  return getCookie(
    request,
    accessCookieName
  );
}

export function getRefreshToken(
  request: Request
): string | null {
  return getCookie(
    request,
    refreshCookieName
  );
}

export function setAuthCookies(
  response: NextResponse,
  request: Request,
  session: RefreshedSession
) {
  const secure =
    requestIsHttps(request);

  response.cookies.set(
    accessCookieName,
    session.token,
    {
      httpOnly: true,
      secure,
      sameSite: "lax",
      path: "/",
      maxAge:
        accessMaxAgeSeconds,
    }
  );

  response.cookies.set(
    refreshCookieName,
    session.refreshToken,
    {
      httpOnly: true,
      secure,
      sameSite: "lax",
      path: "/",
      maxAge:
        refreshMaxAgeSeconds,
    }
  );
}

export function clearAuthCookies(
  response: NextResponse,
  request: Request
) {
  const secure =
    requestIsHttps(request);

  for (const name of [
    accessCookieName,
    refreshCookieName,
  ]) {
    response.cookies.set(
      name,
      "",
      {
        httpOnly: true,
        secure,
        sameSite: "lax",
        path: "/",
        maxAge: 0,
      }
    );
  }
}

export async function
refreshDashboardSession(
  request: Request,
  backendBaseUrl: string
): Promise<RefreshedSession | null> {
  const refreshToken =
    getRefreshToken(request);

  if (!refreshToken) {
    return null;
  }

  const response =
    await fetch(
      `${backendBaseUrl}/api/auth/refresh`,
      {
        method: "POST",
        headers: {
          "Content-Type":
            "application/json",
        },
        body: JSON.stringify({
          refreshToken,
        }),
        cache: "no-store",
      }
    );

  if (!response.ok) {
    return null;
  }

  const data =
    (await response.json()) as {
      token?: string;
      refreshToken?: string;
    };

  if (
    !data.token ||
    !data.refreshToken
  ) {
    return null;
  }

  return {
    token: data.token,
    refreshToken:
      data.refreshToken,
  };
}