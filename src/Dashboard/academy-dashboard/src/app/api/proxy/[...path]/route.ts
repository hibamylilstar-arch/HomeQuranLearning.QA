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

export async function GET(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      path: string[];
    }>;
  }
) {
  const { path } = await params;

  return proxy(
    path,
    request,
    "GET"
  );
}

export async function POST(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      path: string[];
    }>;
  }
) {
  const { path } = await params;

  return proxy(
    path,
    request,
    "POST"
  );
}

export async function PATCH(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      path: string[];
    }>;
  }
) {
  const { path } = await params;

  return proxy(
    path,
    request,
    "PATCH"
  );
}

export async function PUT(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      path: string[];
    }>;
  }
) {
  const { path } = await params;

  return proxy(
    path,
    request,
    "PUT"
  );
}

export async function DELETE(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      path: string[];
    }>;
  }
) {
  const { path } = await params;

  return proxy(
    path,
    request,
    "DELETE"
  );
}

async function proxy(
  pathSegments: string[],
  request: Request,
  method: string
) {
  const sourceUrl =
    new URL(request.url);

  const url =
    `${backendBaseUrl}/api/admin/${pathSegments.join(
      "/"
    )}${sourceUrl.search}`;

  const requestBody =
    method === "GET" ||
    method === "DELETE"
      ? undefined
      : await request.text();

  const send =
    (token: string) =>
      fetch(
        url,
        {
          method,
          headers: {
            Authorization:
              `Bearer ${token}`,
            "Content-Type":
              "application/json",
          },
          body: requestBody,
          cache: "no-store",
        }
      );

  let token =
    getAccessToken(request);

  let refreshed:
    RefreshedSession | null =
      null;

  if (!token) {
    refreshed =
      await refreshDashboardSession(
        request,
        backendBaseUrl
      );

    token =
      refreshed?.token ??
      null;
  }

  if (!token) {
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

  let res =
    await send(token);

  if (
    res.status === 401 &&
    !refreshed
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

    res =
      await send(
        refreshed.token
      );
  }

  if (
    res.status === 204 ||
    res.status === 205 ||
    res.status === 304
  ) {
    const response =
      new NextResponse(
        null,
        {
          status: res.status,
        }
      );

    if (refreshed) {
      setAuthCookies(
        response,
        request,
        refreshed
      );
    }

    return response;
  }

  const data =
    await res
      .json()
      .catch(() => null);

  const response =
    NextResponse.json(
      data ?? {},
      {
        status: res.status,
      }
    );

  if (refreshed) {
    setAuthCookies(
      response,
      request,
      refreshed
    );
  }

  if (res.status === 401) {
    clearAuthCookies(
      response,
      request
    );
  }

  return response;
}