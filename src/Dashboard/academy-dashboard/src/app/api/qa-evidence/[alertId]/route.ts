import { NextResponse } from "next/server";

const backendBaseUrl =
  process.env.BACKEND_BASE_URL ?? "http://localhost:5100";

function getAuthToken(request: Request): string | null {
  return (
    request.headers
      .get("cookie")
      ?.split(";")
      .map((cookie) => cookie.trim())
      .find((cookie) =>
        cookie.startsWith("qa_auth_token=")
      )
      ?.slice("qa_auth_token=".length) ?? null
  );
}

export async function GET(
  request: Request,
  {
    params,
  }: {
    params: Promise<{ alertId: string }>;
  }
) {
  const token = getAuthToken(request);

  if (!token) {
    return NextResponse.json(
      { error: "Not authenticated" },
      { status: 401 }
    );
  }

  const { alertId } = await params;

  const playbackResponse = await fetch(
    `${backendBaseUrl}/api/admin/qa-alerts/${encodeURIComponent(
      alertId
    )}/evidence-playback`,
    {
      method: "GET",
      headers: {
        Authorization: `Bearer ${token}`,
      },
      cache: "no-store",
    }
  );

  if (!playbackResponse.ok) {
    const message =
      await playbackResponse.text().catch(() => "");

    return NextResponse.json(
      {
        error:
          message ||
          "QA evidence is unavailable.",
      },
      { status: playbackResponse.status }
    );
  }

  const payload = (await playbackResponse
    .json()
    .catch(() => null)) as
    | { url?: string }
    | null;

  if (!payload?.url) {
    return NextResponse.json(
      {
        error:
          "QA evidence playback URL is unavailable.",
      },
      { status: 502 }
    );
  }

  const evidenceResponse = await fetch(
    payload.url,
    {
      method: "GET",
      cache: "no-store",
    }
  );

  if (!evidenceResponse.ok || !evidenceResponse.body) {
    return NextResponse.json(
      {
        error:
          "QA evidence storage could not be reached.",
      },
      { status: 502 }
    );
  }

  const headers = new Headers();

  headers.set(
    "Content-Type",
    evidenceResponse.headers.get("content-type") ??
      "audio/wav"
  );

  headers.set(
    "Cache-Control",
    "private, no-store, max-age=0"
  );

  const contentLength =
    evidenceResponse.headers.get("content-length");

  if (contentLength) {
    headers.set(
      "Content-Length",
      contentLength
    );
  }

  return new NextResponse(
    evidenceResponse.body,
    {
      status: 200,
      headers,
    }
  );
}
