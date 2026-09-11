import { NextResponse } from "next/server";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ??
  "http://localhost:5100";

function getAuthToken(
  request: Request
): string | null {
  return (
    request.headers
      .get("cookie")
      ?.split(";")
      .map((cookie) => cookie.trim())
      .find((cookie) =>
        cookie.startsWith("qa_auth_token=")
      )
      ?.slice("qa_auth_token=".length) ??
    null
  );
}

export async function GET(
  request: Request,
  {
    params,
  }: {
    params: Promise<{
      alertId: string;
    }>;
  }
) {
  const token =
    getAuthToken(request);

  if (!token) {
    return NextResponse.json(
      {
        error: "Not authenticated",
      },
      {
        status: 401,
      }
    );
  }

  const { alertId } =
    await params;

  const backendHeaders =
    new Headers();

  backendHeaders.set(
    "Authorization",
    `Bearer ${token}`
  );

  const range =
    request.headers.get("range");

  if (range) {
    backendHeaders.set(
      "Range",
      range
    );
  }

  const response =
    await fetch(
      `${backendBaseUrl}/api/admin/qa-alerts/${encodeURIComponent(
        alertId
      )}/evidence-playback`,
      {
        method: "GET",
        headers: backendHeaders,
        cache: "no-store",
      }
    );

  if (!response.ok) {
    const errorText =
      await response
        .text()
        .catch(() => "");

    return NextResponse.json(
      {
        error:
          errorText ||
          "QA evidence is unavailable.",
      },
      {
        status: response.status,
      }
    );
  }

  if (!response.body) {
    return NextResponse.json(
      {
        error:
          "QA evidence response was empty.",
      },
      {
        status: 502,
      }
    );
  }

  const headers =
    new Headers();

  for (const name of [
    "content-type",
    "content-length",
    "content-range",
    "accept-ranges",
  ]) {
    const value =
      response.headers.get(name);

    if (value) {
      headers.set(name, value);
    }
  }

  if (
    !headers.has(
      "content-type"
    )
  ) {
    headers.set(
      "content-type",
      "audio/wav"
    );
  }

  headers.set(
    "cache-control",
    "private, no-store, max-age=0"
  );

  return new NextResponse(
    response.body,
    {
      status: response.status,
      headers,
    }
  );
}
